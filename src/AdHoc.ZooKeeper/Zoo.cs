// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeper;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;
using static AdHoc.ZooKeeper.Session;

namespace AdHoc.ZooKeeper;
public class Zoo
    : IZooKeeper
{
    private readonly ZooKeeperPath _root;
    private readonly bool _owned;
    private Session? _session;

    private ImmutableArray<Host> _hosts;
    private SemaphoreSlim _lock;

    private readonly ConcurrentDictionary<Watcher, WatchAsync> _watchers = new();

    internal Zoo(
        Session session,
        ImmutableArray<Host> hosts,
        ZooKeeperPath root,
        SemaphoreSlim @lock
    )
    {
        _root = root;
        _hosts = hosts;
        _session = session;
        _lock = @lock;
    }

    public Zoo(
        IEnumerable<Host> hosts,
        ZooKeeperPath root = default,
        IReadOnlySet<Authentication>? authentications = null,
        TimeSpan? connectionTimeout = default,
        TimeSpan? sessionTimeout = default,
        bool readOnly = false
    )
    {
        ArgumentNullException.ThrowIfNull(hosts);
        _hosts = hosts.Where(h => h != default).ToImmutableArray();
        if (_hosts.Length < 2)
            throw new ArgumentException($"A zoo should have at least two hosts.", nameof(hosts));

        connectionTimeout ??= DefaultConnectionTimeout;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(connectionTimeout.Value, TimeSpan.Zero);

        sessionTimeout ??= DefaultSessionTimeout;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sessionTimeout.Value, TimeSpan.Zero);

        _root = root;
        _session = new(
            _hosts[Random.Shared.Next(0, _hosts.Length)],
            authentications?.ToFrozenSet() ?? FrozenSet<Authentication>.Empty,
            connectionTimeout.Value,
            sessionTimeout.Value,
            readOnly
        );
        _lock = new SemaphoreSlim(1, 1);
        _owned = true;
    }

    public async Task<TResult> ExecuteAsync<TResult>(IZooKeeperOperation<TResult> transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var session = _session;
        ObjectDisposedException.ThrowIf(session is null, this);
        try
        {
            return await ExecutingAsync(session, cancellationToken);
        }
        catch (ConnectionException ex)
        {
            var result = await TryReconnectAsync(session, ex.Host, ex, ExecutingAsync, cancellationToken);
            if (result.Item2 is not null)
                throw result.Item2;
            return result.Item1!;
        }

        Task<TResult> ExecutingAsync(Session session, CancellationToken cancellationToken) =>
            session.ExecuteAsync(transaction, _root, (watcher, watch) =>
            {
                _watchers.TryAdd(watcher, watch);
                return async (watcher, @event, cancellationToken) =>
                {
                    if (@event.State != States.Disconnected)
                    {
                        await watch(watcher, @event, cancellationToken);
                        return;
                    }

                    var result = await TryReconnectAsync<object?>(session, ((Watcher)watcher)._host, null, null, cancellationToken);
                    if (result.Item2 is not null)
                        await watch(watcher, @event, cancellationToken);
                };
            }, cancellationToken);
    }


    private async Task<(TResult?, ConnectionException?)> TryReconnectAsync<TResult>(
        Session session,
        Host host,
        ConnectionException? exception,
        Func<Session, CancellationToken, Task<TResult>>? executeAsync,
        CancellationToken cancellationToken
    )
    {
        int length = _hosts.Length;
        int usedIndex = _hosts.IndexOf(host);
        int currentIndex = _hosts.IndexOf(session.Host);
        var exceptions = new List<Exception>(length);
        if (exception is not null)
            exceptions.Add(exception);

        if (usedIndex != currentIndex)
            try
            {
                if (executeAsync is null)
                    return default;
                return (await executeAsync(session, cancellationToken), null);
            }
            catch (ConnectionException ex)
            {
                exceptions.Add(ex);
            }

        currentIndex = (currentIndex + 1) % length;
        while (usedIndex != currentIndex)
        {
            await _lock.WaitAsync(cancellationToken);
            bool locked = true;
            try
            {
                await session.ReconnectAsync(_hosts[currentIndex], cancellationToken);

                _lock.Release();
                locked = false;

                if (executeAsync is null)
                    return default;
                return (await executeAsync(session, cancellationToken), null);
            }
            catch (ConnectionException ex)
            {
                exceptions.Add(ex);
            }
            finally
            {
                if (locked)
                    _lock.Release();
            }
            currentIndex = (currentIndex + 1) % length;
        }

        return (default, new ConnectionException($"Couldn't establish a stable connection to any of {string.Join(", ", _hosts)}.", new AggregateException(exceptions))
        {
            Host = host
        });
    }


    public async ValueTask DisposeAsync()
    {
        while (!_watchers.IsEmpty)
        {
            var watchPair = _watchers.FirstOrDefault();
            if (_watchers.TryRemove(watchPair))
                await watchPair.Key.DisposeAsync();
        }

        if (_owned)
        {
            var session = _session;
            if (session is not null)
                await session.CloseAsync();
        }
        _session = null;
        GC.SuppressFinalize(this);
    }
}

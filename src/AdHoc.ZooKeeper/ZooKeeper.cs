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
public class ZooKeeper
    : IZooKeeper
{
    private readonly ZooKeeperPath _root;
    private readonly bool _owned;
    internal Session? _session;

    private ImmutableArray<Host> _hosts;
    private Host _currentHost;
    private SemaphoreSlim _reconnectLock;

    private readonly ConcurrentDictionary<Watcher, WatchAsync> _watchers = new();

    internal ZooKeeper(
        Session session,
        ImmutableArray<Host> hosts,
        ZooKeeperPath root,
        SemaphoreSlim @lock
    )
    {
        _root = root;
        _hosts = hosts;
        _session = session;
        _reconnectLock = @lock;
    }

    public ZooKeeper(ZooKeeperConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _root = connection.Root;
        _hosts = [.. connection.Hosts];
        _session = new(
            _currentHost = _hosts[Random.Shared.Next(0, _hosts.Length)],
            connection.Authentications.ToFrozenSet(),
            connection.ConnectionTimeout,
            connection.SessionTimeout,
            connection.ReadOnly
        );
        _reconnectLock = new SemaphoreSlim(1, 1);
        _owned = true;
    }

    public ZooKeeper(string connectionString)
        : this(Parse(connectionString)) { }


    public async Task<TResponse> ExecuteAsync<TResponse>(IZooKeeperTransaction<TResponse> transaction, CancellationToken cancellationToken)
        where TResponse : IZooKeeperResponse
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var session = _session;
        ObjectDisposedException.ThrowIf(session is null, this);
        try
        {
            return await ExecutingAsync(session, cancellationToken);
        }
        catch (ConnectionException ex) when (ex is not SessionExpiredException)
        {
            var result = await TryReconnectAsync(session, ex.Host, ex, ExecutingAsync, cancellationToken);
            if (result.Item2 is not null)
                throw result.Item2;
            return result.Item1!;
        }

        Task<TResponse> ExecutingAsync(Session session, CancellationToken cancellationToken) =>
            session.ExecuteAsync(transaction, _root, (watcher, watch) =>
            {
                Host host = _currentHost;
                _watchers.TryAdd(watcher, watch);
                return (watcher, @event, cancellationToken) =>
                {
                    if (@event.State == States.Disconnected)
                    {
                        return new(Task.WhenAll(
                            Task.Run(async () =>
                            {
                                await TryReconnectAsync<object?>(session, host, null, null, cancellationToken);
                                host = _currentHost;
                            }, cancellationToken),
                            watch(watcher, @event, cancellationToken).AsTask()
                        ));
                    }

                    return watch(watcher, @event, cancellationToken);
                };
            }, cancellationToken);
    }


    internal async Task<(TResult?, ConnectionException?)> TryReconnectAsync<TResult>(
        Session session,
        Host host,
        ConnectionException? exception,
        Func<Session, CancellationToken, Task<TResult>>? executeAsync,
        CancellationToken cancellationToken
    )
    {
        if (session.IsConnected)
            return (await InvokeExecuteAsync(session, executeAsync, cancellationToken), null);

        int length = _hosts.Length;
        int usedIndex = _hosts.IndexOf(host);
        int currentIndex = _hosts.IndexOf(_currentHost);
        var exceptions = new List<Exception>(length);
        if (exception is not null)
            exceptions.Add(exception);

        if (usedIndex != currentIndex) // already reconnected
            return (await InvokeExecuteAsync(session, executeAsync, cancellationToken), null);

        await _reconnectLock.WaitAsync(cancellationToken);
        try
        {
            if (session.IsConnected)
                return (await InvokeExecuteAsync(session, executeAsync, cancellationToken), null);

            if (currentIndex != _hosts.IndexOf(_currentHost)) // already reconnected
                return (await InvokeExecuteAsync(session, executeAsync, cancellationToken), null);

            if (usedIndex == -1)
                usedIndex = Random.Shared.Next(0, _hosts.Length);
            currentIndex = (usedIndex + 1) % length;
            do
            {
                try
                {
                    _currentHost = _hosts[currentIndex];
                    await session.ReconnectAsync(_currentHost, cancellationToken);
                    return (await InvokeExecuteAsync(session, executeAsync, cancellationToken), null);
                }
                catch (ConnectionException ex)
                {
                    exceptions.Add(ex);
                }
                currentIndex = (currentIndex + 1) % length;
            } while (currentIndex != usedIndex);
        }
        finally
        {
            _reconnectLock.Release();
        }

        return (default, CreateConnectionException(host, $"Couldn't establish a stable connection to any of {string.Join(", ", _hosts)}.", new AggregateException(exceptions)));

        static Task<TResult> InvokeExecuteAsync(Session session, Func<Session, CancellationToken, Task<TResult>>? executeAsync, CancellationToken cancellationToken)
        {
            if (executeAsync is null)
                return Task.FromResult<TResult>(default!);
            return executeAsync(session, cancellationToken);
        }
    }

    private static ConnectionException CreateConnectionException(
        Host host,
        string message,
        AggregateException exception
    ) =>
        exception.InnerExceptions.Any(e => e is ConnectionLostException)
            ? new ConnectionLostException(message, exception) { Host = host }
            : new ConnectionException(message, exception) { Host = host };


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

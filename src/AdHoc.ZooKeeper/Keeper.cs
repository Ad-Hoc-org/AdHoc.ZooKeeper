// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Collections.Frozen;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;
using static AdHoc.ZooKeeper.Session;

namespace AdHoc.ZooKeeper;
public class Keeper
    : IZooKeeper
{
    private readonly ZooKeeperPath _root;
    private readonly bool _owned;
    private Session? _session;

    private readonly ConcurrentDictionary<Watcher, WatchAsync> _watchers = new();

    internal Keeper(
        Session session,
        ZooKeeperPath root
    )
    {
        _root = root;
        _session = session;
    }

    public Keeper(
        Host host,
        ZooKeeperPath root = default,
        IReadOnlySet<Authentication>? authentications = null,
        TimeSpan? connectionTimeout = default,
        TimeSpan? sessionTimeout = default,
        bool readOnly = false
    )
    {
        if (host == default)
            throw new ArgumentNullException(nameof(host));

        connectionTimeout ??= DefaultConnectionTimeout;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(connectionTimeout.Value, TimeSpan.Zero);

        sessionTimeout ??= DefaultSessionTimeout;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sessionTimeout.Value, TimeSpan.Zero);

        _root = root;
        _session = new(
            host,
            authentications?.ToFrozenSet() ?? FrozenSet<Authentication>.Empty,
            connectionTimeout.Value,
            sessionTimeout.Value,
            readOnly
        );
        _owned = true;
    }


    public Task<TResult> ExecuteAsync<TResult>(IZooKeeperOperation<TResult> transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var session = _session;
        ObjectDisposedException.ThrowIf(session is null, this);
        return session.ExecuteAsync(transaction, _root, (watcher, watch) =>
        {
            _watchers.TryAdd(watcher, watch);
            return watch;
        }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        while (_watchers.Count > 0)
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
    }
}

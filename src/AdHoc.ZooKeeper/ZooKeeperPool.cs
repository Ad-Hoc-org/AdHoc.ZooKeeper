// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;

namespace AdHoc.ZooKeeper;
public class ZooKeeperPool
    : IZooKeeperProvider
{
    private sealed record Pooling(Session Session, ImmutableArray<Host> Hosts, SemaphoreSlim Lock);

    private ConcurrentDictionary<ZooKeeperConnection, Pooling>? _sessions;

    public ZooKeeperPool() =>
        _sessions = new();

    public IZooKeeper GetZooKeeper(ZooKeeperConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ObjectDisposedException.ThrowIf(_sessions is null, this);

        var pooling = _sessions.GetOrAdd(connection with { Root = ZooKeeperPath.Root }, (connection) =>
        {
            var @lock = new SemaphoreSlim(1, 1);
            var hosts = connection.Hosts.ToImmutableArray();
            return new(
                new Session(
                    hosts[Random.Shared.Next(0, hosts.Length)],
                    connection.Authentications.ToFrozenSet(),
                    connection.ConnectionTimeout,
                    connection.SessionTimeout,
                    connection.ReadOnly
                ),
                hosts,
                @lock
            );
        });
        return new ZooKeeper(pooling.Session, pooling.Hosts, connection.Root, pooling.Lock);
    }

    public async ValueTask DisposeAsync()
    {
        var sessions = _sessions;
        _sessions = null;
        if (sessions is not null)
            do
            {
                var key = sessions.Keys.FirstOrDefault();
                if (key is not null && sessions.TryRemove(sessions.Keys.First(), out var session))
                {
                    await session.Session.CloseAsync();
                    session.Lock.Dispose();
                }
            } while (!sessions.IsEmpty);
    }

}

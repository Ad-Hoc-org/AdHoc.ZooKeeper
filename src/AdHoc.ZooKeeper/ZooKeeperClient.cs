// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Frozen;
using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper;
public class ZooKeeperClient
    : IZooKeeper
{

    private readonly ZooKeeperConnection _connection;

    private Session? _currentSession;

    public ZooKeeperClient(ZooKeeperConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    public ZooKeeperClient(string connectionString)
        : this(ZooKeeperConnection.Parse(connectionString)) { }


    public Task<TResult> ExecuteAsync<TResult>(IZooKeeperOperation<TResult> transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return (_currentSession ??= new(
            _connection.Hosts.First(),
            _connection.Authentications.ToFrozenSet(),
            _connection.ConnectionTimeout,
            _connection.SessionTimeout,
            _connection.ReadOnly
        )).ExecuteAsync(transaction, _connection.Root, null, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var session = _currentSession;
        _currentSession = null;
        if (session is not null)
            await session.CloseAsync();
    }
}

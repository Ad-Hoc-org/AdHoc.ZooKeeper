// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

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
        _currentSession = new(connection.Hosts.First(), connection.Authentications, connection.ConnectionTimeout, connection.SessionTimeout, connection.ReadOnly);
    }

    public ZooKeeperClient(string connectionString)
        : this(ZooKeeperConnection.Parse(connectionString)) { }


    public Task<TResult> ExecuteAsync<TResult>(IZooKeeperOperation<TResult> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return _currentSession!.ExecuteAsync(operation, _connection.Root, cancellationToken);
    }

    public ValueTask DisposeAsync() =>
        _currentSession!.DisposeAsync();
}

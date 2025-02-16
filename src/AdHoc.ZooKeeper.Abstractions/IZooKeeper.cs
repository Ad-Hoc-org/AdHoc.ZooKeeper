// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public interface IZooKeeper
    : IAsyncDisposable
{
    public enum States : int
    {
        [Obsolete]
        Unknown = -1,
        Disconnected = 0,
        [Obsolete]
        NoSyncConnected = 1,
        SyncConnected = 3,
        AuthFailed = 4,
        ConnectedReadOnly = 5,
        SASLAuthenticated = 6,
        Expired = -112,
        Closed = 7
    }

    public Task<TResult> ExecuteAsync<TResult>(IZooKeeperOperation<TResult> operation, CancellationToken cancellationToken);
}

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public interface IZooKeeper
    : IZooKeeperTransactable,
        IAsyncDisposable
{
    enum States : int
    {
        Disconnected = 0,
        SyncConnected = 3,
        AuthFailed = 4,
        ConnectedReadOnly = 5,
        SASLAuthenticated = 6,
        Expired = -112,
        Closed = 7
    }
}

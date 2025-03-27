// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public interface IZooKeeperProvider
    : IAsyncDisposable
{
    public IZooKeeper GetZooKeeper(ZooKeeperConnection connection);
}

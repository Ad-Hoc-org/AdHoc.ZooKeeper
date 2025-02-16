// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public interface IZooKeeperProvider
{
    public IZooKeeper GetZooKeeper(ZooKeeperConnection connection);
}

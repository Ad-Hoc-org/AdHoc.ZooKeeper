// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper;
public class ZooKeeperProvider
    : IZooKeeperProvider
{
    public IZooKeeper GetZooKeeper(ZooKeeperConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new ZooKeeper(connection);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

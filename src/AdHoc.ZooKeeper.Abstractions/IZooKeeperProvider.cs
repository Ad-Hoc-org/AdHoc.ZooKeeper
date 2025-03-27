// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public interface IZooKeeperProvider
    : IAsyncDisposable
{
    public IZooKeeper GetZooKeeper(ZooKeeperConnection connection);
}

public static partial class ZooKeeperProviders
{
    public static IZooKeeper GetZooKeeper(this IZooKeeperProvider provider, string connectionString) =>
        provider.GetZooKeeper(ZooKeeperConnection.Parse(connectionString));
}

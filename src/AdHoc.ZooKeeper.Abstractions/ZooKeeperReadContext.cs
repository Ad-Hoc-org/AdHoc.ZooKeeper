// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public readonly ref struct ZooKeeperReadContext
{
    public ZooKeeperPath Root { get; }

    public int Request { get; }
    public long Transaction { get; }
    public ZooKeeperStatus Status { get; }

    public ReadOnlySpan<byte> Data { get; }

    public IZooKeeperWatcher? Watcher { get; }

    public ZooKeeperReadContext(
        ZooKeeperPath root,
        int request,
        long transaction,
        ZooKeeperStatus status,
        ReadOnlySpan<byte> data,
        IZooKeeperWatcher? watcher
    )
    {
        Root = root;
        Request = request;
        Transaction = transaction;
        Status = status;
        Data = data;
        Watcher = watcher;
    }
}

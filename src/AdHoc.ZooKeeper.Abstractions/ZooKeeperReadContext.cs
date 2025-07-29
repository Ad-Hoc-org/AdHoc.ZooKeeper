// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public readonly ref struct ZooKeeperReadContext(
    ZooKeeperPath root,
    ZooKeeperOperations operation,
    int request,
    long transaction,
    ZooKeeperStatus status,
    ReadOnlySpan<byte> data,
    IZooKeeperWatcher? watcher
)
{
    public ZooKeeperPath Root => root;
    public ZooKeeperOperations Operation => operation;

    public int Request => request;
    public long Transaction => transaction;
    public ZooKeeperStatus Status => status;

    public ReadOnlySpan<byte> Data { get; } = data;

    public IZooKeeperWatcher? Watcher => watcher;

    public ZooKeeperReadContext Slice(int start, ZooKeeperOperations operation, ZooKeeperStatus status) =>
        new(root, operation, request, transaction, status, Data.Slice(start), watcher);
}

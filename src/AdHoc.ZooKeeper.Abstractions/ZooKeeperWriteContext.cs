// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;

namespace AdHoc.ZooKeeper.Abstractions;
public readonly ref struct ZooKeeperWriteContext(
    ZooKeeperPath root,
    Span<byte> buffer,
    Action<ZooKeeperPath, Types, WatchAsync> registerWatcher
)
{
    public ZooKeeperPath Root => root;
    public Span<byte> Buffer { get; } = buffer;
    public void RegisterWatcher(ZooKeeperPath path, Types type, WatchAsync watch) =>
        registerWatcher(path, type, watch);

    public ZooKeeperWriteContext Slice(int start) =>
        new(Root, Buffer.Slice(start), registerWatcher);
}

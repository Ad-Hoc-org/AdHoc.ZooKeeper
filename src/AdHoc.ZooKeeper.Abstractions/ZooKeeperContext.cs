// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;

namespace AdHoc.ZooKeeper.Abstractions;
public readonly struct ZooKeeperContext
{
    public ZooKeeperPath Root { get; init; }

    public IBufferWriter<byte> Writer { get; }

    public Func<ZooKeeperOperation, int> GetRequest { get; }


    public Action<ZooKeeperPath, Types, WatchAsync> RegisterWatcher { get; }


    public ZooKeeperContext(
        ZooKeeperPath root,
        IBufferWriter<byte> writer,
        Func<ZooKeeperOperation, int> getRequest,
        Action<ZooKeeperPath, Types, WatchAsync> registerWatcher
    )
    {
        Root = root;
        Writer = writer;
        GetRequest = getRequest;
        RegisterWatcher = registerWatcher;
    }
}

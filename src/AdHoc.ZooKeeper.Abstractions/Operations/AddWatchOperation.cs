// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using static AdHoc.ZooKeeper.Abstractions.AddWatchOperation;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.Operations;

namespace AdHoc.ZooKeeper.Abstractions;

public sealed record AddWatchOperation
    : IZooKeeperOperation<IZooKeeperWatcher>
{

    private static readonly ReadOnlyMemory<byte> _Operation = new byte[] { 0, 0, 0, 106 };

    public ZooKeeperPath Path { get; }

    public bool Recursive { get; }

    public WatchAsync Watch { get; }


    private AddWatchOperation(ZooKeeperPath path, bool recursive, WatchAsync watch)
    {
        path.Validate();
        Path = path;
        Recursive = recursive;
        Watch = watch;
    }


    public void WriteRequest(in ZooKeeperContext context)
    {
        var writer = context.Writer;
        var buffer = writer.GetSpan(RequestHeaderSize + Path.GetMaxSize(context.Root) + sizeof(int));
        int size = LengthSize;

        size += Write(buffer.Slice(size), context.GetRequest(ZooKeeperOperation.AddWatch));

        _Operation.Span.CopyTo(buffer.Slice(size));
        size += OperationSize;

        size += Path.Write(buffer.Slice(size), context.Root);

        size += Write(buffer.Slice(size), Recursive ? 1 : 0);

        Write(buffer, size - LengthSize);

        context.RegisterWatcher((context.Root + Path).Absolute(), Recursive ? Types.RecursivePersistent : Types.Persistent, Watch);

        writer.Advance(size);
    }

    public IZooKeeperWatcher ReadResponse(in ZooKeeperResponse response, IZooKeeperWatcher? watcher)
    {
        Debug.Assert(watcher is not null);
        response.ThrowIfError();

        return watcher;
    }

    public static AddWatchOperation Create(ZooKeeperPath path, bool recursive, WatchAsync watch) =>
        new(path, recursive, watch);

}

public static partial class Operations
{
    public static Task<IZooKeeperWatcher> AddWatchAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        bool recursive,
        WatchAsync watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path, recursive, watch), cancellationToken);

    public static Task<IZooKeeperWatcher> AddWatchAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        bool recursive,
        Watch watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path, recursive, watch.ToAsyncWatch()), cancellationToken);
}

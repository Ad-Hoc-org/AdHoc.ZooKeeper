// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.GetChildrenOperation;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.Operations;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record GetChildrenOperation
    : IZooKeeperOperation<Result>
{
    private static readonly ReadOnlyMemory<byte> _Operation = new byte[] { 0, 0, 0, 8 };

    public ZooKeeperPath Path { get; }
    public WatchAsync? Watch { get; }

    private GetChildrenOperation(ZooKeeperPath path, WatchAsync? watch)
    {
        path.Validate();
        Path = path;
        Watch = watch;
    }

    public void WriteRequest(in ZooKeeperContext context)
    {
        var writer = context.Writer;
        var buffer = writer.GetSpan(RequestHeaderSize + Path.GetMaxSize(context.Root));
        int size = LengthSize;

        size += Write(buffer.Slice(size), context.GetRequest(ZooKeeperOperation.GetChildren));

        _Operation.Span.CopyTo(buffer.Slice(size));
        size += OperationSize;

        size += Path.Write(buffer.Slice(size), context.Root);

        if (Watch is not null)
        {
            context.RegisterWatcher([(context.Root + Path).Absolute()], Watch);
            buffer[size++] = 1;
        }
        else
            buffer[size++] = 0;

        Write(buffer, size - LengthSize);
        writer.Advance(size);
    }

    public Result ReadResponse(in ZooKeeperResponse response, IZooKeeperWatcher? watcher)
    {
        if (response.Status == ZooKeeperStatus.NoNode)
            return new(response.Transaction, default, watcher);

        response.ThrowIfError();

        var data = response.Data;
        var children = new ZooKeeperPath[ReadInt32(data)];

        int pos = LengthSize;
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = ZooKeeperPath.Read(data.Slice(pos), out int size);
            pos += size;
        }

        return new(response.Transaction, children, watcher);
    }

    public static GetChildrenOperation Create(ZooKeeperPath path, WatchAsync? watch = null) =>
        new(path, watch);

    public readonly record struct Result(
        long Transaction,
        ReadOnlyMemory<ZooKeeperPath>? Children,
        IZooKeeperWatcher? Watcher
    );
}

public static partial class Operations
{
    public static Task<Result> GetChildrenAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        WatchAsync watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path, watch), cancellationToken);

    public static Task<Result> GetChildrenAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        Watch watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path, watch.ToWatchAsync()), cancellationToken);

    public static Task<Result> GetChildrenAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path), cancellationToken);
}

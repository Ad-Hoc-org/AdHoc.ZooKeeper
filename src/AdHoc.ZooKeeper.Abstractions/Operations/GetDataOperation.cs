// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.GetDataOperation;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.Operations;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record GetDataOperation
    : IZooKeeperOperation<Result>
{
    private static readonly ReadOnlyMemory<byte> _Operation = new byte[] { 0, 0, 0, 4 };


    public ZooKeeperPath Path { get; }

    public WatchAsync? Watch { get; }


    private GetDataOperation(ZooKeeperPath path, WatchAsync? watch)
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

        size += Write(buffer.Slice(size), context.GetRequest(ZooKeeperOperation.GetData));

        _Operation.Span.CopyTo(buffer.Slice(size));
        size += OperationSize;

        size += Path.Write(buffer.Slice(size), context.Root);

        if (Watch is not null)
        {
            context.RegisterWatcher([(context.Root + Path).Absolute()], Types.Data, Watch);
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
            return new(response.Transaction, default, default, watcher);

        response.ThrowIfError();

        var data = ReadBuffer(response.Data, out int pos);
        return new(
            response.Transaction,
            data.ToArray(),
            ZooKeeperNode.Read(
                response.Data.Slice(pos),
                (response.Root + Path).Absolute(),
                out _
            ),
            watcher
        );
    }


    public static GetDataOperation Create(ZooKeeperPath path, WatchAsync? watch = null) =>
        new(path, watch);


    public readonly record struct Result(
        long Transaction,
        ReadOnlyMemory<byte> Data,
        ZooKeeperNode? Node,
        IZooKeeperWatcher? Watcher
    );
}

public static partial class Operations
{
    public static Task<Result> GetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        WatchAsync watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path), cancellationToken);

    public static Task<Result> GetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        Watch watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path, watch.ToWatchAsync()), cancellationToken);

    public static Task<Result> GetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path), cancellationToken);
}

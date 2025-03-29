// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.GetDataTransaction;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record GetDataTransaction
    : IZooKeeperTransaction<Response>
{
    public ZooKeeperOperations Operation => ZooKeeperOperations.GetData;

    public ZooKeeperPath Path { get; }

    public WatchAsync? Watch { get; }


    private GetDataTransaction(ZooKeeperPath path, WatchAsync? watch)
    {
        path.ThrowIfEmptyOrInvalid();
        Path = path;
        Watch = watch;
    }

    public static GetDataTransaction Create(ZooKeeperPath path, WatchAsync? watch = null) =>
        new(path, watch);


    public int GetMaxRequestSize(in ZooKeeperPath root) =>
        Path.GetMaxBufferSize(root) + BooleanSize;

    public int WriteRequest(in ZooKeeperWriteContext context)
    {
        var buffer = context.Buffer;

        var path = Path.Normalize(context.Root);
        int size = path.Write(buffer);

        if (Watch is null)
            buffer[size++] = 0;
        else
        {
            buffer[size++] = 1;
            context.RegisterWatcher(path, Types.Data, Watch);
        }

        return size;
    }

    public Response ReadResponse(in ZooKeeperReadContext context)
    {
        if (context.Status == ZooKeeperStatus.NoNode)
            return new(context.Transaction, ReadOnlyMemory<byte>.Empty, default, context.Watcher);

        context.Status.ThrowIfError();

        return new(
            context.Transaction,
            ReadBuffer(context.Data, out int pos).ToArray(),
            ZooKeeperNode.Read(
                context.Data.Slice(pos),
                (context.Root + Path).Absolute,
                out _
            ),
            context.Watcher
        );
    }


    public readonly record struct Response(
        long Transaction,
        ReadOnlyMemory<byte> Data,
        ZooKeeperNode? Node,
        IZooKeeperWatcher? Watcher
    ) :
        IZooKeeperResponse,
        IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Watcher?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}

public static partial class ZooKeeperTransactions
{
    public static Task<Response> GetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        WatchAsync watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, watch), cancellationToken);

    public static Task<Response> GetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        Watch watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, watch.ToAsyncWatch()), cancellationToken);

    public static Task<Response> GetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path), cancellationToken);
}

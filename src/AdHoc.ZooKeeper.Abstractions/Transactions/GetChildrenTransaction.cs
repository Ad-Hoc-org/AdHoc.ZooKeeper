// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.GetChildrenTransaction;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record GetChildrenTransaction
    : IZooKeeperTransaction<Response>
{
    public ZooKeeperOperations Operation => ZooKeeperOperations.GetChildren;


    public ZooKeeperPath Path { get; }

    public WatchAsync? Watch { get; }


    private GetChildrenTransaction(ZooKeeperPath path, WatchAsync? watch)
    {
        path.ThrowIfEmptyOrInvalid();
        Path = path;
        Watch = watch;
    }

    public static GetChildrenTransaction Create(ZooKeeperPath path, WatchAsync? watch = null) =>
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
            context.RegisterWatcher(path, Types.Children, Watch);
        }

        return size;
    }

    public Response ReadResponse(in ZooKeeperReadContext context)
    {
        if (context.Status == ZooKeeperStatus.NoNode)
            return new(context.Transaction, default, context.Watcher);

        context.Status.ThrowIfError();

        var data = context.Data;
        var children = new ZooKeeperPath[ReadInt32(data)];

        int pos = LengthSize;
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = ZooKeeperPath.Read(data.Slice(pos), out int size);
            pos += size;
        }

        return new(context.Transaction, children, context.Watcher);
    }


    public readonly record struct Response(
        long Transaction,
        ReadOnlyMemory<ZooKeeperPath>? Children,
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
    public static Task<Response> GetChildrenAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        WatchAsync watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, watch), cancellationToken);

    public static Task<Response> GetChildrenAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        Watch watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, watch.ToAsyncWatch()), cancellationToken);

    public static Task<Response> GetChildrenAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path), cancellationToken);
}

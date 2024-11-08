// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using static AdHoc.ZooKeeper.Abstractions.ExistsTransaction;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record ExistsTransaction
    : IZooKeeperTransaction<Response>
{
    public ZooKeeperOperations Operation => ZooKeeperOperations.Exists;


    public ZooKeeperPath Path { get; }

    public WatchAsync? Watch { get; }


    private ExistsTransaction(ZooKeeperPath path, WatchAsync? watch)
    {
        path.ThrowIfEmptyOrInvalid();
        Path = path;
        Watch = watch;
    }

    public static ExistsTransaction Create(ZooKeeperPath path, WatchAsync? watch = null) =>
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
            context.RegisterWatcher(path, Types.Exists, Watch);
        }

        return size;
    }

    public Response ReadResponse(in ZooKeeperReadContext context, out int size)
    {
        Debug.Assert(context.Operation == Operation);

        size = 0;
        if (context.Status == ZooKeeperStatus.NoNode)
            return new(context.Transaction, Path.Normalize(context.Root), default, context.Watcher);

        context.Status.ThrowIfError();

        return new(
            context.Transaction,
            Path.Normalize(context.Root),
            ZooKeeperNode.Read(
                context.Data,
                out size
            ),
            context.Watcher
        );
    }


    public readonly record struct Response(
        long Transaction,
        ZooKeeperPath Path,
        ZooKeeperNode? Node,
        IZooKeeperWatcher? Watcher
    ) :
        IZooKeeperResponse,
        IAsyncDisposable
    {
        [MemberNotNullWhen(true, nameof(Node))]
        public bool Existed => Node is not null;
        public ValueTask DisposeAsync() => Watcher?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}

public static partial class ZooKeeperTransactions
{
    public static Task<Response> ExistsAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        WatchAsync watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(ExistsTransaction.Create(path, watch), cancellationToken);

    public static Task<Response> ExistsAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        Watch watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(ExistsTransaction.Create(path, watch.ToAsyncWatch()), cancellationToken);

    public static Task<Response> ExistsAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(ExistsTransaction.Create(path), cancellationToken);
}

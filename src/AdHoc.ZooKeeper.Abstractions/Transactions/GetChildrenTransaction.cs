// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

    public Response ReadResponse(in ZooKeeperReadContext context, out int size)
    {
        // in multi transaction the response may not match the operation
        Debug.Assert(context.Operation == Operation
            || context.Operation is ZooKeeperOperations.GetChildrenWithNode);

        size = 0;
        if (context.Status == ZooKeeperStatus.NoNode)
            return new(context.Transaction, Path.Normalize(context.Root), default, default, context.Watcher);

        context.Status.ThrowIfError();

        var data = context.Data;
        var children = new ZooKeeperPath[ReadInt32(data)];

        size = LengthSize;
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = ZooKeeperPath.Read(data.Slice(size), out int pathSize);
            size += pathSize;
        }

        ZooKeeperNode? node = default;
        if (context.Operation == ZooKeeperOperations.GetChildrenWithNode)
        {
            node = ZooKeeperNode.Read(data.Slice(size), out int nodeSize);
            size += nodeSize;
        }

        return new(context.Transaction, Path.Normalize(context.Root), node, children, context.Watcher);
    }


    public readonly record struct Response(
        long Transaction,
        ZooKeeperPath Path,
        ZooKeeperNode? Node,
        ReadOnlyMemory<ZooKeeperPath>? Children,
        IZooKeeperWatcher? Watcher
    ) :
        IZooKeeperResponse,
        IAsyncDisposable
    {
        [MemberNotNullWhen(true, nameof(Children))]
        public bool Existed => Children is not null;

        [MemberNotNullWhen(true, nameof(Children))]
        public bool HasChildren => Children?.Length > 0;


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
        zooKeeper.ExecuteAsync(GetChildrenTransaction.Create(path, watch), cancellationToken);

    public static Task<Response> GetChildrenAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        Watch watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(GetChildrenTransaction.Create(path, watch.ToAsyncWatch()), cancellationToken);

    public static Task<Response> GetChildrenAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(GetChildrenTransaction.Create(path), cancellationToken);

    public static ZooKeeperTransaction.Builder GetChildren(
        this ZooKeeperTransaction.Builder transaction,
        ZooKeeperPath path,
        WatchAsync watch
    ) =>
        transaction.AddTransaction(GetChildrenTransaction.Create(path, watch));

    public static ZooKeeperTransaction.Builder GetChildren(
        this ZooKeeperTransaction.Builder transaction,
        ZooKeeperPath path,
        Watch watch
    ) =>
        transaction.AddTransaction(GetChildrenTransaction.Create(path, watch.ToAsyncWatch()));

    public static ZooKeeperTransaction.Builder GetChildren(
        this ZooKeeperTransaction.Builder transaction,
        ZooKeeperPath path
    ) =>
        transaction.AddTransaction(GetChildrenTransaction.Create(path));
}

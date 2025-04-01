// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

    public Response ReadResponse(in ZooKeeperReadContext context, out int size)
    {
        Debug.Assert(context.Operation == Operation);

        size = 0;
        if (context.Status == ZooKeeperStatus.NoNode)
            return new(context.Transaction, Path.Normalize(context.Root), ReadOnlyMemory<byte>.Empty, default, context.Watcher);

        context.Status.ThrowIfError();

        var data = ReadBuffer(context.Data, out size);
        var node = ZooKeeperNode.Read(context.Data.Slice(size), out int nodeSize);
        size += nodeSize;

        return new(
            context.Transaction,
            Path.Normalize(context.Root),
            data.ToArray(),
            node,
            context.Watcher
        );
    }


    public readonly record struct Response(
        long Transaction,
        ZooKeeperPath Path,
        ReadOnlyMemory<byte> Data,
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
    public static Task<Response> GetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        WatchAsync watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(GetDataTransaction.Create(path, watch), cancellationToken);

    public static Task<Response> GetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        Watch watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(GetDataTransaction.Create(path, watch.ToAsyncWatch()), cancellationToken);

    public static Task<Response> GetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(GetDataTransaction.Create(path), cancellationToken);


    public static ZooKeeperTransaction.Builder GetData(
        this ZooKeeperTransaction.Builder transaction,
        ZooKeeperPath path,
        WatchAsync watch
    ) =>
        transaction.AddTransaction(GetDataTransaction.Create(path, watch));

    public static ZooKeeperTransaction.Builder GetData(
        this ZooKeeperTransaction.Builder transaction,
        ZooKeeperPath path,
        Watch watch
    ) =>
        transaction.AddTransaction(GetDataTransaction.Create(path, watch.ToAsyncWatch()));

    public static ZooKeeperTransaction.Builder GetData(
        this ZooKeeperTransaction.Builder transaction,
        ZooKeeperPath path
    ) =>
        transaction.AddTransaction(GetDataTransaction.Create(path));
}

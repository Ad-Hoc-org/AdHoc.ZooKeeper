// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using static AdHoc.ZooKeeper.Abstractions.AddWatchTransaction;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;

public sealed record AddWatchTransaction
    : IZooKeeperTransaction<Response>
{
    public ZooKeeperOperations Operation => ZooKeeperOperations.AddWatch;


    public ZooKeeperPath Path { get; }

    private readonly Types _type;
    public bool Recursive => _type is Types.RecursivePersistent;

    public WatchAsync Watch { get; }


    private AddWatchTransaction(ZooKeeperPath path, bool recursive, WatchAsync watch)
    {
        path.ThrowIfEmptyOrInvalid();
        ArgumentNullException.ThrowIfNull(watch);
        Path = path;
        _type = recursive ? Types.RecursivePersistent : Types.Persistent;
        Watch = watch;
    }

    public static AddWatchTransaction Create(ZooKeeperPath path, bool recursive, WatchAsync watch) =>
        new(path, recursive, watch);


    public int GetMaxRequestSize(in ZooKeeperPath root) =>
        Path.GetMaxBufferSize(root) + Int32Size;

    public int WriteRequest(in ZooKeeperWriteContext context)
    {
        var buffer = context.Buffer;

        var path = Path.Normalize(context.Root);
        int size = path.Write(buffer);
        size += Write(buffer.Slice(size), Recursive ? 1 : 0);

        context.RegisterWatcher(path, _type, Watch);
        return size;
    }

    public Response ReadResponse(in ZooKeeperReadContext context, out int size)
    {
        Debug.Assert(context.Watcher is not null);
        Debug.Assert(context.Operation == Operation);
        context.Status.ThrowIfError();
        size = 0;
        return new(context.Transaction, context.Watcher);
    }


    public readonly record struct Response(
        long Transaction,
        IZooKeeperWatcher Watcher
    ) :
        IZooKeeperResponse,
        IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Watcher.DisposeAsync();
    }
}

public static partial class ZooKeeperTransactions
{
    public static Task<Response> AddWatchAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        bool recursive,
        WatchAsync watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(AddWatchTransaction.Create(path, recursive, watch), cancellationToken);

    public static Task<Response> AddWatchAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        bool recursive,
        Watch watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(AddWatchTransaction.Create(path, recursive, watch.ToAsyncWatch()), cancellationToken);

    public static Task<Response> AddWatchAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        WatchAsync watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.AddWatchAsync(path, false, watch, cancellationToken);

    public static Task<Response> AddWatchAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        Watch watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.AddWatchAsync(path, false, watch, cancellationToken);

    public static Task<Response> AddWatchRecursiveAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        WatchAsync watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.AddWatchAsync(path, true, watch, cancellationToken);

    public static Task<Response> AddWatchRecursiveAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        Watch watch,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.AddWatchAsync(path, true, watch, cancellationToken);
}

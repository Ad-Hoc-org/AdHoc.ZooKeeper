// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Frozen;
using System.Diagnostics;
using static AdHoc.ZooKeeper.Abstractions.SetWatchersTransaction;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record SetWatchersTransaction
    : IZooKeeperTransaction<Response>
{
    public const int Request = -9;

    public ZooKeeperOperations Operation { get; }

    public long LastTransaction { get; }

    public IReadOnlySet<ZooKeeperPath> Data { get; }
    public IReadOnlySet<ZooKeeperPath> Children { get; }
    public IReadOnlySet<ZooKeeperPath> Exists { get; }
    public IReadOnlySet<ZooKeeperPath> Persistent { get; }
    public IReadOnlySet<ZooKeeperPath> RecursivePersistent { get; }


    private readonly int _maxSize;

    private SetWatchersTransaction(
        long lastTransaction,
        IReadOnlySet<ZooKeeperPath> data,
        IReadOnlySet<ZooKeeperPath> children,
        IReadOnlySet<ZooKeeperPath> exists,
        IReadOnlySet<ZooKeeperPath> persistent,
        IReadOnlySet<ZooKeeperPath> recursivePersistent
    )
    {
        Data = data;
        Children = children;
        Exists = exists;
        Persistent = persistent;
        RecursivePersistent = recursivePersistent;

        _maxSize = Int32Size + TransactionSize + (LengthSize * 3)
            + Data.Sum(p => p.GetMaxBufferSize())
            + Children.Sum(p => p.GetMaxBufferSize())
            + Exists.Sum(p => p.GetMaxBufferSize());
        if (persistent.Count > 0 || recursivePersistent.Count > 0)
        {
            Operation = ZooKeeperOperations.SetWatchesWithPersistent;
            _maxSize = (LengthSize * 2) + Persistent.Sum(p => p.GetMaxBufferSize())
                + RecursivePersistent.Sum(p => p.GetMaxBufferSize());
        }
        else
            Operation = ZooKeeperOperations.SetWatches;
    }

    public static SetWatchersTransaction Create(
        long lastTransaction,
        IEnumerable<ZooKeeperPath>? data = null,
        IEnumerable<ZooKeeperPath>? children = null,
        IEnumerable<ZooKeeperPath>? exists = null,
        IEnumerable<ZooKeeperPath>? persistent = null,
        IEnumerable<ZooKeeperPath>? recursivePersistent = null
    ) => new(
        lastTransaction,
        data?.ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty,
        children?.ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty,
        exists?.ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty,
        persistent?.ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty,
        recursivePersistent?.ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty
    );


    public int GetMaxRequestSize(in ZooKeeperPath root) => _maxSize;

    public int WriteRequest(in ZooKeeperWriteContext context)
    {
        var buffer = context.Buffer;

        int size = Write(buffer, LastTransaction);

        size += WritePaths(buffer.Slice(size), Data);
        size += WritePaths(buffer.Slice(size), Exists);
        size += WritePaths(buffer.Slice(size), Children);

        if (Operation == ZooKeeperOperations.SetWatchesWithPersistent)
        {
            size += WritePaths(buffer.Slice(size), Persistent);
            size += WritePaths(buffer.Slice(size), RecursivePersistent);
        }

        return size;
    }
    private static int WritePaths(Span<byte> buffer, IReadOnlySet<ZooKeeperPath> paths)
    {
        int size = Write(buffer, paths.Count);
        foreach (var path in paths)
            size += path.Write(buffer.Slice(size));
        return size;
    }


    public Response ReadResponse(in ZooKeeperReadContext context, out int size)
    {
        Debug.Assert(context.Operation == Operation);
        Debug.Assert(context.Request == Request);
        context.Status.ThrowIfError();
        size = 0;
        return new(context.Transaction);
    }


    public readonly record struct Response(long Transaction) : IZooKeeperResponse;
}

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
    public IReadOnlySet<ZooKeeperPath> Any { get; }
    public IReadOnlySet<ZooKeeperPath> Persistent { get; }
    public IReadOnlySet<ZooKeeperPath> RecursivePersistent { get; }


    private readonly int _maxSize;

    private SetWatchersTransaction(
        long lastTransaction,
        IReadOnlySet<ZooKeeperPath> data,
        IReadOnlySet<ZooKeeperPath> children,
        IReadOnlySet<ZooKeeperPath> any,
        IReadOnlySet<ZooKeeperPath> persistent,
        IReadOnlySet<ZooKeeperPath> recursivePersistent
    )
    {
        Data = data;
        Children = children;
        Any = any;
        Persistent = persistent;
        RecursivePersistent = recursivePersistent;

        _maxSize = Int32Size + TransactionSize + (LengthSize * 3)
            + Data.Sum(p => p.GetMaxBufferSize())
            + Children.Sum(p => p.GetMaxBufferSize())
            + Any.Sum(p => p.GetMaxBufferSize());
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
        IEnumerable<ZooKeeperPath>? any = null,
        IEnumerable<ZooKeeperPath>? persistent = null,
        IEnumerable<ZooKeeperPath>? recursivePersistent = null
    ) => new(
        lastTransaction,
        data?.ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty,
        children?.ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty,
        any?.ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty,
        persistent?.ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty,
        recursivePersistent?.ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty
    );


    public int GetMaxRequestSize(in ZooKeeperPath root) => _maxSize;

    public int WriteRequest(in ZooKeeperWriteContext context)
    {
        var buffer = context.Buffer;

        int size = Write(buffer, LastTransaction);

        size += WritePaths(buffer.Slice(size), Data);
        size += WritePaths(buffer.Slice(size), Any);
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


    public Response ReadResponse(in ZooKeeperReadContext context)
    {
        Debug.Assert(context.Request == Request);
        context.Status.ThrowIfError();
        return new(context.Transaction);
    }


    public readonly record struct Response(long Transaction) : IZooKeeperResponse;
}

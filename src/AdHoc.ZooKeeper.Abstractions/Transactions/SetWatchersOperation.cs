// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Collections.Frozen;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public static class SetWatcherOperations
{
    public const int Request = -9;

    private static readonly ReadOnlyMemory<byte> _RequestBytes = new byte[] { 255, 255, 255, 247 };
    private static readonly ReadOnlyMemory<byte> _OperationBytes = new byte[] { 0, 0, 0, 101 };
    private static readonly ReadOnlyMemory<byte> _OperationWithPersistentBytes = new byte[] { 0, 0, 0, 105 };

    public static void Write(
        IBufferWriter<byte> writer,
        long lastTransaction,
        IReadOnlySet<ZooKeeperPath>? data = null,
        IReadOnlySet<ZooKeeperPath>? exists = null,
        IReadOnlySet<ZooKeeperPath>? children = null,
        IReadOnlySet<ZooKeeperPath>? persistent = null,
        IReadOnlySet<ZooKeeperPath>? recursivePersistent = null
    )
    {
        data ??= FrozenSet<ZooKeeperPath>.Empty;
        exists ??= FrozenSet<ZooKeeperPath>.Empty;
        children ??= FrozenSet<ZooKeeperPath>.Empty;
        persistent ??= FrozenSet<ZooKeeperPath>.Empty;
        recursivePersistent ??= FrozenSet<ZooKeeperPath>.Empty;
        var hasPersistent = persistent.Any() || recursivePersistent.Any();

        var buffer = writer.GetSpan(RequestHeaderSize
            + Int32Size
            + GetMaxPathsSize(children)
            + GetMaxPathsSize(data)
            + GetMaxPathsSize(exists)
            + (hasPersistent ?
                GetMaxPathsSize(persistent)
                    + GetMaxPathsSize(recursivePersistent)
                : 0
            )
        );
        int size = LengthSize;

        _RequestBytes.Span.CopyTo(buffer.Slice(size));
        size += RequestSize;

        (hasPersistent ? _OperationWithPersistentBytes : _OperationBytes).Span.CopyTo(buffer.Slice(size));
        size += OperationSize;

        size += ZooKeeperTransactions.Write(buffer.Slice(size), lastTransaction);

        size += WritePaths(buffer.Slice(size), data);
        size += WritePaths(buffer.Slice(size), exists);
        size += WritePaths(buffer.Slice(size), children);
        if (hasPersistent)
        {
            size += WritePaths(buffer.Slice(size), persistent);
            size += WritePaths(buffer.Slice(size), recursivePersistent);
        }

        ZooKeeperTransactions.Write(buffer, size - LengthSize);
        writer.Advance(size);
    }

    private static int GetMaxPathsSize(IReadOnlySet<ZooKeeperPath> paths)
    {
        int size = LengthSize;
        foreach (var path in paths)
            size += path.GetMaxBufferSize();
        return size;
    }

    private static int WritePaths(Span<byte> buffer, IReadOnlySet<ZooKeeperPath> paths)
    {
        int size = ZooKeeperTransactions.Write(buffer, paths.Count);
        foreach (var path in paths)
            size += path.Write(buffer.Slice(size));
        return size;
    }

    public static Result Read(in ZooKeeperReadContext response)
    {
        response.ThrowIfError();
        return new(response.Transaction);
    }

    public readonly record struct Result(long Transaction);
}

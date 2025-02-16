// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.IZooKeeper;
using static AdHoc.ZooKeeper.Abstractions.Operations;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperEvent;

namespace AdHoc.ZooKeeper.Abstractions;
public readonly record struct ZooKeeperEvent(
    long Trigger,
    ZooKeeperStatus Status,
    Types Type,
    States State,
    ZooKeeperPath Path
)
{
    public enum Types : int
    {
        None = -1,
        NodeCreated = 1,
        NodeDeleted = 2,
        DataChanged = 3,
        ChildrenChanged = 4,
        DataWatchRemoved = 5,
        ChildWatchRemove = 6,
        PersistentWatchRemoved = 7,
    }

    public const int NoRequest = -1;

    private const int _EventHeaderSize = RequestSize + TransactionSize + StatusSize + Int32Size + Int32Size;
    private static readonly ReadOnlyMemory<byte> _NoRequest = new byte[] { 255, 255, 255, 255 };

    public static ZooKeeperEvent Read(ReadOnlySpan<byte> source, out int size)
    {
        if (!source.Slice(0, RequestSize).SequenceEqual(_NoRequest.Span))
            throw new ArgumentException($"Response hasn't started with event request identifier");
        var path = ZooKeeperPath.Read(source.Slice(_EventHeaderSize), out size);
        size += _EventHeaderSize;
        return new(
            ReadInt64(source.Slice(RequestSize)),
            (ZooKeeperStatus)ReadInt32(source.Slice(RequestSize + TransactionSize)),
            (Types)ReadInt32(source.Slice(RequestSize + TransactionSize + Int32Size)),
            (States)ReadInt32(source.Slice(RequestSize + TransactionSize + Int32Size + Int32Size)),
            path
        );
    }
}

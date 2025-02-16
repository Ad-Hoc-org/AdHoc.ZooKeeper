// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.Operations;

namespace AdHoc.ZooKeeper.Abstractions;
public readonly record struct ZooKeeperNode(
    ZooKeeperPath Path,
    long Creator,
    long LastModifier,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    int Version,
    int ChildVersion,
    int AccessControlListVersion,
    long EphemeralOwner,
    int Length,
    int NumberOfChildren,
    long ChildrenLastModifier
)
{
    public static ZooKeeperNode Read(ReadOnlySpan<byte> source, ZooKeeperPath path, out int size)
    {
        size = SessionSize + SessionSize
            + TimestampSize + TimestampSize
            + VersionSize + VersionSize + VersionSize
            + SessionSize
            + LengthSize + Int32Size
            + SessionSize;
        return new ZooKeeperNode(
            path,
            ReadInt64(source),
            ReadInt64(source.Slice(SessionSize)),
            ReadTimestamp(source.Slice(SessionSize + SessionSize)),
            ReadTimestamp(source.Slice(SessionSize + SessionSize + TimestampSize)),
            ReadInt32(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize)),
            ReadInt32(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize)),
            ReadInt32(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize + VersionSize)),
            ReadInt64(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize + VersionSize + VersionSize)),
            ReadInt32(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize + VersionSize + VersionSize + SessionSize)),
            ReadInt32(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize + VersionSize + VersionSize + SessionSize + LengthSize)),
            ReadInt64(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize + VersionSize + VersionSize + SessionSize + LengthSize + Int32Size))
        );
    }
}

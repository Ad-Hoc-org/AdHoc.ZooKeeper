// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public readonly record struct ZooKeeperNode(
    long Creator = -1,
    long LastModifier = -1,
    DateTimeOffset CreatedAt = default,
    DateTimeOffset ModifiedAt = default,
    int Version = -1,
    int ChildVersion = -1,
    int AccessControlListVersion = -1,
    long EphemeralOwner = -1,
    int Length = -1,
    int ChildrenCount = -1,
    long ChildrenLastModifier = -1
)
{
    public static ZooKeeperNode Read(ReadOnlySpan<byte> source, out int size)
    {
        size = SessionSize + SessionSize
            + TimestampSize + TimestampSize
            + VersionSize + VersionSize + VersionSize
            + SessionSize
            + LengthSize + Int32Size
            + SessionSize;
        return new ZooKeeperNode(
            Creator: ReadInt64(source),
            LastModifier: ReadInt64(source.Slice(SessionSize)),
            CreatedAt: ReadTimestamp(source.Slice(SessionSize + SessionSize)),
            ModifiedAt: ReadTimestamp(source.Slice(SessionSize + SessionSize + TimestampSize)),
            Version: ReadInt32(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize)),
            ChildVersion: ReadInt32(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize)),
            AccessControlListVersion: ReadInt32(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize + VersionSize)),
            EphemeralOwner: ReadInt64(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize + VersionSize + VersionSize)),
            Length: ReadInt32(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize + VersionSize + VersionSize + SessionSize)),
            ChildrenCount: ReadInt32(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize + VersionSize + VersionSize + SessionSize + LengthSize)),
            ChildrenLastModifier: ReadInt64(source.Slice(SessionSize + SessionSize + TimestampSize + TimestampSize + VersionSize + VersionSize + VersionSize + SessionSize + LengthSize + Int32Size))
        );
    }
}

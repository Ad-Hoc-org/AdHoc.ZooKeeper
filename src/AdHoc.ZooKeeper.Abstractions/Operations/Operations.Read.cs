// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using System.Text;

namespace AdHoc.ZooKeeper.Abstractions;
public static partial class Operations
{
    public static int ReadInt32(ReadOnlySpan<byte> source) =>
        BinaryPrimitives.ReadInt32BigEndian(source);

    public static long ReadInt64(ReadOnlySpan<byte> source) =>
        BinaryPrimitives.ReadInt64BigEndian(source);

    public static ReadOnlySpan<byte> ReadBuffer(ReadOnlySpan<byte> source, out int size)
    {
        int length = ReadInt32(source);
        size = length + LengthSize;
        return source.Slice(LengthSize, length);
    }

    public static TimeSpan ReadTimeSpan(ReadOnlySpan<byte> source) =>
        TimeSpan.FromMilliseconds(ReadInt32(source));

    public static DateTimeOffset ReadTimestamp(ReadOnlySpan<byte> source) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ReadInt64(source));
}

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using System.Text;

namespace AdHoc.ZooKeeper.Abstractions;
public static partial class ZooKeeperTransactions
{
    public static int Write(Span<byte> destination, bool value)
    {
        destination[0] = value ? (byte)1 : (byte)0;
        return BooleanSize;
    }

    public static int Write(Span<byte> destination, int value)
    {
        BinaryPrimitives.WriteInt32BigEndian(destination, value);
        return Int32Size;
    }

    public static int Write(Span<byte> destination, long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(destination, value);
        return Int64Size;
    }

    public static int Write(Span<byte> destination, string value)
    {
        Write(destination, value.Length);
        return LengthSize + Encoding.UTF8.GetBytes(value, destination.Slice(LengthSize));
    }

    public static int Write(Span<byte> destination, ReadOnlySpan<byte> buffer)
    {
        Write(destination, buffer.Length);
        buffer.CopyTo(destination.Slice(LengthSize));
        return LengthSize + buffer.Length;
    }

    public static int Write(Span<byte> destination, TimeSpan value) =>
        Write(destination, (int)value.TotalMilliseconds);

}

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using static AdHoc.ZooKeeper.Abstractions.Operations;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;

namespace AdHoc.ZooKeeper.Abstractions;
public static class AddAuthenticationOperation
{
    public const int Request = -4;

    private static readonly ReadOnlyMemory<byte> _RequestBytes = new byte[] { 255, 255, 255, 252 };
    private static readonly ReadOnlyMemory<byte> _Operation = new byte[] { 0, 0, 0, 100 };

    public static void Write(
        IBufferWriter<byte> writer,
        Authentication authentication
    )
    {
        var buffer = writer.GetSpan(RequestHeaderSize
            + Int32Size
            + LengthSize + authentication.Scheme.Length
            + LengthSize + authentication.Data.Length
        );
        int size = LengthSize;

        _RequestBytes.Span.CopyTo(buffer.Slice(size));
        size += RequestSize;

        _Operation.Span.CopyTo(buffer.Slice(size));
        size += OperationSize;

        size += Operations.Write(buffer.Slice(size), 0); // type

        size += Operations.Write(buffer.Slice(size), authentication.Scheme);

        size += Operations.Write(buffer.Slice(size), authentication.Data.Span);

        Operations.Write(buffer, size - LengthSize);
        writer.Advance(size);
    }

    public static Result Read(in ZooKeeperResponse response)
    {
        response.ThrowIfError();
        return new(response.Transaction);
    }

    public readonly record struct Result(long Transaction);
}

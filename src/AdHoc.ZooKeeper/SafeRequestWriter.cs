// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Buffers.Binary;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper;
internal class SafeRequestWriter
    : IBufferWriter<byte>
{
    private readonly IBufferWriter<byte> _writer;

    public int Length { get; private set; } = -1;
    public int Request { get; private set; }
    public int Size { get; private set; }

    public bool IsCompleted => Length == Size;
    public bool IsPing { get; private set; }


    internal SafeRequestWriter(IBufferWriter<byte> writer) => _writer = writer;


    public void Advance(int count)
    {
        Size += count;

        if (Length == -1)
        {
            if (Size < RequestHeaderSize)
                return;

            var header = _writer.GetSpan(RequestHeaderSize);
            Length = BinaryPrimitives.ReadInt32BigEndian(header);
            if (Size > Length + LengthSize)
                throw ZooKeeperException.CreateInvalidRequestSize(Length, Size);

            if (Length == RequestHeaderSize
                && header.Slice(LengthSize + RequestSize).SequenceEqual(PingOperation.OperationBytes.Span)
            )
            {
                IsPing = true;
                Request = PingOperation.Request;
                return; // don't flush ping
            }

            Request = BinaryPrimitives.ReadInt32BigEndian(header.Slice(LengthSize));
            _writer.Advance(Size);
            return;
        }

        if (Size > Length + LengthSize)
            throw ZooKeeperException.CreateInvalidRequestSize(Length, Size);
        _writer.Advance(count);
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (Length == -1)
            return _writer.GetMemory(sizeHint + Size).Slice(Size);
        return _writer.GetMemory(sizeHint);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        if (Length == -1)
            return _writer.GetSpan(sizeHint + Size).Slice(Size);
        return _writer.GetSpan(sizeHint);
    }

}

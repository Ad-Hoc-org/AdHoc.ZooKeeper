// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;

namespace AdHoc.ZooKeeper.Abstractions;
public static partial class Operations
{
    public const int ProtocolVersionSize = Int32Size;
    public const int TimeoutSize = Int32Size;
    public const int SessionSize = Int64Size;
    public const int ReadOnlySize = BooleanSize;
    private const int NewSessionSize = LengthSize + ProtocolVersionSize + TransactionSize + TimeoutSize + SessionSize + LengthSize + ReadOnlySize;

    public const int DefaultPasswordSize = 16;
    public const int DefaultSessionResponseSize = LengthSize + RequestSize + TimeoutSize + SessionSize + LengthSize + DefaultPasswordSize + ReadOnlySize;

    public static string SessionToString(ReadOnlySpan<byte> session) =>
#if NET9_0_OR_GREATER
        "0x" + Convert.ToHexStringLower(session);
#else
        "0x" + Convert.ToHexString(session).ToLower();
#endif


    public static void WriteNewSession(
        IBufferWriter<byte> writer,
        TimeSpan sessionTimeout,
        bool readOnly
    )
    {
        var buffer = writer.GetSpan(NewSessionSize);

        // 0, 4 length
        buffer.Slice(0, 3).Clear();
        buffer[3] = NewSessionSize - LengthSize;
        // 4, 4 protocol version
        // 12, 4 last zxid
        buffer.Slice(LengthSize, RequestSize + TransactionSize).Clear();
        // 16, 4 timeout
        Write(buffer.Slice(16, TimeoutSize), (int)sessionTimeout.TotalMilliseconds);
        // 20, 8 session
        // 28, 4 password size
        buffer.Slice(20, SessionSize + LengthSize).Clear();
        // 33 readonly
        buffer[32] = (byte)(readOnly ? 1 : 0);

        writer.Advance(NewSessionSize);
    }

    public static void WriteReconnectSession(
        IBufferWriter<byte> writer,
        ZooKeeperSession session,
        long lastTransaction
    )
    {
        var buffer = writer.GetSpan(
            LengthSize
            + ProtocolVersionSize
            + TransactionSize
            + TimeoutSize
            + SessionSize
            + LengthSize + session.Password.Length
            + ReadOnlySize
        );
        int size = LengthSize;

        buffer.Slice(size, ProtocolVersionSize).Clear();
        size += ProtocolVersionSize;

        size += Write(buffer.Slice(size), lastTransaction);

        size += Write(buffer.Slice(size), (int)session.SessionTimeout.TotalMilliseconds);

        session.Session.Span.CopyTo(buffer.Slice(size));
        size += SessionSize;

        size += Write(buffer.Slice(size), session.Password.Span);

        buffer[size++] = (byte)(session.IsReadOnly ? 1 : 0);

        Write(buffer, size - LengthSize);
        writer.Advance(size);
    }

    public static ZooKeeperSession ReadSession(in ReadOnlySpan<byte> data) => new(
        data.Slice(ProtocolVersionSize + TimeoutSize, SessionSize).ToArray(),
        data.Slice(
            ProtocolVersionSize + TimeoutSize + SessionSize + LengthSize,
            ReadInt32(data.Slice(ProtocolVersionSize + TimeoutSize + SessionSize))
        ).ToArray(),
        TimeSpan.FromMilliseconds(ReadInt32(data.Slice(ProtocolVersionSize))),
        data[data.Length - 1] == 1
    );

}

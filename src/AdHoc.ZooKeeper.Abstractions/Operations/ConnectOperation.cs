// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using static AdHoc.ZooKeeper.Abstractions.Operations;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record ConnectOperation
{
    public const int TimeoutSize = Int32Size;
    public const int SessionSize = Int64Size;
    public const int ReadOnlySize = BooleanSize;

    private const int _ProtocolVersionSize = Int32Size;
    private const int _NewSessionSize = LengthSize + _ProtocolVersionSize + TransactionSize + TimeoutSize + SessionSize + LengthSize + ReadOnlySize;

    public const int DefaultPasswordSize = 16;
    public const int DefaultSessionResponseSize = LengthSize + RequestSize + TimeoutSize + SessionSize + LengthSize + DefaultPasswordSize + ReadOnlySize;


    private static ReadOnlyMemory<byte> _NewSession = new byte[SessionSize];


    public long LastTransaction { get; }

    public TimeSpan SessionTimeout { get; }

    public ReadOnlyMemory<byte> Session { get; }

    public ReadOnlyMemory<byte> Password { get; }

    public bool ReadOnly { get; }


    private ConnectOperation(
        TimeSpan sessionTimeout,
        bool readOnly,
        long lastTransaction,
        ReadOnlyMemory<byte> session,
        ReadOnlyMemory<byte> password
    )
    {
        SessionTimeout = sessionTimeout;
        ReadOnly = readOnly;
        LastTransaction = lastTransaction;
        Session = session;
        Password = password;
    }


    public void WriteRequest(IBufferWriter<byte> writer)
    {
        var buffer = writer.GetSpan(LengthSize + _ProtocolVersionSize + TransactionSize + TimeoutSize
            + SessionSize + LengthSize + Password.Length + ReadOnlySize);
        int size = LengthSize;

        buffer.Slice(size, _ProtocolVersionSize).Clear();
        size += _ProtocolVersionSize;

        size += Write(buffer.Slice(size), LastTransaction);

        size += Write(buffer.Slice(size), (int)SessionTimeout.TotalMilliseconds);

        Session.Span.CopyTo(buffer.Slice(size));
        size += SessionSize;

        size += Write(buffer.Slice(size), Password.Span);

        buffer[size++] = (byte)(ReadOnly ? 1 : 0);

        Write(buffer, size - LengthSize);
        writer.Advance(size);
    }

    public Result ReadResponse(in ReadOnlySpan<byte> data) => new(
        TimeSpan.FromMilliseconds(ReadInt32(data.Slice(_ProtocolVersionSize))),
        data.Slice(_ProtocolVersionSize + TimeoutSize, SessionSize).ToArray(),
        data.Slice(
            _ProtocolVersionSize + TimeoutSize + SessionSize + LengthSize,
            ReadInt32(data.Slice(_ProtocolVersionSize + TimeoutSize + SessionSize))
        ).ToArray()
    );


    public static ConnectOperation NewSession(TimeSpan sessionTimeout, bool readOnly) =>
        new(sessionTimeout, readOnly, 0, _NewSession, ReadOnlyMemory<byte>.Empty);

    public static ConnectOperation Reconnect(
        TimeSpan sessionTimeout,
        bool readOnly,
        long lastTransaction,
        ReadOnlyMemory<byte> session,
        ReadOnlyMemory<byte> password
    ) => new(sessionTimeout, readOnly, lastTransaction, session, password);


    public readonly record struct Result(
        TimeSpan SessionTimeout,
        ReadOnlyMemory<byte> Session,
        ReadOnlyMemory<byte> Password
    );
}

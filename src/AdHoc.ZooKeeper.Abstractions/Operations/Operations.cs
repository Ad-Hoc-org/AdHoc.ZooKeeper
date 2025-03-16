// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public static partial class Operations
{
    public const int BooleanSize = 1;
    public const int Int32Size = 4;
    public const int Int64Size = 8;

    public const int LengthSize = Int32Size;
    public const int RequestSize = Int32Size;
    public const int OperationSize = Int32Size;
    public const int RequestHeaderSize = LengthSize + RequestSize + OperationSize;

    public const int TransactionSize = Int64Size;
    public const int StatusSize = Int32Size;
    public const int MinimalResponseLength = RequestSize + TransactionSize + StatusSize;

    public const int VersionSize = Int32Size;
    public const int NoVersion = -1;

    public const int TimestampSize = Int64Size;

    public const int ProtocolVersionSize = Int32Size;
    public const int TimeoutSize = Int32Size;
    public const int SessionSize = Int64Size;
    public const int ReadOnlySize = BooleanSize;


    public static void ValidateRequest(ReadOnlySpan<byte> request)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Length, LengthSize + RequestSize + OperationSize);
        var length = ReadInt32(request);
        ArgumentOutOfRangeException.ThrowIfNotEqual(length, request.Length - LengthSize);
    }

    public static int GetRequest(ZooKeeperOperation operation, ref int previousRequest)
    {
        if (operation == ZooKeeperOperation.Ping)
            return PingOperation.Request;
        if (operation == ZooKeeperOperation.Authentication)
            return AddAuthenticationOperation.Request;

        int oldValue, newValue;
        do
        {
            oldValue = previousRequest;
            newValue = oldValue + 1;
            if (newValue < 0)
                newValue = 1;
        } while (Interlocked.CompareExchange(ref previousRequest, newValue, oldValue) != oldValue);
        return newValue;
    }

    public static string SessionToString(ReadOnlySpan<byte> session) =>
#if NET9_0_OR_GREATER
        "0x" + Convert.ToHexStringLower(session);
#else
        "0x" + Convert.ToHexString(session).ToLower();
#endif
}

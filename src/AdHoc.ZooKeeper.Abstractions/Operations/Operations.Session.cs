// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public static partial class Operations
{
    public const int ProtocolVersionSize = Int32Size;
    public const int TimeoutSize = Int32Size;
    public const int SessionSize = Int64Size;
    public const int ReadOnlySize = BooleanSize;
    public const int StartSessionSize = LengthSize + ProtocolVersionSize + TransactionSize + TimeoutSize + SessionSize + LengthSize + ReadOnlySize;

    public const int DefaultPasswordSize = 16;
    public const int DefaultSessionResponseSize = LengthSize + RequestSize + TimeoutSize + SessionSize + LengthSize + DefaultPasswordSize + ReadOnlySize;

    public static string SessionToString(ReadOnlySpan<byte> session) =>
#if NET9_0_OR_GREATER
        "0x" + Convert.ToHexStringLower(session);
#else
        "0x" + Convert.ToHexString(session).ToLower();
#endif
}

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public readonly record struct ZooKeeperSession(
    ReadOnlyMemory<byte> Session,
    ReadOnlyMemory<byte> Password,
    TimeSpan SessionTimeout,
    bool IsReadOnly
)
{
    public override string ToString() =>
        $$"""{{nameof(ZooKeeperSession)}} { Session = {{ToString(Session.Span)}}}""";

    public static string ToString(ReadOnlySpan<byte> session) =>
#if NET9_0_OR_GREATER
        "0x" + Convert.ToHexStringLower(session);
#else
        "0x" + Convert.ToHexString(session).ToLower();
#endif
}

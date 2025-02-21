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
        $$"""{{nameof(ZooKeeperSession)}} { Session = {{Operations.SessionToString(Session.Span)}}}""";
}

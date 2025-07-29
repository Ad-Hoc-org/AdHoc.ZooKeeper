// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;

[Serializable]
public class ConnectionLostException : ConnectionException
{
    public ZooKeeperSession Session { get; init; }
    public long LastTransaction { get; init; }
    public long LastInteractionTimestamp { get; init; }

    public ConnectionLostException() { }
    public ConnectionLostException(string? message) : base(message) { }
    public ConnectionLostException(string? message, Exception? inner) : base(message, inner) { }
}

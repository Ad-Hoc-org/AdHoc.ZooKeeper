// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;

namespace AdHoc.ZooKeeper.Abstractions;

[Serializable]
public class ConnectionException : ZooKeeperException
{
    public Host Host { get; init; }

    public ConnectionException() { }
    public ConnectionException(string? message) : base(message) { }
    public ConnectionException(string? message, Exception? inner) : base(message, inner) { }
}

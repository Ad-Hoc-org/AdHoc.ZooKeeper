// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;

[Serializable]
public class ResponseException : ZooKeeperException
{

    public ZooKeeperStatus Status { get; }

    public ResponseException(ZooKeeperStatus status) => Status = status;
    public ResponseException(ZooKeeperStatus status, string message) : base(message) => Status = status;
    public ResponseException(ZooKeeperStatus status, string message, Exception inner) : base(message, inner) => Status = status;
}

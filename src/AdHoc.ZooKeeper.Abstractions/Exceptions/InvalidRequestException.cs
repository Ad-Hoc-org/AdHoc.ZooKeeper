// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;

[Serializable]
public class InvalidRequestException : ZooKeeperException
{
    public InvalidRequestException() { }
    public InvalidRequestException(string message) : base(message) { }
    public InvalidRequestException(string message, Exception inner) : base(message, inner) { }
}

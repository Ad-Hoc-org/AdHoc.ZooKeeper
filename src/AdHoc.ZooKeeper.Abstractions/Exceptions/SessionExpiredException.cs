// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;

[Serializable]
public class SessionExpiredException : ConnectionLostException
{
    public SessionExpiredException() { }
    public SessionExpiredException(string? message) : base(message) { }
    public SessionExpiredException(string? message, Exception? inner) : base(message, inner) { }
}

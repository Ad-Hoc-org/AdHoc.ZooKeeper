// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;

namespace AdHoc.ZooKeeper.Abstractions;

[Serializable]
public class ZooKeeperException
    : Exception
{
    public ZooKeeperException() { }
    public ZooKeeperException(string? message) : base(message) { }
    public ZooKeeperException(string? message, Exception? inner) : base(message, inner) { }


    public static ConnectionException CreateNoConnection(Host host, Exception? innerException = null) =>
        new($"Couldn't connect to {host}.", innerException)
        {
            Host = host
        };

    public static ConnectionLostException CreateLostConnection(
        Host host,
        ZooKeeperSession session,
        long lastTransaction,
        long lastInteractionTimestamp,
        Exception? innerException = null
    ) => new(
        $"Lost connection {ZooKeeperSession.ToString(session.Session.Span)} to {host}.",
        innerException
    )
    {
        Host = host,
        Session = session,
        LastTransaction = lastTransaction,
        LastInteractionTimestamp = lastInteractionTimestamp
    };

    public static SessionExpiredException CreateSessionExpired(
        Host host,
        ZooKeeperSession session,
        long lastTransaction,
        long lastInteractionTimestamp,
        Exception? innerException = null
    ) => new(
        $"Session {ZooKeeperSession.ToString(session.Session.Span)} expired.",
        innerException
    )
    {
        Host = host,
        Session = session,
        LastTransaction = lastTransaction,
        LastInteractionTimestamp = lastInteractionTimestamp
    };

    public static InvalidRequestException CreateInvalidRequestSize(int length, int size) =>
        new($"Request length is {length} but {size} was written.");

    public static ResponseException CreateResponseError(ZooKeeperStatus status) =>
        new(status, $"Response indicate an error: {status}");
}

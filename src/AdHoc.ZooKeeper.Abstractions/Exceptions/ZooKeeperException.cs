// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.Operations;
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
        ReadOnlyMemory<byte> session,
        ReadOnlyMemory<byte> password,
        long lastTransaction,
        long lastInteractionTimestamp,
        Exception? innerException = null
    ) => new(
        $"Lost connection {SessionToString(session.Span)} to {host}.",
        innerException
    )
    {
        Host = host,
        Session = session,
        Password = password,
        LastTransaction = lastTransaction,
        LastInteractionTimestamp = lastInteractionTimestamp
    };

    public static InvalidRequestException CreateInvalidRequestSize(int length, int size) =>
        new($"Request length is {length} but {size} was written.");

    public static ResponseException CreateResponseError(ZooKeeperStatus status) =>
        new(status, $"Response indicate an error: {status}");
}

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using static AdHoc.ZooKeeper.Abstractions.PingTransaction;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record PingTransaction
    : IZooKeeperTransaction<Response>
{
    public const int Request = -2;

    public ZooKeeperOperations Operation => ZooKeeperOperations.Ping;


    internal PingTransaction() { }


    public int GetMaxRequestSize(in ZooKeeperPath root) => 0;
    public int WriteRequest(in ZooKeeperWriteContext context) => 0;

    public Response ReadResponse(in ZooKeeperReadContext context)
    {
        Debug.Assert(context.Request == Request);
        return new(
            context.Transaction,
            context.Status
        );
    }


    public readonly record struct Response(
        long Transaction,
        ZooKeeperStatus Status
    ) : IZooKeeperResponse;
}

public static partial class ZooKeeperTransactions
{
    public static PingTransaction Ping { get; } = new();

    public static Task<Response> PingAsync(
        this IZooKeeper zooKeeper,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Ping, cancellationToken);
}

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using static AdHoc.ZooKeeper.Abstractions.Operations;
using static AdHoc.ZooKeeper.Abstractions.PingOperation;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record PingOperation
    : IZooKeeperOperation<Result>
{
    public const int Request = -2;


    public static readonly ReadOnlyMemory<byte> Bytes = new byte[] {
        0, 0, 0, 8, // Length
        255,255, 255, 254, // XID
        0, 0, 0, 11 // OpCode
    };

    public static ReadOnlyMemory<byte> RequestBytes => Bytes.Slice(LengthSize, RequestSize);
    public static ReadOnlyMemory<byte> OperationBytes => Bytes.Slice(LengthSize + RequestSize, OperationSize);


    internal PingOperation() { }


    public void WriteRequest(in ZooKeeperContext context) =>
        context.Writer.Write(Bytes.Span);

    public Result ReadResponse(in ZooKeeperResponse response, IZooKeeperWatcher? watcher) =>
        new(response.Transaction, response.Status);


    public readonly record struct Result(
        long Transaction,
        ZooKeeperStatus Status
    );
}

public static partial class Operations
{
    public static PingOperation Ping { get; } = new PingOperation();

    public static Task<Result> PingAsync(
        this IZooKeeper zooKeeper,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Ping, cancellationToken);
}

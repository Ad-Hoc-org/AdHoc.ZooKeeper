// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;
using static AdHoc.ZooKeeper.Abstractions.SetDataOperation;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record SetDataOperation
    : IZooKeeperTransaction<Result>
{
    private static readonly ReadOnlyMemory<byte> _Operation = new byte[] { 0, 0, 0, 5 };

    public ZooKeeperPath Path { get; }
    public ReadOnlyMemory<byte> Data { get; }
    public int Version { get; }

    private SetDataOperation(ZooKeeperPath path, ReadOnlyMemory<byte> data, int version)
    {
        path.ThrowIfInvalid();
        Path = path;
        Data = data;
        Version = version;
    }

    public void WriteRequest(in ZooKeeperWriteContext context)
    {
        var writer = context.Writer;
        var buffer = writer.GetSpan(RequestHeaderSize + Path.GetMaxSize(context.Root) + Data.Length);
        int size = LengthSize;

        size += Write(buffer.Slice(size), context.GetRequest(ZooKeeperOperations.SetData));

        _Operation.Span.CopyTo(buffer.Slice(size));
        size += OperationSize;

        size += Path.Write(buffer.Slice(size), context.Root);

        size += Write(buffer.Slice(size), Data.Span);

        size += Write(buffer.Slice(size), Version);

        Write(buffer, size - LengthSize);
        writer.Advance(size);
    }

    public Result ReadResponse(in ZooKeeperReadContext response, IZooKeeperWatcher? watcher)
    {
        if (response.Status == ZooKeeperStatus.NoNode)
            return new(response.Transaction, default);

        response.ThrowIfError();

        var node = ZooKeeperNode.Read(response.Data, (response.Root + Path).Absolute, out _);
        return new(response.Transaction, node);
    }

    public static SetDataOperation Create(ZooKeeperPath path, ReadOnlyMemory<byte> data, int version = NoVersion) =>
        new(path, data, version);

    public readonly record struct Result(
        long Transaction,
        ZooKeeperNode? Node
    );
}

public static partial class ZooKeeperTransactions
{
    public static Task<Result> SetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        int version,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path, data, version), cancellationToken);

    public static Task<Result> SetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path, data), cancellationToken);
}

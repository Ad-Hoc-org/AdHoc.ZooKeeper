// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.DeleteOperation;
using static AdHoc.ZooKeeper.Abstractions.Operations;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record DeleteOperation
    : IZooKeeperOperation<Result>
{
    private static readonly ReadOnlyMemory<byte> _Operation = new byte[] { 0, 0, 0, 2 };

    public ZooKeeperPath Path { get; }

    public int Version { get; }

    private DeleteOperation(ZooKeeperPath path, int version)
    {
        path.Validate();
        Path = path;
        Version = version;
    }

    public void WriteRequest(in ZooKeeperContext context)
    {
        var writer = context.Writer;
        var buffer = writer.GetSpan(RequestHeaderSize + Path.GetMaxSize(context.Root) + VersionSize);
        int size = LengthSize;

        size += Write(buffer.Slice(size), context.GetRequest(ZooKeeperOperation.Delete));

        _Operation.Span.CopyTo(buffer.Slice(size));
        size += OperationSize;

        size += Path.Write(buffer.Slice(size), context.Root);

        size += Write(buffer.Slice(size), Version);

        Write(buffer, size - LengthSize);
        writer.Advance(size);
    }

    public Result ReadResponse(in ZooKeeperResponse response, IZooKeeperWatcher? watcher)
    {
        if (response.Status == ZooKeeperStatus.NoNode)
            return new(response.Transaction, false, false);

        if (response.Status == ZooKeeperStatus.NotEmpty)
            return new(response.Transaction, true, false);

        response.ThrowIfError();

        return new(response.Transaction, true, true);
    }


    public static DeleteOperation Create(ZooKeeperPath path, int version = -1) =>
        new(path, version);


    public readonly record struct Result(
        long Transaction,
        bool Existed,
        bool Deleted
    );
}

public static partial class Operations
{

    public static Task<Result> DeleteAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        int version,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path, version), cancellationToken);

    public static Task<Result> DeleteAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path), cancellationToken);

}

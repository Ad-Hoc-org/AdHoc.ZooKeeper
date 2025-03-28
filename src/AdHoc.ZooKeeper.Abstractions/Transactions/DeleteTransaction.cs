// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.DeleteTransaction;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record DeleteTransaction
    : IZooKeeperTransaction<Response>
{
    private static readonly ReadOnlyMemory<byte> _Operation = new byte[] { 0, 0, 0, 2 };

    public ZooKeeperPath Path { get; }

    public int Version { get; }

    public ZooKeeperOperations Operation => throw new NotImplementedException();

    private DeleteTransaction(ZooKeeperPath path, int version)
    {
        path.ThrowIfInvalid();
        Path = path;
        Version = version;
    }

    public void WriteRequest(in ZooKeeperWriteContext context)
    {
        var writer = context.Writer;
        var buffer = writer.GetSpan(RequestHeaderSize + Path.GetMaxSize(context.Root) + VersionSize);
        int size = LengthSize;

        size += Write(buffer.Slice(size), context.GetRequest(ZooKeeperOperations.Delete));

        _Operation.Span.CopyTo(buffer.Slice(size));
        size += OperationSize;

        size += Path.Write(buffer.Slice(size), context.Root);

        size += Write(buffer.Slice(size), Version);

        Write(buffer, size - LengthSize);
        writer.Advance(size);
    }

    public Response ReadResponse(in ZooKeeperReadContext context)
    {
        if (context.Status == ZooKeeperStatus.NoNode)
            return new(context.Transaction, false, false);
        if (context.Status == ZooKeeperStatus.NotEmpty)
            return new(context.Transaction, false, true);

        context.Status.ThrowIfError();

        return new(context.Transaction, true, false);
    }


    public static DeleteTransaction Create(ZooKeeperPath path, int version = -1) =>
        new(path, version);
    public int GetMaxRequestSize(in ZooKeeperPath root) => throw new NotImplementedException();
    int IZooKeeperTransaction<Response>.WriteRequest(in ZooKeeperWriteContext context) => throw new NotImplementedException();
    public Response ReadResponse(in ZooKeeperReadContext context) => throw new NotImplementedException();

    // TODO check for path
    public readonly record struct Response(
        long Transaction,
        bool Deleted,
        bool NotEmpty
    ) : IZooKeeperResponse;
}

public static partial class ZooKeeperTransactions
{

    public static Task<Response> DeleteAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        int version,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path, version), cancellationToken);

    public static Task<Response> DeleteAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(Create(path), cancellationToken);

}

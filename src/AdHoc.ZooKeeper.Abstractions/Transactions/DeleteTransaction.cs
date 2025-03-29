// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.DeleteTransaction;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record DeleteTransaction
    : IZooKeeperTransaction<Response>
{

    public ZooKeeperOperations Operation => ZooKeeperOperations.Delete;


    public ZooKeeperPath Path { get; }

    public int Version { get; }


    private DeleteTransaction(ZooKeeperPath path, int version)
    {
        path.ThrowIfEmptyOrInvalid();
        Path = path;
        Version = version;
    }

    public static DeleteTransaction Create(ZooKeeperPath path, int version = NoVersion) =>
        new(path, version);


    public int GetMaxRequestSize(in ZooKeeperPath root) =>
        Path.GetMaxBufferSize(root) + VersionSize;


    public int WriteRequest(in ZooKeeperWriteContext context)
    {
        var buffer = context.Buffer;

        var path = Path.Normalize(context.Root);
        int size = path.Write(buffer);

        size += Write(buffer.Slice(size), Version);
        return size;
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

    public readonly record struct Response(
        long Transaction,
        bool Deleted,
        bool NotEmpty
    ) : IZooKeeperResponse;
}

public static partial class ZooKeeperTransactions
{
    public static Task<Response> DeleteAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        int version,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, version), cancellationToken);

    public static Task<Response> DeleteAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path), cancellationToken);
}

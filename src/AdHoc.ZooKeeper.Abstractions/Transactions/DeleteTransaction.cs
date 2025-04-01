// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
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

    public Response ReadResponse(in ZooKeeperReadContext context, out int size)
    {
        Debug.Assert(context.Operation == Operation);

        size = 0;
        if (context.Status == ZooKeeperStatus.NoNode)
            return new(context.Transaction, Path.Normalize(context.Root), false, false);
        if (context.Status == ZooKeeperStatus.NotEmpty)
            return new(context.Transaction, Path.Normalize(context.Root), false, true);

        context.Status.ThrowIfError();

        return new(context.Transaction, Path.Normalize(context.Root), true, false);
    }

    public readonly record struct Response(
        long Transaction,
        ZooKeeperPath Path,
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
        zooKeeper.ExecuteAsync(DeleteTransaction.Create(path, version), cancellationToken);

    public static Task<Response> DeleteAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(DeleteTransaction.Create(path), cancellationToken);

    public static ZooKeeperTransaction.Builder Delete(
        this ZooKeeperTransaction.Builder transaction,
        ZooKeeperPath path,
        int version
    ) =>
        transaction.AddTransaction(DeleteTransaction.Create(path, version));

    public static ZooKeeperTransaction.Builder Delete(
        this ZooKeeperTransaction.Builder transaction,
        ZooKeeperPath path
    ) =>
        transaction.AddTransaction(DeleteTransaction.Create(path));
}

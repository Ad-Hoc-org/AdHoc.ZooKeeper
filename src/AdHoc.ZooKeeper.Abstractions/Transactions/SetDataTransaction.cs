// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using static AdHoc.ZooKeeper.Abstractions.SetDataTransaction;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record SetDataTransaction
    : IZooKeeperTransaction<Response>
{
    public ZooKeeperOperations Operation => ZooKeeperOperations.SetData;


    public ZooKeeperPath Path { get; }

    public ReadOnlyMemory<byte> Data { get; }

    public int Version { get; }


    private SetDataTransaction(ZooKeeperPath path, ReadOnlyMemory<byte> data, int version)
    {
        path.ThrowIfInvalid();
        Path = path;
        Data = data;
        Version = version;
    }

    public static SetDataTransaction Create(ZooKeeperPath path, ReadOnlyMemory<byte> data, int version = NoVersion) =>
        new(path, data, version);


    public int GetMaxRequestSize(in ZooKeeperPath root) =>
        Path.GetMaxBufferSize(root) + Data.Length + Int32Size;

    public int WriteRequest(in ZooKeeperWriteContext context)
    {
        var buffer = context.Buffer;

        var path = Path.Normalize(context.Root);
        int size = path.Write(buffer);

        size += Write(buffer.Slice(size), Data.Span);

        size += Write(buffer.Slice(size), Version);

        return size;
    }

    public Response ReadResponse(in ZooKeeperReadContext context, out int size)
    {
        Debug.Assert(context.Operation == Operation);

        size = 0;
        if (context.Status == ZooKeeperStatus.NoNode)
            return new(context.Transaction, Path.Normalize(context.Root), default);

        context.Status.ThrowIfError();

        return new(context.Transaction, Path.Normalize(context.Root), ZooKeeperNode.Read(context.Data, out _));
    }


    public readonly record struct Response(
        long Transaction,
        ZooKeeperPath Path,
        ZooKeeperNode? Node
    ) : IZooKeeperResponse
    {
        public bool Updated => Node is not null;
    }
}

public static partial class ZooKeeperTransactions
{
    public static Task<Response> SetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        int version,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(SetDataTransaction.Create(path, data, version), cancellationToken);

    public static Task<Response> SetDataAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(SetDataTransaction.Create(path, data), cancellationToken);


    public static ZooKeeperTransaction.Builder SetData(
        this ZooKeeperTransaction.Builder transaction,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        int version
    ) =>
        transaction.AddTransaction(SetDataTransaction.Create(path, data, version));

    public static ZooKeeperTransaction.Builder SetData(
        this ZooKeeperTransaction.Builder transaction,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data
    ) =>
        transaction.AddTransaction(SetDataTransaction.Create(path, data));
}

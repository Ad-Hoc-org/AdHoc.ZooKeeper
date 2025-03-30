// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

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

    public Response ReadResponse(in ZooKeeperReadContext context)
    {
        if (context.Status == ZooKeeperStatus.NoNode)
            return new(context.Transaction, default);

        context.Status.ThrowIfError();

        return new(context.Transaction, ZooKeeperNode.Read(context.Data, (context.Root + Path).Absolute, out _));
    }


    public readonly record struct Response(
        long Transaction,
        ZooKeeperNode? Node
    ) : IZooKeeperResponse
    {
        public bool Updated => Node is not null;
    }
}

public static partial class ZooKeeperTransactions
{
    public static Task<Response> SetDataAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        int version,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, data, version), cancellationToken);

    public static Task<Response> SetDataAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, data), cancellationToken);
}

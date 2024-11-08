using System.Diagnostics;
using static AdHoc.ZooKeeper.Abstractions.GetEphemeralsTransaction;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record GetEphemeralsTransaction
    : IZooKeeperTransaction<Response>
{
    public ZooKeeperOperations Operation => ZooKeeperOperations.GetEphemerals;

    public ZooKeeperPath Path { get; }

    private GetEphemeralsTransaction(ZooKeeperPath path)
    {
        path.ThrowIfEmptyOrInvalid();
        Path = path;
    }

    public static GetEphemeralsTransaction Create(ZooKeeperPath path) =>
        new(path);

    public int GetMaxRequestSize(in ZooKeeperPath root) =>
        Path.GetMaxBufferSize(root);

    public int WriteRequest(in ZooKeeperWriteContext context) =>
        Path.Normalize(context.Root)
            .Write(context.Buffer);

    public Response ReadResponse(in ZooKeeperReadContext context, out int size)
    {
        Debug.Assert(context.Operation == Operation);

        context.Status.ThrowIfError();

        var data = context.Data;
        var ephemerals = new ZooKeeperPath[ReadInt32(data)];
        size = LengthSize;
        for (var i = 0; i < ephemerals.Length; i++)
        {
            ephemerals[i] = ZooKeeperPath.Read(data.Slice(size), out int pathSize);
            size += pathSize;
        }

        return new(context.Transaction, Path.Normalize(context.Root), ephemerals);
    }

    public readonly record struct Response(
        long Transaction,
        ZooKeeperPath Path,
        ReadOnlyMemory<ZooKeeperPath> Ephemerals
    ) : IZooKeeperResponse;
}

public static partial class ZooKeeperTransactions
{
    public static Task<Response> GetEphemeralsAsync(
        this IZooKeeper zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(GetEphemeralsTransaction.Create(path), cancellationToken);
}

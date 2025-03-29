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

    public Response ReadResponse(in ZooKeeperReadContext context)
    {
        context.Status.ThrowIfError();

        var data = context.Data;
        var ephemerals = new ZooKeeperPath[ReadInt32(data)];
        int offset = LengthSize;
        for (var i = 0; i < ephemerals.Length; i++)
        {
            ephemerals[i] = ZooKeeperPath.Read(data.Slice(offset), out int size);
            offset += size;
        }
        return new(context.Transaction, ephemerals);
    }

    public readonly record struct Response(
        long Transaction,
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
        zooKeeper.ProcessAsync(Create(path), cancellationToken);
}

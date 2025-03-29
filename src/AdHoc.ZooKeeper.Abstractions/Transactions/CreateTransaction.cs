// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.CreateTransaction;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record CreateTransaction
    : IZooKeeperTransaction<Response>
{
    private enum ModeFlag : int
    {
        Persistent = 0,
        Ephemeral = 1,
        Sequential = 1 << 1,
        PersistentSequential = Persistent | Sequential,
        EphemeralSequential = Ephemeral | Sequential,
        Container = 1 << 2,
        TimeToLive = 5,
        PersistentWithTimeToLive = Persistent | TimeToLive,
        EphemeralWithTimeToLive = Ephemeral | TimeToLive,
    }

    // TODO will be different depending on ttl and container
    public ZooKeeperOperations Operation => ZooKeeperOperations.Create;


    public ZooKeeperPath Path { get; }

    public ReadOnlyMemory<byte> Data { get; }


    private readonly ModeFlag _mode;
    public bool IsPersistent => !_mode.HasFlag(ModeFlag.Ephemeral | ModeFlag.Container);
    public bool IsEphemeral => _mode.HasFlag(ModeFlag.Ephemeral);
    public bool IsContainer => _mode.HasFlag(ModeFlag.Container);
    public bool IsSequential => _mode.HasFlag(ModeFlag.Sequential);

    // TODO
    public TimeSpan? TimeToLive { get; private init; }


    private CreateTransaction(ZooKeeperPath path, ReadOnlyMemory<byte> data, ModeFlag mode)
    {
        path.ThrowIfEmptyOrInvalid();
        Path = path;
        Data = data;
        _mode = mode;
    }

    public static CreateTransaction Create(ZooKeeperPath path, ReadOnlyMemory<byte> data) =>
        new(path, data, ModeFlag.Persistent);

    public static CreateTransaction CreateEphemeral(ZooKeeperPath path, ReadOnlyMemory<byte> data) =>
        new(path, data, ModeFlag.Ephemeral);


    public int GetMaxRequestSize(in ZooKeeperPath root) =>
        Path.GetMaxBufferSize(root)
        + LengthSize + Data.Length
        + LengthSize + Int32Size + LengthSize + "world".Length + LengthSize + "anyone".Length
        + Int32Size;

    public int WriteRequest(in ZooKeeperWriteContext context)
    {
        var buffer = context.Buffer;

        var path = Path.Normalize(context.Root);
        int size = path.Write(buffer);
        size += Write(buffer.Slice(size), Data.Span);

        // TODO ACL
        size += Write(buffer.Slice(size), 1);
        size += Write(buffer.Slice(size), (int)ZooKeeperPermissions.All);
        size += Write(buffer.Slice(size), "world");
        size += Write(buffer.Slice(size), "anyone");

        size += Write(buffer.Slice(size), (int)_mode);

        return size;
    }

    public Response ReadResponse(in ZooKeeperReadContext context)
    {
        if (context.Status == ZooKeeperStatus.NodeExists)
            return new(context.Transaction, Path.Normalize(context.Root), true, false);
        if (context.Status == ZooKeeperStatus.NoNode)
            return new(context.Transaction, Path.Normalize(context.Root), false, true);

        context.Status.ThrowIfError();

        return new(
            context.Transaction,
            ZooKeeperPath.Read(context.Data, out _),
            false,
            false
        );
    }

    public readonly record struct Response(
        long Transaction,
        ZooKeeperPath Path,
        bool AlreadyExisted,
        bool ContainerMissing
    ) : IZooKeeperResponse;
}

public static partial class ZooKeeperTransactions
{

    public static Task<Response> CreateAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, data), cancellationToken);

    public static Task<Response> CreateAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, ReadOnlyMemory<byte>.Empty), cancellationToken);


    public static Task<Response> CreateEphemeralAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(CreateEphemeral(path, data), cancellationToken);

    public static Task<Response> CreateEphemeralAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(CreateEphemeral(path, ReadOnlyMemory<byte>.Empty), cancellationToken);

}

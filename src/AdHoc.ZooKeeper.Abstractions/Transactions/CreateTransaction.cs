// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using static AdHoc.ZooKeeper.Abstractions.CreateTransaction;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record CreateTransaction
    : IZooKeeperTransaction<Response>
{
    private const long _MaxTimeToLive = 1099511627775L; // feature masked
    private enum ModeFlag : int
    {
        Persistent = 0,
        Ephemeral = 1,
        Sequential = 1 << 1,
        PersistentSequential = Persistent | Sequential,
        EphemeralSequential = Ephemeral | Sequential,
        Container = 1 << 2,
        WithTimeToLive = 5,
        SequentialWithTimeToLive = Sequential | WithTimeToLive
    }

    // TODO will be different depending on ttl and container
    public ZooKeeperOperations Operation { get; }


    public ZooKeeperPath Path { get; }

    public ReadOnlyMemory<byte> Data { get; }


    private readonly ModeFlag _mode;
    public bool IsPersistent => !_mode.HasFlag(ModeFlag.Ephemeral | ModeFlag.Container);
    public bool IsEphemeral => _mode.HasFlag(ModeFlag.Ephemeral);
    public bool IsContainer => _mode.HasFlag(ModeFlag.Container);
    public bool IsSequential => _mode.HasFlag(ModeFlag.Sequential);

    public TimeSpan? TimeToLive { get; }


    private CreateTransaction(ZooKeeperPath path, ReadOnlyMemory<byte> data, ModeFlag mode, TimeSpan? timeToLive)
    {
        path.ThrowIfEmptyOrInvalid();
        Path = path;
        Data = data;
        _mode = mode;
        TimeToLive = timeToLive;
        if (timeToLive is null)
            Operation = ZooKeeperOperations.Create;
        else
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeToLive.Value, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(timeToLive.Value.TotalMilliseconds, _MaxTimeToLive);
            Debug.Assert(_mode.HasFlag(ModeFlag.WithTimeToLive));
            Operation = ZooKeeperOperations.CreateWithTimeToLive;
        }
    }

    public static CreateTransaction Create(
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        bool sequential
    ) =>
        new(path, data, sequential ? ModeFlag.PersistentSequential : ModeFlag.Persistent, null);

    public static CreateTransaction CreateContainer(
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data
    ) =>
        new(path, data, ModeFlag.Container, null);

    public static CreateTransaction Create(
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        TimeSpan timeToLive,
        bool sequential
    ) =>
        new(path, data, sequential ? ModeFlag.SequentialWithTimeToLive : ModeFlag.WithTimeToLive, timeToLive);

    public static CreateTransaction CreateEphemeral(
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        bool sequential
    ) =>
        new(path, data, sequential ? ModeFlag.EphemeralSequential : ModeFlag.Ephemeral, null);


    public int GetMaxRequestSize(in ZooKeeperPath root) =>
        Path.GetMaxBufferSize(root)
        + LengthSize + Data.Length
        + LengthSize + Int32Size + LengthSize + "world".Length + LengthSize + "anyone".Length
        + Int32Size
        + (TimeToLive is null ? 0 : TimeSpanSize);

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

        if (TimeToLive is not null)
            size += Write(buffer.Slice(size), TimeToLive.Value);

        return size;
    }

    public Response ReadResponse(in ZooKeeperReadContext context)
    {
        if (context.Status == ZooKeeperStatus.NodeExists)
            return new(context.Transaction, Path.Normalize(context.Root), true, false);
        if (context.Status == ZooKeeperStatus.NoNode)
            return new(context.Transaction, Path.Normalize(context.Root), false, true);

        if (context.Status == ZooKeeperStatus.Unimplemented && TimeToLive is not null)
            throw new ResponseException(ZooKeeperStatus.Unimplemented, "Nodes with time to live are not supported. Please enable extended types on the server to use this feature.");
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
        bool sequential,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, data, sequential), cancellationToken);

    public static Task<Response> CreateAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, data, false), cancellationToken);

    public static Task<Response> CreateAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        bool sequential,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, ReadOnlyMemory<byte>.Empty, sequential), cancellationToken);

    public static Task<Response> CreateAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, ReadOnlyMemory<byte>.Empty, false), cancellationToken);



    public static Task<Response> CreateContainerAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(CreateContainer(path, data), cancellationToken);

    public static Task<Response> CreateContainerAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(CreateContainer(path, ReadOnlyMemory<byte>.Empty), cancellationToken);



    public static Task<Response> CreateEphemeralAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        bool sequential,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(CreateEphemeral(path, data, sequential), cancellationToken);

    public static Task<Response> CreateEphemeralAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(CreateEphemeral(path, data, false), cancellationToken);

    public static Task<Response> CreateEphemeralAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        bool sequential,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(CreateEphemeral(path, ReadOnlyMemory<byte>.Empty, sequential), cancellationToken);

    public static Task<Response> CreateEphemeralAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(CreateEphemeral(path, ReadOnlyMemory<byte>.Empty, false), cancellationToken);



    public static Task<Response> CreateAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        TimeSpan timeToLive,
        bool sequential,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, data, timeToLive, sequential), cancellationToken);

    public static Task<Response> CreateAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        ReadOnlyMemory<byte> data,
        TimeSpan timeToLive,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, data, timeToLive, false), cancellationToken);

    public static Task<Response> CreateAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        TimeSpan timeToLive,
        bool sequential,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, ReadOnlyMemory<byte>.Empty, timeToLive, sequential), cancellationToken);

    public static Task<Response> CreateAsync(
        this IZooKeeperTransactable zooKeeper,
        ZooKeeperPath path,
        TimeSpan timeToLive,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ProcessAsync(Create(path, ReadOnlyMemory<byte>.Empty, timeToLive, false), cancellationToken);

}

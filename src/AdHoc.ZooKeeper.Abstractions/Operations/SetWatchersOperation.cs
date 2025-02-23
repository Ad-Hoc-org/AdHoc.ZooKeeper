// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Frozen;
using static AdHoc.ZooKeeper.Abstractions.Operations;
using static AdHoc.ZooKeeper.Abstractions.SetWatcherOperations;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record SetWatcherOperations
    : IZooKeeperOperation<Result>
{
    public const int Request = -4;

    private static readonly ReadOnlyMemory<byte> _RequestBytes = new byte[] { 255, 255, 255, 247 };
    private static readonly ReadOnlyMemory<byte> _OperationBytes = new byte[] { 0, 0, 0, 101 };


    public IReadOnlySet<ZooKeeperPath> Children { get; }
    public IReadOnlySet<ZooKeeperPath> Data { get; }
    public IReadOnlySet<ZooKeeperPath> Exists { get; }


    private SetWatcherOperations(
        IReadOnlySet<ZooKeeperPath> children,
        IReadOnlySet<ZooKeeperPath> data,
        IReadOnlySet<ZooKeeperPath> exists
    )
    {
        Children = children;
        Data = data;
        Exists = exists;
    }

    public void WriteRequest(in ZooKeeperContext context)
    {
        var writer = context.Writer;
        var buffer = writer.GetSpan(RequestHeaderSize
            + Int32Size
            + GetMaxPathsSize(Children, context.Root)
            + GetMaxPathsSize(Data, context.Root)
            + GetMaxPathsSize(Exists, context.Root)
        );
        int size = LengthSize;

        _RequestBytes.Span.CopyTo(buffer.Slice(size));
        size += RequestSize;

        _OperationBytes.Span.CopyTo(buffer.Slice(size));
        size += OperationSize;

        size += WritePaths(buffer.Slice(size), Children, context.Root);
        size += WritePaths(buffer.Slice(size), Data, context.Root);
        size += WritePaths(buffer.Slice(size), Exists, context.Root);

        Write(buffer, size - LengthSize);
        writer.Advance(size);
    }

    private static int GetMaxPathsSize(IReadOnlySet<ZooKeeperPath> paths, ZooKeeperPath root)
    {
        int size = LengthSize;
        foreach (var path in paths)
            size += path.GetMaxSize(root);
        return size;
    }

    private static int WritePaths(Span<byte> buffer, IReadOnlySet<ZooKeeperPath> paths, ZooKeeperPath root)
    {
        int size = Write(buffer, paths.Count);
        foreach (var path in paths)
            size += path.Write(buffer.Slice(size), root);
        return size;
    }

    public Result ReadResponse(in ZooKeeperResponse response, IZooKeeperWatcher? watcher)
    {
        response.ThrowIfError();
        return new Result();
    }

    public static SetWatcherOperations Create(
        IEnumerable<ZooKeeperPath>? children = null,
        IEnumerable<ZooKeeperPath>? data = null,
        IEnumerable<ZooKeeperPath>? exists = null
    ) =>
        new SetWatcherOperations(
            children?.Select(p => p.Absolute()).ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty,
            data?.Select(p => p.Absolute()).ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty,
            exists?.Select(p => p.Absolute()).ToFrozenSet() ?? FrozenSet<ZooKeeperPath>.Empty
        );

    public readonly record struct Result();
}

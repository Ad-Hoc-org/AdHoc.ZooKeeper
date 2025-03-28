// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;
using static AdHoc.ZooKeeper.Abstractions.RemoveWatchOperation;

namespace AdHoc.ZooKeeper.Abstractions;

public sealed record RemoveWatchOperation
    : IZooKeeperTransaction<Result>
{
    private static readonly ReadOnlyMemory<byte> _Operation = new byte[] { 0, 0, 0, 18 };


    public IZooKeeperWatcher Watcher { get; }

    private RemoveWatchOperation(IZooKeeperWatcher watcher) =>
        Watcher = watcher;


    public void WriteRequest(in ZooKeeperWriteContext context)
    {
        var writer = context.Writer;
        var buffer = writer.GetSpan(RequestHeaderSize + Watcher.Path.GetMaxBufferSize() + Int32Size);
        int size = LengthSize;

        size += Write(buffer.Slice(size), context.GetRequest(ZooKeeperOperations.RemoveWatch));

        _Operation.Span.CopyTo(buffer.Slice(size));
        size += OperationSize;

        size += Watcher.Path.Write(buffer.Slice(size));

        size += Write(buffer.Slice(size), (int)Watcher.Type);

        Write(buffer, size - LengthSize);
        writer.Advance(size);
    }

    public Result ReadResponse(in ZooKeeperReadContext response, IZooKeeperWatcher? watcher)
    {
        response.ThrowIfError();
        return new(response.Transaction);
    }


    public static RemoveWatchOperation Create(IZooKeeperWatcher watcher)
    {
        ArgumentNullException.ThrowIfNull(watcher);
        return new(watcher);
    }


    public readonly record struct Result(long Transaction);
}

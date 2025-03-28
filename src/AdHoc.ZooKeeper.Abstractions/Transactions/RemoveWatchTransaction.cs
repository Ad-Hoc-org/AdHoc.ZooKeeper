// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.RemoveWatchTransaction;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;

public sealed record RemoveWatchTransaction
    : IZooKeeperTransaction<Response>
{
    public ZooKeeperOperations Operation => ZooKeeperOperations.RemoveWatch;


    public IZooKeeperWatcher Watcher { get; }


    private RemoveWatchTransaction(IZooKeeperWatcher watcher)
    {
        ArgumentNullException.ThrowIfNull(watcher);
        Watcher = watcher;
    }

    public static RemoveWatchTransaction Create(IZooKeeperWatcher watcher) =>
        new(watcher);


    public int GetMaxRequestSize(in ZooKeeperPath root) =>
        Watcher.Path.GetMaxBufferSize(root) + Int32Size;


    public int WriteRequest(in ZooKeeperWriteContext context)
    {
        var buffer = context.Buffer;

        int size = Watcher.Path.Write(buffer);
        size += Write(buffer.Slice(size), (int)Watcher.Type);

        return size;
    }

    public Response ReadResponse(in ZooKeeperReadContext context)
    {
        context.Status.ThrowIfError();
        return new(context.Transaction);
    }


    public readonly record struct Response(long Transaction);
}

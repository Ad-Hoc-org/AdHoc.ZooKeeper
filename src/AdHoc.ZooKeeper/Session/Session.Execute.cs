// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{
    internal Task<TResponse> ExecuteAsync<TResponse>(
        IZooKeeperTransaction<TResponse> transaction,
        ZooKeeperPath root,
        Func<Watcher, WatchAsync, WatchAsync>? registerWatcher,
        CancellationToken cancellationToken
    )
        where TResponse : IZooKeeperResponse
    {
        ObjectDisposedException.ThrowIf(_disposeSource.IsCancellationRequested, this);

        if (transaction.Operation == ZooKeeperOperations.RemoveWatch)
            throw new InvalidOperationException("Remove watch transaction can be only used internally to reduce miss handling of events.");

        if (transaction.Operation == ZooKeeperOperations.Ping)
            return ExecutePingAsync(root, transaction, cancellationToken);

        return DispatchAsync(root, transaction, registerWatcher, cancellationToken);
    }

    private async Task<TResponse> ExecutePingAsync<TResponse>(
        ZooKeeperPath root,
        IZooKeeperTransaction<TResponse> transaction,
        CancellationToken cancellationToken
    )
        where TResponse : IZooKeeperResponse
    {
        TaskCompletionSource<Response> pending = new();
        while (!_pending.TryAdd(PingTransaction.Request, pending))
            if (_pending.TryGetValue(PingTransaction.Request, out var previous) && !previous.Task.IsCompleted)
                try { await previous.Task.WaitAsync(cancellationToken); } catch { }

        return await DispatchAsync(root, PingTransaction.Request, pending, transaction, null, cancellationToken);
    }
}

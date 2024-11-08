// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<Response>> _pending = new();

    private async Task<TResponse> DispatchAsync<TResponse>(
        ZooKeeperPath root,
        IZooKeeperTransaction<TResponse> transaction,
        Func<Watcher, WatchAsync, WatchAsync>? registerWatcher,
        CancellationToken cancellationToken
    ) where TResponse : IZooKeeperResponse
    {
        int request;
        TaskCompletionSource<Response> pending = new();
        await _writeLock.WaitAsync(cancellationToken);
        NetworkStream stream;
        try
        {
            stream = await EnsureSessionAsync(cancellationToken);
            do
            {
                request = GetRequest(transaction.Operation);
                Debug.Assert(request != PingTransaction.Request); // should be manage pending itself
                if (request < 0)
                    throw new InvalidOperationException("Session transactions are only allowed internally.");
            } while (!_pending.TryAdd(request, pending));
        }
        finally
        {
            _writeLock.Release();
        }

        return await DispatchAsync(stream, root, request, pending, transaction, registerWatcher, cancellationToken);
    }

    private async Task<TResponse> DispatchAsync<TResponse>(
        NetworkStream stream,
        ZooKeeperPath root,
        int request,
        TaskCompletionSource<Response> pending,
        IZooKeeperTransaction<TResponse> transaction,
        Func<Watcher, WatchAsync, WatchAsync>? registerWatcher,
        CancellationToken cancellationToken
    ) where TResponse : IZooKeeperResponse
    {
        Debug.Assert(_pending.Values.Contains(pending));
        try
        {
            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                _pending.TryRemove(KeyValuePair.Create(request, pending));
                pending.TrySetCanceled(cancellationToken);
            });
            await _writeLock.WaitAsync(cancellationToken);
            IZooKeeperWatcher? watcher = null;
            try
            {
                await WriteAsync(
                    stream,
                    writer => WriteTransaction(
                        writer, root, request, transaction,
                        (ZooKeeperPath path, Types type, WatchAsync watch) =>
                        {
                            if (watcher is not null)
                                throw new InvalidOperationException("Only one watcher per operation allowed");
                            watcher = RegisterWatcher(path, type, watch, registerWatcher);
                        }
                    ),
                    cancellationToken
                );
            }
            finally { _writeLock.Release(); }

            return await ReceiveAsync(stream, root, pending.Task.WaitAsync(cancellationToken), transaction, watcher, cancellationToken);
        }
        catch (Exception ex)
        {
            _pending.TryRemove(KeyValuePair.Create(request, pending));
            pending.TrySetException(ex);
            throw;
        }
    }

    private async Task<TResponse> ReceiveAsync<TResponse>(
        NetworkStream stream,
        ZooKeeperPath root,
        Task<Response> pending,
        IZooKeeperTransaction<TResponse> transaction,
        IZooKeeperWatcher? watcher,
        CancellationToken cancellationToken
    )
        where TResponse : IZooKeeperResponse
    {
        pending = pending.WaitAsync(cancellationToken);
        Task receiving;
        while (!pending.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            receiving = ReceivingAsync(stream);

            await Task.WhenAny(receiving, pending);
        }

        using var response = await pending;
        return ReadTransaction(response._memory, root, transaction, watcher);
    }
}

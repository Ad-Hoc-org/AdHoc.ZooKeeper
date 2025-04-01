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
        do
        {
            request = GetRequest(transaction.Operation);
            Debug.Assert(request != PingTransaction.Request); // should be manage pending itself
            if (request < 0)
                throw new InvalidOperationException("Session transactions are only allowed internally.");
        } while (!_pending.TryAdd(request, pending));

        return await DispatchAsync(root, request, pending, transaction, registerWatcher, cancellationToken);
    }

    private async Task<TResponse> DispatchAsync<TResponse>(
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
            NetworkStream stream;
            IZooKeeperWatcher? watcher = null;
            try
            {
                stream = await EnsureSessionAsync(cancellationToken);
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

#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            return await ReceiveAsync(stream, root, pending.Task, transaction, watcher, cancellationToken);
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
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
        Task receiving;
        while (!pending.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            receiving = ReceivingAsync(stream);

#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            await Task.WhenAny(receiving, pending);
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        }

#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
        using var response = await pending;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        return ReadTransaction(response._memory, root, transaction, watcher);
    }
}

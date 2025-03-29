// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<Response>> _pending = new();
    private readonly ConcurrentDictionary<int, Task> _responding = new();

    private async Task<TResponse> DispatchAsync<TResponse>(
        ZooKeeperPath root,
        IZooKeeperTransaction<TResponse> transaction,
        Func<Watcher, WatchAsync, WatchAsync>? registerWatcher,
        CancellationToken cancellationToken
    ) where TResponse : IZooKeeperResponse
    {
        await _lock.WaitAsync(cancellationToken);

        bool released = false;
        TaskCompletionSource<Response> pending = new();
        int? request = null;
        Task<TResponse>? receive = null;
        CancellationTokenRegistration registration = default;
        try
        {
            var stream = await EnsureSessionAsync(cancellationToken);

            do
            {
                request = GetRequest(transaction.Operation);
                if (request == PingTransaction.Request)
                    break;
            } while (!_pending.TryAdd(request.Value, pending));
            registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));

            IZooKeeperWatcher? watcher = null;
            await WriteAsync(
                stream,
                writer => WriteTransaction(
                    writer, root, request.Value, transaction,
                    (ZooKeeperPath path, Types type, WatchAsync watch) =>
                    {
                        if (watcher is not null)
                            throw new InvalidOperationException("Only one watcher per operation allowed");
                        watcher = RegisterWatcher(path, type, watch, registerWatcher);
                    }
                ),
                cancellationToken
            );

            receive = ReceiveAsync(stream, root, pending.Task, transaction, watcher, cancellationToken);
            _responding[request.Value] = receive;

            // release lock after writing and task management is done
            _lock.Release();
            released = true;

            var response = await receive;
            _responding.TryRemove(KeyValuePair.Create<int, Task>(request.Value, receive));
            _pending.TryRemove(KeyValuePair.Create(request.Value, pending));
            return response;
        }
        catch (Exception ex)
        {
            bool canceled = ex is OperationCanceledException canceledEx && canceledEx.CancellationToken == cancellationToken;

            if (request is not null)
            {
                if (receive is not null)
                    _responding.TryRemove(KeyValuePair.Create<int, Task>(request.Value, receive));
                if (_pending.TryRemove(KeyValuePair.Create(request.Value, pending)))
                    if (canceled)
                        pending.TrySetCanceled(cancellationToken);
                    else
                        pending.TrySetException(ex);
            }

            throw;
        }
        finally
        {
            await registration.DisposeAsync();
            if (!released)
                _lock.Release();
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
            var task = await Task.WhenAny(receiving, pending);
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        }

#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
        using var response = await pending;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        return ReadTransaction(response._memory, root, transaction, watcher);
    }
}

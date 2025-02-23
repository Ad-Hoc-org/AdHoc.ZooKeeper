// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.Operations;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperEvent;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{

    private Task _receiving;

    private readonly ConcurrentDictionary<int, TaskCompletionSource<Response>> _pending;
    private readonly ConcurrentDictionary<int, Task> _responding;

    private readonly CancellationTokenSource _disposeSource;


    private async Task<TResult> ReceiveAsync<TResult>(
        IZooKeeperOperation<TResult> operation,
        ZooKeeperPath root,
        NetworkStream stream,
        Task<Response> pending,
        IZooKeeperWatcher? watcher,
        CancellationToken cancellationToken
    )
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
        var transaction = response.ToTransaction(root);
        _lastTransaction = transaction.Transaction;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        return operation.ReadResponse(
            transaction,
            watcher
        );
    }

    private async Task ReceivingAsync(NetworkStream stream)
    {
        var receiving = _receiving;
        if (!receiving.IsCompleted)
        {
            await receiving;
            return;
        }

        CancellationToken cancellationToken = _disposeSource.Token;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_receiving.IsCompleted)
                receiving = _receiving;
            else
                _receiving = receiving = Task.Run(async () =>
                {
                    IMemoryOwner<byte>? owner = null;
                    try
                    {
                        while (!_pending.IsEmpty || !_watchers.IsEmpty)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                await ResponseWithAsync(new ObjectDisposedException(this.ToString()));
                                continue;
                            }

                            try
                            {
                                var responseTask = ReadAsync(stream, owner, cancellationToken);
                                while (!responseTask.IsCompleted)
                                {
                                    await Task.WhenAny(
                                        responseTask,
                                        KeepAliveAsync(cancellationToken)
                                    );
                                }
                                var response = await responseTask;
                                owner = response._owner;

                                var requestIdentifier = ReadInt32(response._memory.Span);
                                if (_pending.TryRemove(requestIdentifier, out var request))
                                {
                                    if (request.TrySetResult(response))
                                        owner = null;
                                }
                                else if (requestIdentifier == NoRequest)
                                {
                                    DispatchEvent(response);
                                }
                                else
                                {
                                    Console.WriteLine("Received: " + string.Join(",", response._memory.ToArray()));
                                }
                            }
                            catch (Exception ex)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                    await ResponseWithAsync(new ObjectDisposedException(this.ToString(), ex));
                                else
                                    await ResponseWithAsync(ex);
                                if (_pending.IsEmpty && ex is ConnectionLostException)
                                    return;
                            }
                        }
                    }
                    finally
                    {
                        owner?.Dispose();
                    }
                }, cancellationToken);
        }
        finally { _lock.Release(); }
        await receiving;
    }

    private async Task ResponseWithAsync(Exception exception)
    {
        await _lock.WaitAsync();
        try
        {
            while (!_pending.IsEmpty)
                if (_pending.TryRemove(_pending.Keys.First(), out var request))
                    request.TrySetException(exception);
        }
        finally
        {
            _lock.Release();
        }
    }

}

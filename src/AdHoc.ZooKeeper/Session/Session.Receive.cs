// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{

    private Task _receiving = Task.CompletedTask;

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
                                    Debug.Assert(false);
                                }
                            }
                            catch (Exception ex)
                            {
                                if (_disposeSource.IsCancellationRequested)
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

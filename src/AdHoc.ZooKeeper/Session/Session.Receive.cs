// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{

    private Task _receiving = Task.CompletedTask;

    private Task ReceivingAsync(NetworkStream stream)
    {
        if (!_receiving.IsCompleted)
            return _receiving;

        lock (_receiving)
            if (!_receiving.IsCompleted)
                return _receiving;
            else
                return _receiving = ProcessDataAsync(stream, _disposeSource.Token);

        async Task ProcessDataAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            await Task.Yield();

            IMemoryOwner<byte>? owner = null;
            try
            {
                while (!_pending.IsEmpty || HasWatchers)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        ThrowWith(new ObjectDisposedException(this.ToString()));
                        DeregisterWatchers();
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
                            DispatchEvent(response);
                        else
                            Debug.Assert(false);
                    }
                    catch (Exception ex)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            ThrowWith(new ObjectDisposedException(this.ToString(), ex));
                        else
                            ThrowWith(ex);

                        if (!stream.Socket.Connected)
                            return; // receiving has to start with new connection
                    }
                }
            }
            finally
            {
                owner?.Dispose();
            }
        }
    }

    private void ThrowWith(Exception exception)
    {
        while (!_pending.IsEmpty)
            if (_pending.TryRemove(_pending.Keys.FirstOrDefault(), out var request))
                request.TrySetException(exception);
    }

}

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.IO.Pipelines;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.Operations;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{
    internal async Task<TResult> ExecuteAsync<TResult>(
        IZooKeeperOperation<TResult> operation,
        ZooKeeperPath root,
        Func<Watcher, WatchAsync, WatchAsync>? registerWatcher,
        CancellationToken cancellationToken
    )
    {
        ObjectDisposedException.ThrowIf(_disposeSource.IsCancellationRequested, this);

        bool dispatched;
        if (operation is not PingOperation)
        {
            (var result, dispatched) = await DispatchAsync(operation, root, async (stream, pending, cancellationToken) =>
            {
                var pipeWriter = PipeWriter.Create(stream);
                var writer = new SafeRequestWriter(pipeWriter);
                bool hasRequest = false;
                Watcher? watcher = null;
                operation.WriteRequest(new(
                    root,
                    writer,
                    (operation) =>
                    {
                        int request;
                        do
                        {
                            request = GetRequest(operation);
                            if (request == PingOperation.Request)
                                break;
                        } while (!_pending.TryAdd(request, pending));
                        hasRequest = true;
                        return request;
                    },
                    (ZooKeeperPath path, Types type, WatchAsync watch) =>
                    {
                        if (watcher is not null)
                            throw new InvalidOperationException("Only one watcher per operation allowed");
                        watcher = RegisterWatcher(path, type, watch, registerWatcher);
                    }
                ));

                if (writer.IsPing)
                    return default;

                if (writer.IsCompleted)
                    throw ZooKeeperException.CreateInvalidRequestSize(writer.Length, writer.Size);

                if (!hasRequest)
                    throw new ZooKeeperException("Request identifier has to be requested from context!");

                _lastInteractionTimestamp = Stopwatch.GetTimestamp();
                await pipeWriter.FlushAsync(cancellationToken);
                return (writer.Request, watcher);
            }, cancellationToken);
            if (dispatched)
                return result!;
        }

        // is ping
        return await ExecutePingAsync(root, operation, cancellationToken);
    }

    private async Task<TResult> ExecutePingAsync<TResult>(
        ZooKeeperPath root,
        IZooKeeperOperation<TResult> operation,
        CancellationToken cancellationToken
    )
    {
        Task<TResult>? ping = null;
        var (pingResult, dispatched) = await DispatchAsync(operation, root, async (stream, pending, cancellationToken) =>
        {
            if (_responding.TryGetValue(PingOperation.Request, out var task))
            {
                if (operation is PingOperation)
                    ping = Task.Run(async () => await (Task<TResult>)task, cancellationToken);
                else
                {
                    if (!_pending.TryGetValue(PingOperation.Request, out var response))
                        throw new InvalidOperationException();
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                    ping = Task.Run(async () => operation.ReadResponse((await response.Task).ToTransaction(root), null), cancellationToken);
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
                }
                return default;
            }

            _pending[PingOperation.Request] = pending;
            await stream.WriteAsync(PingOperation.Bytes, cancellationToken);
            _lastInteractionTimestamp = Stopwatch.GetTimestamp();
            await stream.FlushAsync(cancellationToken);
            return (PingOperation.Request, null);
        }, cancellationToken);
        return dispatched ? pingResult! : await ping!;
    }
}

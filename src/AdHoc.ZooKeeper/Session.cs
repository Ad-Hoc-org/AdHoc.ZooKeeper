// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeper;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.Operations;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperEvent;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
    : IAsyncDisposable
{
    private readonly Host _host;

    private readonly IReadOnlySet<Authentication> _authentications;

    private readonly TimeSpan _connectionTimeout;

    private readonly TimeSpan _sessionTimeout;

    private readonly bool _readOnly;


    internal Session(
        Host host,
        IReadOnlySet<Authentication> authentications,
        TimeSpan connectionTimeout,
        TimeSpan sessionTimeout,
        bool readOnly
    )
    {
        _host = host;
        _authentications = authentications;
        _connectionTimeout = connectionTimeout;
        _sessionTimeout = sessionTimeout;
        _readOnly = readOnly;

        _pending = new();
        _receiving = new();
        _watchers = new();
        _receiveTask = Task.CompletedTask;
        _disposeSource = new();
    }


    internal async Task<TResult> ExecuteAsync<TResult>(
        IZooKeeperOperation<TResult> operation,
        ZooKeeperPath root,
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
                            request = GetRequest(operation, ref _previousRequest);
                            if (request == PingOperation.Request)
                                break;
                        } while (!_pending.TryAdd(request, pending));
                        hasRequest = true;
                        return request;
                    },
                    (IEnumerable<ZooKeeperPath> paths, WatchAsync watch) =>
                        watcher = RegisterWatcher(paths, watch)
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
            if (_receiving.TryGetValue(PingOperation.Request, out var task))
            {
                if (operation is PingOperation)
                    ping = Task.Run(async () => await (Task<TResult>)task, cancellationToken);
                else
                {
                    if (!_pending.TryGetValue(PingOperation.Request, out var response))
                        throw new InvalidOperationException();
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                    ping = Task.Run(async () => operation.ReadResponse((await response.Task).ToResponse(root), null), cancellationToken);
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

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeper;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.Operations;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperEvent;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{

    private Task _receiving;

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Watcher, WatchAsync>> _watchers;

    private readonly ConcurrentDictionary<int, TaskCompletionSource<Response>> _pending;
    private readonly ConcurrentDictionary<int, Task> _responding;

    private readonly CancellationTokenSource _disposeSource;


    private async Task<TResult> ReceiveAsync<TResult>(
        IZooKeeperOperation<TResult> operation,
        ZooKeeperPath root,
        NetworkStream stream,
        Task<Response> pending,
        Watcher? watcher,
        CancellationToken cancellationToken
    )
    {
        Task receiveTask;
        while (!pending.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();

            receiveTask = _receiving;
            if (receiveTask.IsCompleted)
                _receiving = receiveTask = ReceivingAsync(stream);

#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            await Task.WhenAny(receiveTask, pending);
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
        await Task.Yield();
        CancellationToken cancellationToken = _disposeSource.Token;

        IMemoryOwner<byte>? owner = null;
        try
        {
            while (!_pending.IsEmpty || !_watchers.IsEmpty)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await DisconnectWithAsync(new ObjectDisposedException(this.ToString()));
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
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                        await DisconnectWithAsync(new ObjectDisposedException(this.ToString(), ex));
                    else
                        await DisconnectWithAsync(ex);
                }
            }
        }
        finally
        {
            owner?.Dispose();
        }
    }

    private void DispatchEvent(Response response)
    {
        var @event = ZooKeeperEvent.Read(response._memory.Span, out _);
        var path = @event.Path.Value;
        if (_watchers.TryGetValue(path, out var watchers))
        {
            foreach (var watchPair in watchers)
                try
                {
                    if (watchers.TryRemove(watchPair))
#pragma warning disable VSTHRD110 // Observe result of async calls
                        watchPair.Value(watchPair.Key, @event, _disposeSource.Token);
#pragma warning restore VSTHRD110 // Observe result of async calls
                }
                catch { }


            if (watchers.IsEmpty && _watchers.TryRemove(KeyValuePair.Create(path, watchers)))
            {
                // read after watchers was added before removing
                if (!watchers.IsEmpty)
                    _watchers.AddOrUpdate(path, watchers, (_, newWatches) =>
                    {
                        foreach (var watcher in watchers)
                            newWatches.TryAdd(watcher.Key, watcher.Value);
                        return newWatches;
                    });
            }
        }
    }

    private async Task DisconnectWithAsync(Exception exception)
    {
        await _lock.WaitAsync();
        try
        {
            _tcpClient?.Dispose();

            while (!_pending.IsEmpty)
                if (_pending.TryRemove(_pending.Keys.First(), out var request))
                    request.TrySetException(exception);

            while (!_watchers.IsEmpty)
                if (_watchers.TryRemove(_watchers.Keys.First(), out var watchers))
                    foreach (var (watcher, watch) in watchers)
                        try
                        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            watch(watcher, new(0, ZooKeeperStatus.ConnectionLoss, Types.None, States.Disconnected, default), CancellationToken.None);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        }
                        catch { }
        }
        finally
        {
            _lock.Release();
        }
    }


    private Watcher RegisterWatcher(IEnumerable<ZooKeeperPath> paths, WatchAsync watch)
    {
        var watcherPaths = paths.Select(p => p.Absolute().Value).ToImmutableHashSet();
        var watcher = new Watcher(this, watcherPaths);
        foreach (var path in watcherPaths)
            _watchers.AddOrUpdate(path,
                _ =>
                {
                    ConcurrentDictionary<Watcher, WatchAsync> watchers = new();
                    watchers[watcher] = watch;
                    return watchers;
                },
                (_, watchers) =>
                {
                    watchers.TryAdd(watcher, watch);
                    return watchers;
                }
            );
        return watcher;
    }

    private class Watcher
        : IZooKeeperWatcher
    {
        private readonly Session _session;
        private readonly ImmutableHashSet<string> _paths;

        public Watcher(Session session, ImmutableHashSet<string> paths)
        {
            _session = session;
            _paths = paths;
        }

        public ValueTask DisposeAsync()
        {
            foreach (var path in _paths)
                if (_session._watchers.TryGetValue(path, out var watchers))
                    watchers.TryRemove(this, out _);
            return ValueTask.CompletedTask;
        }
    }

}

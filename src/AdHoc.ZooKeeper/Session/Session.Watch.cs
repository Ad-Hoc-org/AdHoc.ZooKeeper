// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeper;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{
    private readonly ConcurrentDictionary<ZooKeeperPath, ConcurrentDictionary<Watcher, WatchAsync>> _watchers = new();
    private readonly ConcurrentDictionary<ZooKeeperPath, ConcurrentDictionary<Watcher, WatchAsync>> _recursiveWatchers = new();

    private void DispatchEvent(Response response)
    {
        var @event = ZooKeeperEvent.Read(response._memory.Span, out _);
        _lastTransaction = @event.Trigger;
        var path = @event.Path;
        if (_watchers.TryGetValue(path, out var watchers))
        {
            foreach (var watchPair in watchers)
                try
                {
                    var (watcher, watch) = watchPair;
                    if (watcher.Type.IsHandling(@event.Type)
                        && (watcher.Type.IsPersistent || watchers.TryRemove(watchPair))
                    )
#pragma warning disable VSTHRD110 // Observe result of async calls
                        watch(watcher, @event, _disposeSource.Token);
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

    private void DeregisterWatchers()
    {
        var state = IsConnected ? States.Closed : States.Disconnected;
        while (!_watchers.IsEmpty)
            if (_watchers.TryRemove(_watchers.Keys.First(), out var watchers))
                foreach (var (watcher, watch) in watchers)
                    try
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#pragma warning disable VSTHRD110 // Observe result of async calls
                        var task = watch(watcher, new(0, ZooKeeperStatus.Ok, ZooKeeperEvent.Types.None, state, default), CancellationToken.None);
#pragma warning restore VSTHRD110 // Observe result of async calls
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                    catch { }
    }

    private async ValueTask ReregisterWatchersAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        if (_watchers.IsEmpty)
            return;

        ConcurrentDictionary<Types, HashSet<ZooKeeperPath>> paths = new();
        foreach (var (path, watchers) in _watchers)
            foreach (var (watcher, _) in watchers)
                paths.AddOrUpdate(watcher.Type,
                    _ => [path],
                    (_, paths) => { paths.Add(path); return paths; }
                );

        await SendAsync(
            stream,
            SetWatchersTransaction.Create(
                _lastTransaction,
                data: paths.TryGetValue(Types.Data, out var data) ? data : null,
                any: paths.TryGetValue(Types.Any, out var any) ? any : null,
                children: paths.TryGetValue(Types.Children, out var children) ? children : null,
                persistent: paths.TryGetValue(Types.Persistent, out var persistent) ? persistent : null,
                recursivePersistent: paths.TryGetValue(Types.RecursivePersistent, out var persistentRecursive) ? persistentRecursive : null
            ),
            cancellationToken
        );

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        ReceivingAsync(stream);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    }

    private Watcher RegisterWatcher(ZooKeeperPath path, Types type, WatchAsync watch, Func<Watcher, WatchAsync, WatchAsync>? registerWatch)
    {
        var watcherPaths = path.Absolute;
        var watcher = new Watcher(this, watcherPaths, type);

        if (registerWatch is not null)
            watch = registerWatch(watcher, watch);

        (type is Types.RecursivePersistent ? _recursiveWatchers : _watchers).AddOrUpdate(path,
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

    internal class Watcher(
        Session session,
        ZooKeeperPath path,
        Types type
    )
        : IZooKeeperWatcher
    {
        public Types Type => type;
        public ZooKeeperPath Path => path;

        public async ValueTask DisposeAsync()
        {
            if (session._watchers.TryGetValue(path, out var watchers))
                if (watchers.TryRemove(this, out _))
                {
                    try
                    {
                        if (watchers.IsEmpty || watchers.All(p => p.Key.Type != Type))
                            try
                            {
                                await session.ExecuteAsync(RemoveWatchTransaction.Create(this), default, null, default);
                            }
                            catch { }
                    }
                    catch { }
                }
        }
    }

}

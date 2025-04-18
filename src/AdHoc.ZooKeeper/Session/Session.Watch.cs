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

    private bool HasWatchers => !_watchers.IsEmpty || !_recursiveWatchers.IsEmpty;

    private void DispatchEvent(Response response)
    {
        var @event = ZooKeeperEvent.Read(response._memory.Span, out _);
        _lastTransaction = @event.Trigger;
        var path = @event.Path;

        if (_watchers.TryGetValue(path, out var watchers))
        {
            foreach (var watchPair in watchers)
            {
                var (watcher, watch) = watchPair;
                if (watcher.Type.IsHandling(@event.Type)
                    && (watcher.Type is Types.Persistent || watchers.TryRemove(watchPair))
                )
                    DispatchEvent(watcher, watch, @event, _disposeSource.Token);
            }

            if (watchers.IsEmpty && _watchers.TryRemove(KeyValuePair.Create(path, watchers)))
            {
                // read after watcher was added before removing
                if (!watchers.IsEmpty)
                    _watchers.AddOrUpdate(path, watchers, (_, newWatches) =>
                    {
                        foreach (var watcher in watchers)
                            newWatches.TryAdd(watcher.Key, watcher.Value);
                        return newWatches;
                    });
            }
        }

        if (!_recursiveWatchers.IsEmpty)
            do
            {
                if (_recursiveWatchers.TryGetValue(path, out var recWatchers))
                    foreach (var watchPair in recWatchers)
                    {
                        var (watcher, watch) = watchPair;
                        DispatchEvent(watcher, watch, @event, _disposeSource.Token);
                    }

                path = path.Parent;
            } while (!path.IsEmpty);
    }

    private static void DispatchEvent(Watcher watcher, WatchAsync watch, ZooKeeperEvent @event, CancellationToken cancellationToken)
    {
#pragma warning disable VSTHRD110 // Observe result of async calls
#pragma warning disable CA2012 // Use ValueTasks correctly
        try { watch(watcher, @event, cancellationToken); } catch { }
#pragma warning restore CA2012 // Use ValueTasks correctly
#pragma warning restore VSTHRD110 // Observe result of async calls
    }

    private void DispatchConnectionEvent(ZooKeeperEvent @event)
    {
        foreach (var (_, watchers) in _watchers.Concat(_recursiveWatchers))
            foreach (var (watcher, watch) in watchers)
                DispatchEvent(watcher, watch, @event, CancellationToken.None);
    }

    private void DeregisterWatchers()
    {
        DispatchConnectionEvent(new(_lastTransaction, ZooKeeperStatus.Ok, ZooKeeperEvent.Types.None, IsConnected ? States.Closed : States.Disconnected, ZooKeeperPath.Empty));
        Deregister(_watchers);
        Deregister(_recursiveWatchers);

        void Deregister(ConcurrentDictionary<ZooKeeperPath, ConcurrentDictionary<Watcher, WatchAsync>> watchesDict)
        {
            while (!watchesDict.IsEmpty)
                if (watchesDict.TryRemove(watchesDict.Keys.FirstOrDefault(), out var watchers))
                    watchers.Clear();
        }
    }

    private async ValueTask ReregisterWatchersAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        if (!HasWatchers)
            return;

        ConcurrentDictionary<Types, HashSet<ZooKeeperPath>> paths = new();
        foreach (var (path, watchers) in _watchers.Concat(_recursiveWatchers))
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
                exists: paths.TryGetValue(Types.Exists, out var exists) ? exists : null,
                children: paths.TryGetValue(Types.Children, out var children) ? children : null,
                persistent: paths.TryGetValue(Types.Persistent, out var persistent) ? persistent : null,
                recursivePersistent: paths.TryGetValue(Types.RecursivePersistent, out var persistentRecursive) ? persistentRecursive : null
            ),
            cancellationToken
        );

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        ReceivingAsync(stream); // should run in background
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
    }

    private Watcher RegisterWatcher(ZooKeeperPath path, Types type, WatchAsync watch, Func<Watcher, WatchAsync, WatchAsync>? registerWatch)
    {
        var watcher = new Watcher(this, path, type);

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
            if ((type is Types.RecursivePersistent ? session._recursiveWatchers : session._watchers).TryGetValue(path, out var watchers))
                if (watchers.TryRemove(this, out _))
                    if (watchers.IsEmpty || watchers.All(p => p.Key.Type != Type))
                        try
                        {
                            await session.DispatchAsync(ZooKeeperPath.Empty, RemoveWatchTransaction.Create(this), null, CancellationToken.None);
                        }
                        catch { }
        }
    }

}

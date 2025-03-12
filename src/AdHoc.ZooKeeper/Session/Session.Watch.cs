// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Concurrent;
using System.Collections.Frozen;
using AdHoc.ZooKeeper.Abstractions;
using Microsoft.VisualBasic;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeper;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{

    private readonly ConcurrentDictionary<ZooKeeperPath, ConcurrentDictionary<Watcher, WatchAsync>> _watchers;

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
                        var task = watch(watcher, new(0, ZooKeeperStatus.ConnectionLoss, ZooKeeperEvent.Types.None, state, default), CancellationToken.None);
#pragma warning restore VSTHRD110 // Observe result of async calls
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                    catch { }
    }

    private async ValueTask ReregisterWatchersAsync(CancellationToken cancellationToken)
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

        var stream = await EnsureSessionAsync(cancellationToken);
        await SendAsync(stream, SetWatcherOperations.Create(
            _lastTransaction,
            children: paths.TryGetValue(Types.Children, out var children) ? children : null,
            data: paths.TryGetValue(Types.Children, out var data) ? data : null,
            exists: paths.TryGetValue(Types.Children, out var exists) ? exists : null
        ), cancellationToken);
    }

    private Watcher RegisterWatcher(IEnumerable<ZooKeeperPath> paths, Types type, WatchAsync watch, Func<Watcher, WatchAsync, WatchAsync>? registerWatch)
    {
        var watcherPaths = paths.Select(p => p.Absolute()).ToFrozenSet();
        var watcher = new Watcher(this, watcherPaths, type);
        if (registerWatch is not null)
            watch = registerWatch(watcher, watch);
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

    internal class Watcher(
        Session session,
        FrozenSet<ZooKeeperPath> paths,
        Types type
    )
        : IZooKeeperWatcher
    {
        public Types Type => type;
        public IReadOnlySet<ZooKeeperPath> Paths => paths;

        public ValueTask DisposeAsync()
        {
            foreach (var path in paths)
                if (session._watchers.TryGetValue(path, out var watchers))
                    watchers.TryRemove(this, out _);
            return ValueTask.CompletedTask;
        }
    }

}

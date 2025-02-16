// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;

namespace AdHoc.ZooKeeper.Abstractions;

public interface IZooKeeperWatcher
    : IAsyncDisposable
{
    public delegate ValueTask WatchAsync(IZooKeeperWatcher watcher, ZooKeeperEvent @event, CancellationToken cancellationToken);
    public delegate void Watch(IZooKeeperWatcher watcher, ZooKeeperEvent @event);
}


public static partial class Operations
{
#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods
    public static WatchAsync ToWatchAsync(this Watch watch) =>
#pragma warning restore VSTHRD200 // Use "Async" suffix for async methods
        (watcher, @event, cancellationToken) =>
        {
            watch(watcher, @event);
            return ValueTask.CompletedTask;
        };
}

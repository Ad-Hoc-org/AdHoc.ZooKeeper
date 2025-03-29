// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;

namespace AdHoc.ZooKeeper.Abstractions;

public interface IZooKeeperWatcher
    : IAsyncDisposable
{
    enum Types : int
    {
        Children = 1,
        Data = 2,
        Any = 3,
        Persistent = 4,
        RecursivePersistent = 5
    }

    Types Type { get; }

    ZooKeeperPath Path { get; }

    delegate ValueTask WatchAsync(IZooKeeperWatcher watcher, ZooKeeperEvent @event, CancellationToken cancellationToken);
    delegate void Watch(IZooKeeperWatcher watcher, ZooKeeperEvent @event);
}


public static partial class ZooKeeperWatchers
{
    public static bool IsHandling(this Types type, ZooKeeperEvent.Types eventType) => type switch
    {
        Types.Children => eventType is ZooKeeperEvent.Types.ChildrenChanged,
        Types.Data => eventType is ZooKeeperEvent.Types.DataChanged,
        _ => true
    };

    //internal static bool IsRecursive(this Types type) => type is Types.RecursivePersistent;
    //internal static bool IsPersistent(this Types type) => type is Types.Persistent or Types.RecursivePersistent;

    public static WatchAsync ToAsyncWatch(this Watch watch, [CallerArgumentExpression(nameof(watch))] string? watchExpression = null)
    {
        ArgumentNullException.ThrowIfNull(watch, watchExpression);
        return (watcher, @event, cancellationToken) =>
        {
            watch(watcher, @event);
            return ValueTask.CompletedTask;
        };
    }
}

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;

namespace AdHoc.ZooKeeper.Abstractions;

public interface IZooKeeperWatcher
    : IAsyncDisposable
{
    public readonly record struct Types(int Value)
    {
        private const int _Children = 1;
        public static readonly Types Children = _Children;

        private const int _Data = 2;
        public static readonly Types Data = _Data;

        private const int _Exist = 3;
        public static readonly Types Exist = _Exist;

        private const int _Persistent = 4;
        public static readonly Types Persistent = _Persistent;

        private const int _RecursivePersistent = 5;
        public static readonly Types RecursivePersistent = _RecursivePersistent;

        public bool IsRecursive => Value is _RecursivePersistent;
        public bool IsPersistent => Value is _Persistent or _RecursivePersistent;

        public bool IsHandling(ZooKeeperEvent.Types type) => Value switch
        {
            _Children => type is ZooKeeperEvent.Types.ChildrenChanged,
            _Data => type is ZooKeeperEvent.Types.DataChanged,
            _ => true
        };

        public static implicit operator Types(int value) => new(value);
        public static explicit operator int(Types type) => type.Value;
    }

    Types Type { get; }

    ZooKeeperPath Path { get; }

    public delegate ValueTask WatchAsync(IZooKeeperWatcher watcher, ZooKeeperEvent @event, CancellationToken cancellationToken);
    public delegate void Watch(IZooKeeperWatcher watcher, ZooKeeperEvent @event);
}


public static partial class Operations
{
    public static WatchAsync ToAsyncWatch(this Watch watch, [CallerArgumentExpression(nameof(watch))] string? watchName = null)
    {
        ArgumentNullException.ThrowIfNull(watch, watchName);
        return (watcher, @event, cancellationToken) =>
        {
            watch(watcher, @event);
            return ValueTask.CompletedTask;
        };
    }
}

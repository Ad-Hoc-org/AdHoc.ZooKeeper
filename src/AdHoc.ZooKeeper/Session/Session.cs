// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Frozen;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{
    internal Host Host { get; private set; }
    private readonly FrozenSet<Authentication> _authentications;
    private readonly TimeSpan _connectionTimeout;
    private readonly TimeSpan _sessionTimeout;
    private readonly bool _readOnly;

    internal Session(
        Host host,
        FrozenSet<Authentication> authentications,
        TimeSpan connectionTimeout,
        TimeSpan sessionTimeout,
        bool readOnly
    )
    {
        Host = host;
        _authentications = authentications;
        _connectionTimeout = connectionTimeout;
        _sessionTimeout = sessionTimeout;
        _readOnly = readOnly;

        _pending = new();
        _responding = new();
        _watchers = new();
        _receiving = Task.CompletedTask;
        _disposeSource = new();
    }


    internal async ValueTask ReconnectAsync(Host host, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync();
        try
        {
            Host = host;
            _disposeSource.TryReset();
            _tcpClient?.Dispose();
            _tcpClient = null;

            var receiving = _receiving;
            _receiving = Task.CompletedTask;
            await receiving;

            await EnsureSessionAsync(cancellationToken);
        }
        finally { _lock.Release(); }
    }

    internal async ValueTask CloseAsync()
    {
        DeregisterWatchers();

        await _disposeSource.CancelAsync();
        _tcpClient?.Dispose();
        _tcpClient = null;

        var receiving = _receiving;
        _receiving = Task.CompletedTask;
        if (receiving is not null)
            try { await receiving; } catch { }
    }


}

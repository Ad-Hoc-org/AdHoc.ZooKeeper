// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Frozen;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{
    private Host _host;
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
        _host = host;
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


    public async ValueTask ReconnectAsync(Host host)
    {
        await _lock.WaitAsync();
        try
        {
            _host = host;
            _tcpClient?.Dispose();
            var receiving = _receiving;
            _receiving = Task.CompletedTask;
            await receiving;
        }
        finally { _lock.Release(); }
    }


    public async ValueTask CloseAsync()
    {
        await _disposeSource.CancelAsync();
        var receiveTask = _receiving;
        _receiving = Task.CompletedTask;
        if (receiveTask is not null)
            try { await receiveTask; } catch { }
        _tcpClient?.Dispose();
        _tcpClient = null;
    }


}

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Frozen;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{
    private Host _host;
    private readonly FrozenSet<Authentication> _authentications;
    private readonly TimeSpan _connectionTimeout;
    private readonly TimeSpan _sessionTimeout;
    private readonly bool _readOnly;


    private readonly CancellationTokenSource _disposeSource = new();


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
    }


    internal async ValueTask ReconnectAsync(Host host, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            _host = host;
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;

            var receiving = _receiving;
            _receiving = Task.CompletedTask;
            try { await receiving; } catch { }

            await EnsureSessionAsync(cancellationToken);
        }
        finally { _writeLock.Release(); }
    }

    internal async ValueTask CloseAsync()
    {
        await _disposeSource.CancelAsync();

        var receiving = _receiving;
        _receiving = Task.CompletedTask;
        if (receiving is not null)
            try { await receiving; } catch { }

        var client = _tcpClient;
        _tcpClient = null;
        if (client is not null)
        {
            await _writeLock.WaitAsync();
            try
            {
                await WriteAsync(client.GetStream(),
                    WriteCloseSession,
                    CancellationToken.None
                );
            }
            catch { }
            finally { _writeLock.Release(); }
            client.Dispose();
        }
        _writeLock.Dispose();
    }


}

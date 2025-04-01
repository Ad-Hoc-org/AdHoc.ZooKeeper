// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{
    private ZooKeeperSession? _session;

    private long _lastInteractionTimestamp;
    private long _lastTransaction;

    private TcpClient? _tcpClient;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private Task _keepAlive = Task.CompletedTask;

    [MemberNotNullWhen(true, nameof(_tcpClient))]
    internal bool IsConnected =>
        _tcpClient is not null && _tcpClient.Connected
        && _tcpClient.Client.Poll(0, SelectMode.SelectWrite);

    private async ValueTask<NetworkStream> EnsureSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (IsConnected)
                return _tcpClient!.GetStream();

            var receiving = _receiving;
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            await receiving; // wait until all pending request are canceled

            _tcpClient = new() { SendTimeout = (int)_connectionTimeout.TotalMilliseconds };
            await _tcpClient.ConnectAsync(_host.Address, _host.Port, cancellationToken);
            var stream = _tcpClient.GetStream();

            _session = await SendAsync(stream,
                _session is not null && Stopwatch.GetElapsedTime(_lastInteractionTimestamp) < _session.Value.SessionTimeout
                    ? writer => WriteReconnectSession(writer, _session.Value, _lastTransaction)
                    : writer => WriteNewSession(writer, _sessionTimeout, _readOnly),
                data => ReadSession(data.Span),
                cancellationToken
            );

            foreach (var auth in _authentications)
                await SendAsync(stream, AddAuthenticationTransaction.Create(auth), cancellationToken);

            await ReregisterWatchersAsync(stream, cancellationToken);

            return stream;
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (ConnectionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ThrowConnection(ex);
            throw;
        }
    }

    [DoesNotReturn]
    private void ThrowConnection(Exception? exception = null)
    {
        if (_session is null)
            throw ZooKeeperException.CreateNoConnection(_host, exception);
        else if (Stopwatch.GetElapsedTime(_lastInteractionTimestamp) < _session.Value.SessionTimeout)
        {
            DispatchConnectionEvent(new ZooKeeperEvent(0, ZooKeeperStatus.ConnectionLoss, ZooKeeperEvent.Types.None, IZooKeeper.States.Disconnected, ZooKeeperPath.Empty));
            throw ZooKeeperException.CreateLostConnection(_host, _session.Value, _lastTransaction, _lastInteractionTimestamp, exception);
        }
        else
        {
            DispatchConnectionEvent(new ZooKeeperEvent(0, ZooKeeperStatus.ConnectionLoss, ZooKeeperEvent.Types.None, IZooKeeper.States.Expired, ZooKeeperPath.Empty));
            throw ZooKeeperException.CreateSessionExpired(_host, _session.Value, _lastTransaction, _lastInteractionTimestamp, exception);
        }
    }

    private Task KeepAliveAsync(CancellationToken cancellationToken)
    {
        if (!_keepAlive.IsCompleted)
            return _keepAlive;

        lock (_keepAlive)
        {
            if (!_keepAlive.IsCompleted)
                return _keepAlive;
            else
                return _keepAlive = PingIfNeededAsync(cancellationToken);
        }
        async Task PingIfNeededAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();

            var session = _session;
            if (session is null)
                return; // not connected

            var elapsed = Stopwatch.GetElapsedTime(_lastInteractionTimestamp);
            var timeout = session.Value.SessionTimeout;
            if (elapsed > timeout)
                return; // already expired

            while (IsConnected && elapsed < timeout / 2)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                await Task.Delay((timeout / 2) - elapsed, cancellationToken);
                // other request was made so we can idle
                elapsed = Stopwatch.GetElapsedTime(_lastInteractionTimestamp);
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            if (HasWatchers)
                await ExecutePingAsync(ZooKeeperPath.Root, Ping, cancellationToken);
        }
    }

}

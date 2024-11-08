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

    private long _lastInteractionTime;
    private long _lastTransaction;

    private TcpClient? _tcpClient;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private Task _keepAlive = Task.CompletedTask;

    [MemberNotNullWhen(true, nameof(_tcpClient))]
    internal bool IsConnected =>
        _tcpClient is not null && _tcpClient.Connected
        && !_tcpClient.Client.Poll(0, SelectMode.SelectError);

    private async ValueTask<NetworkStream> EnsureSessionAsync(CancellationToken cancellationToken)
    {
        Debug.Assert(_writeLock.CurrentCount == 0); // write should be sync
        try
        {
            if (IsConnected)
                return _tcpClient!.GetStream();

            var receiving = _receiving;
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            try
            {
                await receiving.WaitAsync(cancellationToken); // wait until all pending request are canceled
            }
            catch { }

            _tcpClient = new() { SendTimeout = (int)_connectionTimeout.TotalMilliseconds };
            await _tcpClient.ConnectAsync(_host.Address, _host.Port, cancellationToken);
            var stream = _tcpClient.GetStream();

            var lastInteractionTime = _lastInteractionTime;
            var session = await SendAsync(stream,
                _session is not null
                    ? writer => WriteReconnectSession(writer, _session.Value, _lastTransaction)
                    : writer => WriteNewSession(writer, _sessionTimeout, _readOnly),
                data => ReadSession(data.Span),
                cancellationToken
            );
            if (session is null)
            {
                _lastInteractionTime = lastInteractionTime; // responded but without active session
                ThrowConnection(new ConnectionException("Unable to start a session!"));
            }

            foreach (var auth in _authentications)
                await SendAsync(stream, AddAuthenticationTransaction.Create(auth), cancellationToken);
            await ReregisterWatchersAsync(stream, cancellationToken);

            _session = session;
            return stream;
        }
        catch (Exception ex) when (ex is not ConnectionException &&
            !(ex is OperationCanceledException cex && cex.CancellationToken == cancellationToken)
        )
        {
            ThrowConnection(ex);
            throw;
        }
    }

    [DoesNotReturn]
    private void ThrowConnection(Exception? exception = null)
    {
        _tcpClient?.Dispose();
        _tcpClient = null;
        if (_session is null)
            throw ZooKeeperException.CreateNoConnection(_host, exception);
        else if (Stopwatch.GetElapsedTime(_lastInteractionTime) < _session.Value.SessionTimeout)
        {
            DispatchConnectionEvent(new ZooKeeperEvent(0, ZooKeeperStatus.ConnectionLoss, ZooKeeperEvent.Types.None, IZooKeeper.States.Disconnected, ZooKeeperPath.Empty));
            throw ZooKeeperException.CreateLostConnection(_host, _session.Value, _lastTransaction, _lastInteractionTime, exception);
        }
        else
        {
            var session = _session.Value;
            _session = null;
            _lastInteractionTime = 0;
            DispatchConnectionEvent(new ZooKeeperEvent(0, ZooKeeperStatus.ConnectionLoss, ZooKeeperEvent.Types.None, IZooKeeper.States.Expired, ZooKeeperPath.Empty));
            throw ZooKeeperException.CreateSessionExpired(_host, session, _lastTransaction, _lastInteractionTime, exception);
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
            var session = _session;
            if (session is null)
                return; // not connected

            var elapsed = Stopwatch.GetElapsedTime(_lastInteractionTime);
            var timeout = session.Value.SessionTimeout;
            if (elapsed > timeout)
                return; // already expired

            while (IsConnected && elapsed < timeout / 2)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                await Task.Delay((timeout / 2) - elapsed, cancellationToken);
                // other request was made so we can idle
                elapsed = Stopwatch.GetElapsedTime(_lastInteractionTime);
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            if (HasWatchers)
                await ExecutePingAsync(ZooKeeperPath.Root, Ping, cancellationToken);
        }
    }

}

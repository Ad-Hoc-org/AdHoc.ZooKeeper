// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.Operations;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
    : IAsyncDisposable
{

    private ReadOnlyMemory<byte>? _session;
    private ReadOnlyMemory<byte>? _password;
    private long _lastInteractionTimestamp;
    private long _lastTransaction;

    private TimeSpan? _connectedSessionTimeout;
    public TimeSpan SessionTimeout => _connectedSessionTimeout ?? _sessionTimeout;

    private TcpClient? _tcpClient;
    private readonly SemaphoreSlim _lock = new(1, 1);


    private Task _keepAliveTask = Task.CompletedTask;


    [MemberNotNullWhen(true, nameof(_tcpClient))]
    private bool IsConnected =>
        _tcpClient is not null && _tcpClient.Connected
        && _tcpClient.Client.Poll(0, SelectMode.SelectWrite);


    private async ValueTask<NetworkStream> EnsureSessionAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
            return _tcpClient!.GetStream();
        try
        {
            if (IsConnected)
                return _tcpClient.GetStream();

            var receiveTask = _receiveTask;
            _tcpClient?.Dispose();
            await receiveTask; // wait until all pending request are canceled

            _tcpClient = new() { SendTimeout = (int)_connectionTimeout.TotalMilliseconds };
            await _tcpClient.ConnectAsync(_host.Address, _host.Port);
            var stream = _tcpClient.GetStream();

            var session = await SendAsync(stream,
                writer => WriteNewSession(writer, _sessionTimeout, _readOnly),
                data => ReadSession(data.Span),
                cancellationToken
            );
            _connectedSessionTimeout = session.SessionTimeout;
            _lastInteractionTimestamp = Stopwatch.GetTimestamp();
            _session = session.Session;
            _password = session.Password;

            //var operation = ConnectOperation.Reconnect(_sessionTimeout, _readOnly, _lastTransaction, _session.Value, _password!.Value);
            //var result = await SendAsync(stream, operation.WriteRequest, data => operation.ReadResponse(data.Span), cancellationToken);
            //_connectedSessionTimeout = result.SessionTimeout;
            //_lastInteractionTimestamp = Stopwatch.GetTimestamp();
            //_session = result.Session;
            //_password = result.Password;
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
        else
            throw ZooKeeperException.CreateLostConnection(_host, _session.Value, _password!.Value, _lastTransaction, _lastInteractionTimestamp, exception);
    }

    private async Task KeepAliveAsync(CancellationToken cancellationToken)
    {
        var keepAliveTask = _keepAliveTask;
        if (!keepAliveTask.IsCompleted)
        {
            await keepAliveTask;
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_keepAliveTask.IsCompleted)
            {
                keepAliveTask = _keepAliveTask;
            }
            else
            {
                _keepAliveTask = keepAliveTask = Task.Run(async () =>
                {
                    if (_connectedSessionTimeout is null)
                        return; // not connected

                    var elapsed = Stopwatch.GetElapsedTime(_lastInteractionTimestamp);
                    if (elapsed > _connectedSessionTimeout)
                        return; // already expired

                    while (elapsed < _connectedSessionTimeout / 2)
                    {
                        await Task.Delay((_connectedSessionTimeout.Value / 2) - elapsed, cancellationToken);
                        // other request was made so we can wait longer
                        elapsed = Stopwatch.GetElapsedTime(_lastInteractionTimestamp);
                    }

                    await ExecutePingAsync(ZooKeeperPath.Root, Ping, cancellationToken);
                }, cancellationToken);
            }
        }
        finally { _lock.Release(); }
        await keepAliveTask;
    }

}

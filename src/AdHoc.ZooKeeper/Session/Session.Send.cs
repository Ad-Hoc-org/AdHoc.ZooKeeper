// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.Operations;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{

    private int _previousRequest;


    private Task<TResult> SendAsync<TResult>(
        NetworkStream stream,
        IZooKeeperOperation<TResult> operation,
        CancellationToken cancellationToken
    ) => SendAsync(
        stream,
        writer => operation.WriteRequest(new ZooKeeperContext(
            ZooKeeperPath.Root,
            writer,
            op => GetRequest(op, ref _previousRequest),
            (_, _, _) => throw new InvalidOperationException()
        )),
        data => operation.ReadResponse(Response.ToTransaction(data.Span, ZooKeeperPath.Root), null),
        cancellationToken
    );

    private async Task<TResult> SendAsync<TResult>(
        NetworkStream stream,
        Action<IBufferWriter<byte>> write,
        Func<ReadOnlyMemory<byte>, TResult> read,
        CancellationToken cancellationToken
    )
    {
#if DEBUG
        if (!_pending.IsEmpty)
            throw new InvalidOperationException();
#endif
        var pipeWriter = PipeWriter.Create(stream);
        write(pipeWriter);
        _lastInteractionTimestamp = Stopwatch.GetTimestamp();
        await pipeWriter.FlushAsync(cancellationToken);

        using var response = await ReadAsync(stream, null, cancellationToken);
        return read(response._memory);
    }

    private async Task<Response> ReadAsync(
        NetworkStream stream,
        IMemoryOwner<byte>? owner,
        CancellationToken cancellationToken
    )
    {
        try
        {
            Memory<byte> buffer;
            if (owner is null || owner.Memory.Length < MinimalResponseLength)
            {
                owner?.Dispose();
                owner = MemoryPool<byte>.Shared.Rent(MinimalResponseLength);
                buffer = owner.Memory;
            }
            else
                buffer = owner.Memory;

            var bytes = await stream.ReadAsync(buffer.Slice(0, LengthSize), cancellationToken);
            if (bytes == 0) ThrowConnection();
            else if (bytes != LengthSize)
                throw new ZooKeeperException($"Invalid ZooKeeper response!");

            var length = ReadInt32(buffer.Span.Slice(0, LengthSize));
            if (length < MinimalResponseLength)
                throw new ZooKeeperException($"Invalid ZooKeeper response!");

            if (length > buffer.Length)
            {
                owner.Dispose();
                owner = MemoryPool<byte>.Shared.Rent(length);
                buffer = owner.Memory;
            }

            var response = buffer.Slice(0, length);
            bytes = await stream.ReadAsync(response, cancellationToken);
            if (bytes == 0) ThrowConnection();
            else if (bytes != length)
                throw new ZooKeeperException($"Invalid ZooKeeper response!");

            return new Response(owner, response);
        }
        catch (IOException ex)
        {
            ThrowConnection(ex);
            throw;
        }
    }

    private readonly struct Response
        : IDisposable
    {
        internal readonly IMemoryOwner<byte> _owner;

        internal readonly ReadOnlyMemory<byte> _memory;

        internal Response(IMemoryOwner<byte> owner, ReadOnlyMemory<byte> memory)
        {
            _owner = owner;
            _memory = memory;
        }

        internal ZooKeeperResponse ToTransaction(ZooKeeperPath root) =>
            ToTransaction(_memory.Span, root);

        internal static ZooKeeperResponse ToTransaction(ReadOnlySpan<byte> data, ZooKeeperPath root) => new(
            root,
            request: ReadInt32(data),
            transaction: ReadInt64(data.Slice(RequestSize)),
            status: (ZooKeeperStatus)ReadInt32(data.Slice(RequestSize + TransactionSize)),
            data: data.Slice(RequestSize + TransactionSize + StatusSize)
        );

        public void Dispose() =>
            _owner.Dispose();
    }


    private async Task<(TResult?, bool)> DispatchAsync<TResult>(
        IZooKeeperOperation<TResult> operation,
        ZooKeeperPath root,
        Func<NetworkStream, TaskCompletionSource<Response>, CancellationToken, ValueTask<(int?, Watcher?)>> writeAsync,
        CancellationToken cancellationToken
    )
    {
        await _lock.WaitAsync(cancellationToken);
        bool released = false;
        TaskCompletionSource<Response> pending = new();
        int? request = null;
        Task<TResult>? receive = null;
        CancellationTokenRegistration registration = default;
        try
        {
            var stream = await EnsureSessionAsync(cancellationToken);
            (request, var watcher) = await writeAsync(stream, pending, cancellationToken);
            if (request is null)
                return (default, false);

            registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
            receive = ReceiveAsync(operation, root, stream, pending.Task, watcher, cancellationToken);
            _responding[request.Value] = receive;

            // release lock after writing and task management is done
            _lock.Release();
            released = true;

            var result = await receive;
            _responding.TryRemove(KeyValuePair.Create<int, Task>(request.Value, receive));
            _pending.TryRemove(KeyValuePair.Create(request.Value, pending));
            return (result, true);
        }
        catch (Exception ex)
        {
            bool canceled = ex is OperationCanceledException canceledEx && canceledEx.CancellationToken == cancellationToken;

            if (request is not null)
            {
                if (receive is not null)
                    _responding.TryRemove(KeyValuePair.Create<int, Task>(request.Value, receive));
                if (_pending.TryRemove(KeyValuePair.Create(request.Value, pending)))
                    if (canceled)
                        pending.TrySetCanceled(cancellationToken);
                    else
                        pending.TrySetException(ex);
            }

            throw;
        }
        finally
        {
            await registration.DisposeAsync();
            if (!released)
                _lock.Release();
        }
    }

}

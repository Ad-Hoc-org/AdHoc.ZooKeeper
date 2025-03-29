// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using static AdHoc.ZooKeeper.Abstractions.IZooKeeperWatcher;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper;
internal sealed partial class Session
{

    private int _previousRequest;

    public int GetRequest(ZooKeeperOperations operation) =>
        ZooKeeperTransactions.GetRequest(operation, ref _previousRequest);


    private async Task<TResponse> SendAsync<TResponse>(
        NetworkStream stream,
        Action<IBufferWriter<byte>> write,
        Func<ReadOnlyMemory<byte>, TResponse> read,
        CancellationToken cancellationToken
    )
    {
        Debug.Assert(_pending.IsEmpty);

        await WriteAsync(stream, write, cancellationToken);
        using var response = await ReadAsync(stream, null, cancellationToken);
        return read(response._memory);
    }

    private async ValueTask WriteAsync(
        NetworkStream stream,
        Action<IBufferWriter<byte>> write,
        CancellationToken cancellationToken
    )
    {
        var pipeWriter = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
        write(pipeWriter);
        _lastInteractionTimestamp = Stopwatch.GetTimestamp();
        await pipeWriter.FlushAsync(cancellationToken);
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

    private Task<TResponse> SendAsync<TResponse>(
        NetworkStream stream,
        IZooKeeperTransaction<TResponse> transaction,
        CancellationToken cancellationToken
    )
        where TResponse : IZooKeeperResponse
    {
        int request = GetRequest(transaction.Operation);
        Debug.Assert(request < 0); // only connections request are sync allowed
        return SendAsync(
            stream,
            writer => WriteTransaction(
                writer,
                ZooKeeperPath.Root,
                request,
                transaction,
                (_, _, _) => throw new InvalidOperationException()
            ),
            data => ReadTransaction(data, ZooKeeperPath.Root, transaction, null),
            cancellationToken
        );
    }

    private static void WriteTransaction<TResponse>(
        IBufferWriter<byte> writer,
        ZooKeeperPath root,
        int request,
        IZooKeeperTransaction<TResponse> transaction,
        Action<ZooKeeperPath, Types, WatchAsync> registerWatcher
    ) where TResponse : IZooKeeperResponse
    {
        var buffer = writer.GetSpan(RequestHeaderSize + transaction.GetMaxRequestSize(root));

        Write(buffer.Slice(LengthSize), request);
        Write(buffer.Slice(LengthSize + RequestSize), (int)transaction.Operation);

        int size = transaction.WriteRequest(new(
            root,
            buffer.Slice(LengthSize + RequestSize + OperationSize),
            registerWatcher
        ));

        Write(buffer, LengthSize + RequestSize + size);

        writer.Advance(RequestHeaderSize + size);
    }

    private TResponse ReadTransaction<TResponse>(
        ReadOnlyMemory<byte> data,
        ZooKeeperPath root,
        IZooKeeperTransaction<TResponse> transaction,
        IZooKeeperWatcher? watcher
    )
        where TResponse : IZooKeeperResponse
    {
        var context = Response.ToContext(data.Span, root, watcher);
        _lastTransaction = context.Transaction;
        return transaction.ReadResponse(context);
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

        internal ZooKeeperReadContext ToContext(ZooKeeperPath root, IZooKeeperWatcher? watcher) =>
            ToContext(_memory.Span, root, watcher);

        internal static ZooKeeperReadContext ToContext(ReadOnlySpan<byte> data, ZooKeeperPath root, IZooKeeperWatcher? watcher) => new(
            root,
            request: ReadInt32(data),
            transaction: ReadInt64(data.Slice(RequestSize)),
            status: (ZooKeeperStatus)ReadInt32(data.Slice(RequestSize + TransactionSize)),
            data: data.Slice(RequestSize + TransactionSize + StatusSize),
            watcher: watcher
        );

        public void Dispose() =>
            _owner.Dispose();
    }
}

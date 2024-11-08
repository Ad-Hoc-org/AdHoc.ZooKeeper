// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransaction;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed partial record ZooKeeperTransaction
    : IZooKeeperTransaction<Response>
{
    public const int HeaderSize = Int32Size + BooleanSize + Int32Size;

    public ZooKeeperOperations Operation => ZooKeeperOperations.Transaction;

    public IReadOnlyList<IZooKeeperTransaction> Transactions { get; }

    private ZooKeeperTransaction(ImmutableArray<IZooKeeperTransaction> transactions) =>
        Transactions = transactions;

    public static ZooKeeperTransaction Create(params IEnumerable<IZooKeeperTransaction> transactions) =>
        new(transactions?.ToImmutableArray() ?? []);


    public int GetMaxRequestSize(in ZooKeeperPath root)
    {
        int size = LengthSize + (HeaderSize * Transactions.Count);
        foreach (var transaction in Transactions)
            size += transaction.GetMaxRequestSize(root);
        return size;
    }

    public int WriteRequest(in ZooKeeperWriteContext context)
    {
        var buffer = context.Buffer;
        int size = 0;
        foreach (var transaction in Transactions)
        {
            size += WriteHeader(buffer.Slice(size), transaction.Operation);
            size += transaction.WriteRequest(context.Slice(size));
        }
        size += WriteHeader(buffer.Slice(size));
        return size;
    }

    private static int WriteHeader(in Span<byte> buffer, ZooKeeperOperations operation)
    {
        Write(buffer, (int)operation);
        buffer[Int32Size] = 0;
        Write(buffer.Slice(Int32Size + BooleanSize), -1);
        return HeaderSize;
    }

    private static int WriteHeader(in Span<byte> buffer)
    {
        Write(buffer, -1);
        buffer[Int32Size] = 1;
        Write(buffer.Slice(Int32Size + BooleanSize), -1);
        return HeaderSize;
    }

    public Response ReadResponse(in ZooKeeperReadContext context, out int size)
    {
        Debug.Assert(context.Operation == Operation);

        context.Status.ThrowIfError();
        size = 0;

        ZooKeeperOperations? operation;
        ZooKeeperStatus status;
        Error? error = null;
        var responses = new IZooKeeperResponse[Transactions.Count];
        int responseSize;
        for (var i = 0; i < Transactions.Count; i++)
        {
            var transaction = Transactions[i];
            (operation, status) = ReadHeader(context.Data.Slice(size), ref size);
            Debug.Assert(operation is not null);

            if (operation is ZooKeeperOperations.Error)
            {
                status = (ZooKeeperStatus)ReadInt32(context.Data.Slice(size));
                size += Int32Size;
                if (error is null && status is not ZooKeeperStatus.Ok)
                    error = new(status, i, transaction);
            }
            else
            {
                responses[i] = transaction.ReadResponse(context.Slice(size, operation!.Value, status), out responseSize);
                size += responseSize;
            }
        }

        (operation, status) = ReadHeader(context.Data.Slice(size), ref size);
        Debug.Assert(operation is null);

        return error is null ? new(context.Transaction, responses, default)
            : new(context.Transaction, default, error);
    }

    private static (ZooKeeperOperations? Operation, ZooKeeperStatus Status) ReadHeader(in ReadOnlySpan<byte> data, ref int offset)
    {
        if (data[Int32Size] == 1)
            return default;

        offset += HeaderSize;
        return ((ZooKeeperOperations)ReadInt32(data), (ZooKeeperStatus)ReadInt32(data.Slice(Int32Size + BooleanSize)));
    }


    public readonly record struct Response(
        long Transaction,
        ReadOnlyMemory<IZooKeeperResponse>? Responses,
        Error? Error
    ) : IZooKeeperResponse
    {
        [MemberNotNullWhen(false, nameof(Responses))]
        [MemberNotNullWhen(true, nameof(Error))]
        public bool HasError => Error is not null;
    }

    public readonly record struct Error(
        ZooKeeperStatus Status,
        int Index,
        IZooKeeperTransaction Transaction
    );
}

public static partial class ZooKeeperTransactions
{
    public static Task<Response> TransactAsync(
        this IZooKeeper zooKeeper,
        IEnumerable<IZooKeeperTransaction> transactions,
        CancellationToken cancellationToken
    ) =>
        zooKeeper.ExecuteAsync(ZooKeeperTransaction.Create(transactions), cancellationToken);
}

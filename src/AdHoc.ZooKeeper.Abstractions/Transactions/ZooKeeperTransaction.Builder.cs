// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransaction;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed partial record ZooKeeperTransaction
{
    public sealed class Builder(IZooKeeper zooKeeper)
    {
        private readonly List<IZooKeeperTransaction> _transactions = new();

        public Builder AddTransaction<TResponse>(IZooKeeperTransaction<TResponse> transaction)
            where TResponse : IZooKeeperResponse
        {
            _transactions.Add(transaction);
            return this;
        }

        public Task<Response> CommitAsync(CancellationToken cancellationToken) =>
            zooKeeper.TransactAsync(_transactions, cancellationToken);
    }
}

public static partial class ZooKeeperTransactions
{
    public static Builder StartTransaction(this IZooKeeper zooKeeper)
    {
        ArgumentNullException.ThrowIfNull(zooKeeper);
        return new Builder(zooKeeper);
    }
}

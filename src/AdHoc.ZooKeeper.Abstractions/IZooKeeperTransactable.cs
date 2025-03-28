// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public interface IZooKeeperTransactable
{
    Task<TResponse> ProcessAsync<TResponse>(IZooKeeperTransaction<TResponse> transaction, CancellationToken cancellationToken)
        where TResponse : IZooKeeperResponse;
}

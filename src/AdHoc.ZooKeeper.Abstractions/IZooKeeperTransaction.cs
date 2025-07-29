// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;

/// <summary>
/// Represents a ZooKeeper transaction interface.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IZooKeeperTransaction<out TResponse>
    : IZooKeeperTransaction
    where TResponse : IZooKeeperResponse
{
    IZooKeeperResponse IZooKeeperTransaction.ReadResponse(in ZooKeeperReadContext context, out int size) =>
        ReadResponse(context, out size);
    new TResponse ReadResponse(in ZooKeeperReadContext context, out int size);
}

public interface IZooKeeperTransaction
{
    /// <summary>
    /// Gets the ZooKeeper operation associated with this transaction.
    /// </summary>
    ZooKeeperOperations Operation { get; }

    /// <summary>
    /// Gets the maximum request size for the specified root path.
    /// </summary>
    /// <param name="root">The root path.</param>
    /// <returns>The maximum request size.</returns>
    /// <remarks>This method should only calculate the data size, without header.</remarks>
    int GetMaxRequestSize(in ZooKeeperPath root);

    /// <summary>
    /// Writes the request data to the specified context.
    /// </summary>
    /// <param name="context">The write context.</param>
    /// <returns>The number of bytes written.</returns>
    /// <remarks>This method should only write the request data, not headers.</remarks>
    int WriteRequest(in ZooKeeperWriteContext context);

    /// <summary>
    /// Reads the response from the specified context.
    /// </summary>
    /// <param name="context">The read context.</param>
    /// <returns>The response.</returns>
    IZooKeeperResponse ReadResponse(in ZooKeeperReadContext context, out int size);
}

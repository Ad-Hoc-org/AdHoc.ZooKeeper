// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public readonly ref struct ZooKeeperResponse
{

    public ZooKeeperPath Root { get; }

    public int Request { get; }
    public long Transaction { get; }
    public ZooKeeperStatus Status { get; }
    public ReadOnlySpan<byte> Data { get; }

    public ZooKeeperResponse(
        ZooKeeperPath root,
        int request,
        long transaction,
        ZooKeeperStatus status,
        ReadOnlySpan<byte> data
    )
    {
        Root = root;
        Request = request;
        Transaction = transaction;
        Status = status;
        Data = data;
    }

    public void ThrowIfError()
    {
        if (Status != ZooKeeperStatus.Ok)
            throw ZooKeeperException.CreateResponseError(Status);
    }
}

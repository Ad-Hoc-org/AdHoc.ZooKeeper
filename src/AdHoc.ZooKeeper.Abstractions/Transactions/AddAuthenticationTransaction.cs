// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Text;
using static AdHoc.ZooKeeper.Abstractions.AddAuthenticationTransaction;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record AddAuthenticationTransaction
    : IZooKeeperTransaction<Response>
{
    public const int Request = -4;

    public ZooKeeperOperations Operation => ZooKeeperOperations.Authentication;

    public Authentication Authentication { get; }


    private AddAuthenticationTransaction(Authentication authentication) =>
        Authentication = authentication;

    public static AddAuthenticationTransaction Create(Authentication authentication) =>
        new(authentication);


    public int GetMaxRequestSize(in ZooKeeperPath path) =>
        Int32Size // type
        + LengthSize + Encoding.UTF8.GetMaxByteCount(Authentication.Scheme.Length)
        + LengthSize + Authentication.Data.Length;

    public int WriteRequest(in ZooKeeperWriteContext context)
    {
        var buffer = context.Buffer;

        int size = Write(buffer, 0); // type

        size += Write(buffer.Slice(size), Authentication.Scheme);
        size += Write(buffer.Slice(size), Authentication.Data.Span);

        return size;
    }

    public Response ReadResponse(in ZooKeeperReadContext context)
    {
        Debug.Assert(context.Request == Request);
        context.Status.ThrowIfError();
        return new(
            context.Transaction
        );
    }

    public readonly record struct Response(long Transaction) : IZooKeeperResponse;
}

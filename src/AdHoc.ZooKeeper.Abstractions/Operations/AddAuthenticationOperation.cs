// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using static AdHoc.ZooKeeper.Abstractions.AddAuthenticationOperation;
using static AdHoc.ZooKeeper.Abstractions.Operations;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record AddAuthenticationOperation
    : IZooKeeperOperation<Result>
{
    private const int _Request = -4;

    private static readonly ReadOnlyMemory<byte> _RequestBytes = new byte[] { 255, 255, 255, 252 };
    private static readonly ReadOnlyMemory<byte> _Operation = new byte[] { 0, 0, 0, 100 };

    public Authentication Authentication { get; }

    private AddAuthenticationOperation(
        Authentication authentication
    ) =>
        Authentication = authentication;

    public void WriteRequest(in ZooKeeperContext context)
    {
        var writer = context.Writer;
        var buffer = writer.GetSpan(RequestHeaderSize
            + Int32Size
            + LengthSize + Authentication.Scheme.Length
            + LengthSize + Authentication.Data.Length
        );
        int size = LengthSize;

        _RequestBytes.Span.CopyTo(buffer.Slice(size));
        size += RequestSize;

        _Operation.Span.CopyTo(buffer.Slice(size));
        size += OperationSize;

        size += Write(buffer.Slice(size), 0); // type

        size += Write(buffer.Slice(size), Authentication.Scheme);

        size += Write(buffer.Slice(size), Authentication.Data.Span);

        Write(buffer, size - LengthSize);
        writer.Advance(size);
    }

    public Result ReadResponse(in ZooKeeperResponse response, IZooKeeperWatcher? watcher)
    {
        response.ThrowIfError();
        return new Result(response.Transaction);
    }


    public static AddAuthenticationOperation Create(
        Authentication authentication
    )
    {
        if (authentication == default)
            throw new ArgumentNullException(nameof(authentication));
        ArgumentNullException.ThrowIfNull(authentication.Scheme);
        return new AddAuthenticationOperation(authentication);
    }


    public readonly record struct Result(long Transaction);
}

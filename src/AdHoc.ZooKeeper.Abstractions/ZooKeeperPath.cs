// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Text;
using static AdHoc.ZooKeeper.Abstractions.Operations;

namespace AdHoc.ZooKeeper.Abstractions;
public readonly record struct ZooKeeperPath
{
    public static ZooKeeperPath Root { get; } = "/";

    public string Value { get; }

    public bool IsAbsolute => Value?.StartsWith('/') ?? false;

    public ZooKeeperPath() => Value = "/";

    public ZooKeeperPath(string value) =>
        Value = value;

    public ZooKeeperPath Absolute() =>
        Value is null || Value.Length == 0 ? Root
        : Value[0] == '/' ? this : '/' + Value;

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Value);
        if (Value.Length == 0)
            return;

        string? reason = null;
        ReadOnlySpan<char> span = Value.AsSpan();
        char last = span[0];
        char c;
        int length = span.Length;
        for (int i = 1; i < length; last = span[i++])
        {
            c = span[i];

            if (c is '\0'
                or <= '\u001f'
                or (>= '\u007f' and <= '\u009F')
                or (>= '\ud800' and <= '\uf8ff')
                or >= '\ufff0'
            )
            {
                reason = $"invalid character '{c}' at {i}";
                break;
            }

            if (c == '/' && last == '/')
            {
                reason = $"empty node name specified {i}";
                break;
            }

            if (c == '.')
            {
                if (last == '.')
                {
                    if (span[i - 2] == '/' && ((i + 1 == span.Length) || span[i + 1] == '/'))
                    {
                        reason = $"relative paths not allowed: {i}";
                        break;
                    }
                }
                else
                {
                    if (span[i - 1] == '/' && ((i + 1 == span.Length) || span[i + 1] == '/'))
                    {
                        reason = $"relative paths not allowed: {i}";
                        break;
                    }
                }
            }
        }

        if (reason is not null)
            throw new ArgumentException($"Invalid path '{Value}' caused by " + reason);
    }


    public int GetMaxSize(ZooKeeperPath root = default) =>
        LengthSize
        + (Value is null ? 0 : Encoding.UTF8.GetMaxByteCount(Value.Length))
        + (root.Value is null ? 0 : Encoding.UTF8.GetMaxByteCount(root.Value.Length))
        + 2; // root '/' if missing and join '/'

    public int Write(Span<byte> destination, ZooKeeperPath root = default)
    {
        var value = Value is null ? [] : Value.AsSpan();
        var rootValue = root.Value is null ? [] : root.Value.AsSpan();

        int size = LengthSize;
        if (rootValue.Length == 0 || (rootValue.Length == 1 && rootValue[0] == '/'))
        {
            if (value.Length == 0)
            {
                Operations.Write(destination, 1);
                destination[LengthSize] = (byte)'/';
                return LengthSize + 1;
            }
            else
            {
                if (value[0] != '/')
                    destination[size++] = (byte)'/';
                size += Encoding.UTF8.GetBytes(value, destination.Slice(size));
                Operations.Write(destination, size - LengthSize);
                return size;
            }
        }

        if (rootValue[0] != '/')
            destination[size++] = (byte)'/';
        size += Encoding.UTF8.GetBytes(rootValue, destination.Slice(size));

        if (value.Length == 0 || (value.Length == 1 && value[0] == '/'))
        {
            Operations.Write(destination, size - LengthSize);
            return size;
        }

        if (rootValue[rootValue.Length - 1] != '/')
        {
            if (value[0] != '/')
                destination[size++] = (byte)'/';
        }
        else if (value[0] == '/')
            value = value.Slice(1);

        size += Encoding.UTF8.GetBytes(value, destination.Slice(size));

        Operations.Write(destination, size - LengthSize);
        return size;
    }


    public static ZooKeeperPath Read(ReadOnlySpan<byte> source, out int size)
    {
        int length = ReadInt32(source);
        size = LengthSize + length;
        return Encoding.UTF8.GetString(
            source.Slice(
                LengthSize,
                length
            )
        );
    }


    /// <summary>
    /// Combines multiple <see cref="ZooKeeperPath"/> instances into a single path.
    /// </summary>
    /// <param name="paths">An array of <see cref="ZooKeeperPath"/> instances to combine.</param>
    /// <returns>A single combined <see cref="ZooKeeperPath"/>.</returns>
    /// <remarks>
    /// This method can be used for multiple concatenations of <see cref="ZooKeeperPath"/> instances.
    /// </remarks>
    public static ZooKeeperPath Combine(params ReadOnlySpan<ZooKeeperPath> paths)
    {
        StringBuilder builder = new();
        bool hasSeparator = false;
        foreach (var path in paths)
        {
            if (path.Value is null || path.Value.Length == 0)
            {
                hasSeparator = false;
                continue;
            }

            var span = path.Value.AsSpan();
            if (builder.Length == 0)
                builder.Append(span);
            else if (hasSeparator)
                builder.Append(span[0] == '/' ? span.Slice(1) : span);
            else
            {
                if (span[0] == '/')
                    builder.Append(span);
                else
                    builder.Append('/')
                        .Append(span);
            }
            hasSeparator = span[span.Length - 1] == '/';
        }
        return builder.ToString();
    }


    public override string? ToString() => Value;


    /// <summary>
    /// Combines two <see cref="ZooKeeperPath"/> instances into a single path.
    /// </summary>
    /// <param name="left">The first <see cref="ZooKeeperPath"/> instance.</param>
    /// <param name="right">The second <see cref="ZooKeeperPath"/> instance.</param>
    /// <returns>A single combined <see cref="ZooKeeperPath"/>.</returns>
    /// <remarks>
    /// This operator can be used for combining two <see cref="ZooKeeperPath"/> instances.
    /// For multiple concatenations, consider using the <see cref="Combine"/> method.
    /// </remarks>
    /// <seealso cref="Combine(ReadOnlySpan{ZooKeeperPath})"/>
    public static ZooKeeperPath operator +(ZooKeeperPath left, ZooKeeperPath right) =>
        Combine(left, right);


    public static implicit operator ZooKeeperPath(string value) => new(value);
    public static implicit operator string(ZooKeeperPath path) => path.Value;
}

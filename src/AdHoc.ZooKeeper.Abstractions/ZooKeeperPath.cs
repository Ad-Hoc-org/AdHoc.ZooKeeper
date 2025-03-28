// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public readonly struct ZooKeeperPath
    : IEquatable<ZooKeeperPath>
{
    public const char Separator = '/';
    public static ZooKeeperPath Root { get; } = "/";
    public static ZooKeeperPath Empty { get; } = ReadOnlyMemory<char>.Empty;


    public ReadOnlyMemory<char> Memory { get; }


    public ZooKeeperPath(ReadOnlyMemory<char> path) => Memory = path;
    public ZooKeeperPath(string path) : this(path.AsMemory()) { }


    public bool IsEmpty => Memory.IsEmpty;

    public int Length => Memory.Length;


    public bool IsRoot => Memory.Length == 1 && Memory.Span[0] == Separator;

    public bool IsAbsolute => !Memory.IsEmpty && Memory.Span[0] == Separator;

    public ZooKeeperPath Absolute
    {
        get
        {
            if (IsEmpty)
                return Root;
            if (Memory.Span[0] == Separator)
                return this;
            char[] chars = new char[Memory.Length + 1];
            chars[0] = Separator;
            Memory.Span.CopyTo(chars.AsSpan(1));
            return new ReadOnlyMemory<char>(chars);
        }
    }

    /// <summary>
    /// Converts the current <see cref="ZooKeeperPath"/> to an absolute path if it is not already.
    /// If the path is not absolute, it uses the specified root to make it absolute.
    /// </summary>
    /// <param name="root">The root <see cref="ZooKeeperPath"/> to use if the current path is not absolute.</param>
    /// <returns>An absolute <see cref="ZooKeeperPath"/>.</returns>
    public ZooKeeperPath ToAbsolute(ZooKeeperPath root) =>
        IsAbsolute ? this : Combine(root.Absolute, this);


    public bool IsContainer =>
        !Memory.IsEmpty && Memory.Span[Memory.Length - 1] == Separator;

    public ZooKeeperPath Container
    {
        get
        {
            int length = Memory.Length;
            if (length == 0)
                return Empty;
            var span = Memory.Span;
            if (length == 1 && span[0] == Separator)
                return Root;

            int index = span.Slice(0, length - 1).LastIndexOf(Separator);
            if (index == -1)
                return Empty;
            if (index == 0)
                return Root;

            return Memory.Slice(0, index + 1);
        }
    }


    public int GetMaxBufferSize(ZooKeeperPath root = default) =>
        LengthSize + Encoding.UTF8.GetMaxByteCount(root.Memory.Length) + 1 // separator if needed
            + Encoding.UTF8.GetMaxByteCount(Memory.Length);

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

    public int Write(Span<byte> destination)
    {
        int size = Encoding.UTF8.GetBytes(Memory.Span, destination.Slice(LengthSize));
        return ZooKeeperTransactions.Write(destination, size) + size;
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
        int length = 0;
        int size = paths.Length;
        if (size == 0)
            return Empty;
        if (size == 1)
            return paths[0];
        if (size == 2)
        {
            var first = paths[0];
            if (first.IsEmpty || first.IsRoot)
                return paths[1].Absolute;
        }

        bool endWithSeparator = false;
        int len;
        ReadOnlyMemory<char> mem;
        ReadOnlySpan<char> span;
        for (int i = 0; i < size; i++)
        {
            mem = paths[i].Memory;
            len = mem.Length;
            if (len == 0)
            {
                if (!endWithSeparator && i > 0)
                    length++;
                endWithSeparator = false;
                continue;
            }

            span = mem.Span;
            if (span[0] == Separator)
            {
                if (len == 1)
                {
                    if (i == 0 || !endWithSeparator || paths[i - 1].IsRoot)
                        length++;
                    endWithSeparator = true;
                    continue;
                }

                if (endWithSeparator)
                    length--;
            }
            else if (!endWithSeparator && i > 0)
                length++;

            length += len;
            endWithSeparator = span[len - 1] == Separator;
        }

        endWithSeparator = false;
        var chars = new char[length];
        int pos = 0;
        for (int i = 0; i < size; i++)
        {
            mem = paths[i].Memory;
            len = mem.Length;
            if (len == 0)
            {
                if (!endWithSeparator && i > 0)
                    chars[pos++] = Separator;
                endWithSeparator = false;
                continue;
            }

            span = mem.Span;
            if (span[0] == Separator)
            {
                if (len == 1)
                {
                    if (i == 0 || !endWithSeparator || paths[i - 1].IsRoot)
                        chars[pos++] = Separator;
                    endWithSeparator = true;
                    continue;
                }

                if (endWithSeparator)
                {
                    span = span.Slice(1);
                    len--;
                }
                span.CopyTo(chars.AsSpan(pos));
                pos += len;
            }
            else
            {
                if (!endWithSeparator && i > 0)
                    chars[pos++] = Separator;

                span.CopyTo(chars.AsSpan(pos));
                pos += len;
            }

            endWithSeparator = span[len - 1] == Separator;
        }

        return new ReadOnlyMemory<char>(chars);
    }

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


    public static implicit operator ZooKeeperPath(ReadOnlyMemory<char> path) => new(path);
    public static implicit operator ZooKeeperPath(ReadOnlySpan<char> path) => new(path.ToArray());
    public static implicit operator ZooKeeperPath(string path) => new(path);


    public override int GetHashCode() => Memory.Length;
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is ZooKeeperPath path && Equals(path);
    public bool Equals(ZooKeeperPath other) =>
        Memory.Length == other.Memory.Length
        && (Memory.IsEmpty || Memory.Span.SequenceEqual(other.Memory.Span));

    public static bool operator ==(ZooKeeperPath left, ZooKeeperPath right) => left.Equals(right);
    public static bool operator !=(ZooKeeperPath left, ZooKeeperPath right) => !left.Equals(right);

    public override string ToString() => Memory.ToString();
}

public static class ZooKeepers
{
    public static ZooKeeperPath Combine(this ZooKeeperPath path, params ReadOnlySpan<ZooKeeperPath> paths) =>
        ZooKeeperPath.Combine([path, .. paths]);


    public static void ThrowIfEmpty(this ZooKeeperPath path, [CallerArgumentExpression(nameof(path))] string? pathExpression = null)
    {
        if (path.IsEmpty)
            throw new ArgumentException($"Path '{pathExpression}' can't be empty.", nameof(pathExpression));
    }

    public static void ThrowIfInvalid(this ZooKeeperPath path, [CallerArgumentExpression(nameof(path))] string? pathExpression = null)
    {
        if (path.IsEmpty)
            return;

        ReadOnlySpan<char> span = path.Memory.Span;
        int length = span.Length;
        char last = span[0];
        char c;
        for (int i = 1; i < length; last = span[i++])
        {
            c = span[i];

            if (c is '\0'
                or <= '\u001f'
                or (>= '\u007f' and <= '\u009F')
                or (>= '\ud800' and <= '\uf8ff')
                or >= '\ufff0'
            )
                throw new ArgumentException($"Path '{pathExpression}' has invalid character '{c}' at {i}: {path}", pathExpression);

            if (c == '/' && last == '/')
                throw new ArgumentException($"Path '{pathExpression}' has an empty node name specified at {i}: {path}", pathExpression);

            if (c == '.')
                if (last == '.')
                {
                    if (span[i - 2] == '/' && ((i + 1 == span.Length) || span[i + 1] == '/'))
                        throw new ArgumentException($"Path '{pathExpression}' has a not allowed relative path specified at {i}: {path}");
                }
                else
                {
                    if (span[i - 1] == '/' && ((i + 1 == span.Length) || span[i + 1] == '/'))
                        throw new ArgumentException($"Path '{pathExpression}' has a not allowed relative path specified at {i}: {path}");
                }
        }
    }

    public static void ThrowIfEmptyOrInvalid(this ZooKeeperPath path, [CallerArgumentExpression(nameof(path))] string? pathExpression = null) =>
        path.ThrowIfEmpty(pathExpression)
            .ThrowIfInvalid(pathExpression);
}

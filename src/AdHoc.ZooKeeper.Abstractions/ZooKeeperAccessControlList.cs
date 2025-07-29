// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections;
using System.Collections.Immutable;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperAccessControlList;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperTransactions;

namespace AdHoc.ZooKeeper.Abstractions;
public class ZooKeeperAccessControlList
    : IReadOnlyList<Entry>
{

    public static ZooKeeperAccessControlList Default { get; } = new([new Entry(WorldScheme, AnyoneIdentifier, ZooKeeperPermissions.All)]);

    public const string WorldScheme = "world";
    public const string AnyoneIdentifier = "anyone";

    public readonly record struct Entry(
        string Scheme,
        string Identifier,
        ZooKeeperPermissions Permissions
    );

    private readonly ImmutableList<Entry> _entries;



    public ZooKeeperAccessControlList() =>
        _entries = [];

    public ZooKeeperAccessControlList(IEnumerable<Entry> entries) =>
        _entries = [.. entries];


    public int Size =>
        LengthSize + Int32Size + (_entries.Count * (Int32Size + LengthSize + LengthSize) * LengthSize)
        + _entries.Sum(e => e.Scheme.Length + e.Identifier.Length);

    public int Write(in Span<byte> buffer)
    {
        int size = 0;
        size += ZooKeeperTransactions.Write(buffer.Slice(size), Size - LengthSize);
        foreach (var entry in _entries)
        {
            size += ZooKeeperTransactions.Write(buffer.Slice(size), entry.Scheme);
            size += ZooKeeperTransactions.Write(buffer.Slice(size), entry.Identifier);
            size += ZooKeeperTransactions.Write(buffer.Slice(size), (int)entry.Permissions);
        }
        return size;
    }

    public int Count => _entries.Count;

    public Entry this[int index] => ((IReadOnlyList<Entry>)_entries)[index];

    public IEnumerator<Entry> GetEnumerator() => ((IEnumerable<Entry>)_entries).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_entries).GetEnumerator();
}

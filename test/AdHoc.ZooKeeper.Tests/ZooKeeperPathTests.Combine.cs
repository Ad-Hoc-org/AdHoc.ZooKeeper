// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public partial class ZooKeeperPathTests
{
    [Test]
    public async Task Combine_NoPath()
    {
        await Assert.That(ZooKeeperPath.Combine())
            .IsEqualTo(ZooKeeperPath.Empty);
    }
    [Test]
    public async Task Combine_SinglePath()
    {
        await Assert.That(ZooKeeperPath.Combine("path1"))
            .IsEqualTo("path1");
        await Assert.That(ZooKeeperPath.Combine("/path1"))
            .IsEqualTo("/path1");
    }

    [Test]
    public async Task Combine_MultiplePaths()
    {
        await Assert.That(ZooKeeperPath.Combine("path1", "path2", "path3"))
            .IsEqualTo("path1/path2/path3");
    }

    [Test]
    public async Task Combine_EmptyPaths()
    {
        await Assert.That(ZooKeeperPath.Combine("", "path2"))
            .IsEqualTo("/path2");
        await Assert.That(ZooKeeperPath.Combine("path1", ""))
            .IsEqualTo("path1/");
        await Assert.That(ZooKeeperPath.Combine("", "/path2", ""))
            .IsEqualTo("/path2/");
        await Assert.That(ZooKeeperPath.Combine("", "/path2", "", ""))
            .IsEqualTo("/path2//");
        await Assert.That(ZooKeeperPath.Combine("", "", ""))
            .IsEqualTo("//");
    }

    [Test]
    public async Task Combine_SeparatorPaths()
    {
        await Assert.That(ZooKeeperPath.Combine("/", "path2"))
            .IsEqualTo("/path2");
        await Assert.That(ZooKeeperPath.Combine("path1", "/"))
            .IsEqualTo("path1/");
        await Assert.That(ZooKeeperPath.Combine("path1", "/", "path3"))
            .IsEqualTo("path1/path3");
        await Assert.That(ZooKeeperPath.Combine("/", "path2", "/"))
            .IsEqualTo("/path2/");
        await Assert.That(ZooKeeperPath.Combine("/", "/path2/", "/"))
            .IsEqualTo("/path2/");
        await Assert.That(ZooKeeperPath.Combine("/", "/", "/"))
            .IsEqualTo("///");
    }

    [Test]
    public async Task Combine_EndsWithSeparator()
    {
        await Assert.That(ZooKeeperPath.Combine("path1/", "path2"))
            .IsEqualTo("path1/path2");
        await Assert.That(ZooKeeperPath.Combine("path1/", "/path2"))
            .IsEqualTo("path1/path2");
    }

    [Test]
    public async Task Combine_StartsWithSeparator()
    {
        await Assert.That(ZooKeeperPath.Combine("/path1", "path2"))
            .IsEqualTo("/path1/path2");
        await Assert.That(ZooKeeperPath.Combine("/path1", "/path2"))
            .IsEqualTo("/path1/path2");
    }
}

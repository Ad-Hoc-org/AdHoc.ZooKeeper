// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public partial class ZooKeeperPathTests
{

    [Test]
    public async Task IsContainer()
    {
        await Assert.That(ZooKeeperPath.Empty.IsContainer)
            .IsFalse();
        await Assert.That(ZooKeeperPath.Root.IsContainer)
            .IsTrue();
        await Assert.That(new ZooKeeperPath("container").IsContainer)
            .IsFalse();
        await Assert.That(new ZooKeeperPath("container/").IsContainer)
            .IsTrue();
    }

    [Test]
    public async Task Container_Empty()
    {
        await Assert.That(ZooKeeperPath.Empty.Container)
            .IsEqualTo(ZooKeeperPath.Empty);
    }

    [Test]
    public async Task Container_Root()
    {
        await Assert.That(ZooKeeperPath.Root)
            .IsEqualTo(ZooKeeperPath.Root);
    }

    [Test]
    public async Task Container_Node()
    {
        await Assert.That(new ZooKeeperPath("node").Container)
            .IsEqualTo(ZooKeeperPath.Empty);
    }

    [Test]
    public async Task Container_AbsoluteNode()
    {
        await Assert.That(new ZooKeeperPath("/node").Container)
            .IsEqualTo(ZooKeeperPath.Root);
    }

    [Test]
    public async Task Container_Path()
    {
        await Assert.That(new ZooKeeperPath("container/node").Container)
            .IsEqualTo("container/");
    }

    [Test]
    public async Task Container_AbsolutePath()
    {
        await Assert.That(new ZooKeeperPath("/container/node").Container)
            .IsEqualTo("/container/");
    }
}

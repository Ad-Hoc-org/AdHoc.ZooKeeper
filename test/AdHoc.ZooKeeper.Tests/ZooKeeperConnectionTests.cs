// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;
public class ZooKeeperConnectionTests
{
    [Test]
    public async Task ShouldBeEquals_IndependentOfHostsOrder()
    {
        await Assert.That(ZooKeeperConnection.Parse("localhost:2181,localhost:2182"))
            .IsEqualTo(ZooKeeperConnection.Parse("localhost:2182,localhost:2181"));
        await Assert.That(ZooKeeperConnection.Parse("localhost:2181,localhost:2182").GetHashCode())
            .IsEqualTo(ZooKeeperConnection.Parse("localhost:2182,localhost:2181").GetHashCode());
    }

    [Test]
    public async Task ShouldBeEquals_IndependentOfAuthenticationOrder()
    {
        await Assert.That(ZooKeeperConnection.Parse("localhost:2181?auth=digits:abc&auth=digits:123"))
            .IsEqualTo(ZooKeeperConnection.Parse("localhost:2181?auth=digits:123&auth=digits:abc"));
        await Assert.That(ZooKeeperConnection.Parse("localhost:2181?auth=digits:abc&auth=digits:123").GetHashCode())
            .IsEqualTo(ZooKeeperConnection.Parse("localhost:2181?auth=digits:123&auth=digits:abc").GetHashCode());
    }
}

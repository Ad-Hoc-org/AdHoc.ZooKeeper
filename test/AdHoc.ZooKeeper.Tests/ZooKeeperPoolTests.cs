using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public class ZooKeeperPoolTests
{
    //[Test]
    //public async Task GetZooKeeperAsync_DifferentConnectionShouldHaveDifferentSession(CancellationToken cancellationToken)
    //{
    //    await using var pool = new ZooKeeperPool();
    //    await using var keeper1 = pool.GetZooKeeper(ZooKeeperConnection.Parse("localhost:2181"));
    //    await using var keeper2 = pool.GetZooKeeper(ZooKeeperConnection.Parse("localhost:2182"));
    //    await Assert.That(((ZooKeeper)keeper1)._session)
    //        .IsNotEqualTo(((ZooKeeper)keeper2)._session);
    //}

    //[Test]
    //public async Task GetZooKeeperAsync_SameConnectionShouldHaveSameSession(CancellationToken cancellationToken)
    //{
    //    await using var pool = new ZooKeeperPool();
    //    await using var keeper1 = pool.GetZooKeeper(ZooKeeperConnection.Parse("localhost:2181"));
    //    await using var keeper2 = pool.GetZooKeeper(ZooKeeperConnection.Parse("localhost:2181"));
    //    await Assert.That(((ZooKeeper)keeper1)._session)
    //        .IsEqualTo(((ZooKeeper)keeper2)._session);
    //}

    //[Test]
    //public async Task GetZooKeeperAsync_DifferentRootShouldHaveSameSession(CancellationToken cancellationToken)
    //{
    //    await using var pool = new ZooKeeperPool();
    //    await using var keeper1 = pool.GetZooKeeper(ZooKeeperConnection.Parse("localhost:2181/foo"));
    //    await using var keeper2 = pool.GetZooKeeper(ZooKeeperConnection.Parse("localhost:2181/bar"));
    //    await Assert.That(((ZooKeeper)keeper1)._session)
    //        .IsEqualTo(((ZooKeeper)keeper2)._session);
    //}
}

using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public abstract partial class ZooKeeperTests
{

    [Test]
    [DependsOn(nameof(CreateAsync_NewNode))]
    [DependsOn(nameof(SetDataAsync_ExistingNode))]
    public async Task AddWatchAsync_Persistent(CancellationToken cancellationToken)
    {
        bool dispatched;
        var watcher = await ZooKeeper.AddWatchAsync(_NewNode, false, (_, _) => dispatched = true, cancellationToken);

        dispatched = false;
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        await Assert.That(dispatched).IsTrue();

        dispatched = false;
        await ZooKeeper.SetDataAsync(_NewNode, _SetData, cancellationToken);
        await Assert.That(dispatched).IsTrue();

        await watcher.DisposeAsync();
        dispatched = false;
        await ZooKeeper.SetDataAsync(_NewNode, _SetData, cancellationToken);
        await Assert.That(dispatched).IsFalse();
    }

    [Test]
    public async Task AddWatchAsync_PersistentAndOthers(CancellationToken cancellationToken)
    {
        bool dispatched;
        var watcher = await ZooKeeper.AddWatchAsync(_NewNode, false, (_, _) => dispatched = true, cancellationToken);

        dispatched = false;
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        await Assert.That(dispatched).IsTrue();

        dispatched = false;
        await ZooKeeper.SetDataAsync(_NewNode, _SetData, cancellationToken);
        await Assert.That(dispatched).IsTrue();

        await watcher.DisposeAsync();
        dispatched = false;
        await ZooKeeper.SetDataAsync(_NewNode, _SetData, cancellationToken);
        await Assert.That(dispatched).IsFalse();
    }

}

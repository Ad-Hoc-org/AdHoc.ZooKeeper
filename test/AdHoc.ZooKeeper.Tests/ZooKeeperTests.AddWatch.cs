using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public partial class ZooKeeperTests
{

    [Test]
    [DependsOn(nameof(CreateAsync_NewNode))]
    [DependsOn(nameof(SetDataAsync_ExistingNode))]
    public async Task AddWatchAsync_Persistent(CancellationToken cancellationToken)
    {
        bool dispatched;
        await using var watcher = await ZooKeeper.AddWatchAsync(_NewNode, (_, _) => dispatched = true, cancellationToken);

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
    [DependsOn(nameof(CreateAsync_NewNode))]
    public async Task AddWatchAsync_PersistentRecursive(CancellationToken cancellationToken)
    {
        bool dispatched;
        await using var watcher = await ZooKeeper.AddWatchRecursiveAsync(_NewNode, (_, _) => dispatched = true, cancellationToken);

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
    [DependsOn(nameof(AddWatchAsync_PersistentRecursive))]
    public async Task AddWatchAsync_PersistentRecursive_Children(CancellationToken cancellationToken)
    {
        bool dispatched;
        await using var watcher = await ZooKeeper.AddWatchRecursiveAsync(_NewNode, (_, _) => dispatched = true, cancellationToken);

        dispatched = false;
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        await Assert.That(dispatched).IsTrue();

        dispatched = false;
        await ZooKeeper.CreateAsync(_ChildNode, _NewData, cancellationToken);
        await Assert.That(dispatched).IsTrue();
    }

    [Test]
    [DependsOn(nameof(AddWatchAsync_Persistent))]
    public async Task AddWatchAsync_PersistentWithDifferentLifecycle(CancellationToken cancellationToken)
    {
        int dispatched = 0;
        await using var watcher = await ZooKeeper.AddWatchAsync(_NewNode, (_, _) => dispatched++, cancellationToken);

        await using (await ZooKeeper.AddWatchAsync(_NewNode, (_, _) => dispatched++, cancellationToken))
        {
            dispatched = 0;
            await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
            await Assert.That(dispatched).IsEqualTo(2);
        }

        dispatched = 0;
        await ZooKeeper.SetDataAsync(_NewNode, _SetData, cancellationToken);
        await Assert.That(dispatched).IsEqualTo(1);
    }

    [Test]
    [DependsOn(nameof(AddWatchAsync_Persistent))]
    public async Task AddWatchAsync_PersistentAndOthers(CancellationToken cancellationToken)
    {
        int dispatched = 0;
        await using var watcher = await ZooKeeper.AddWatchAsync(_NewNode, (_, _) => dispatched++, cancellationToken);
        await ZooKeeper.ExistsAsync(_NewNode, (_, _) => dispatched++, cancellationToken);

        dispatched = 0;
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        await Assert.That(dispatched).IsEqualTo(2);

        dispatched = 0;
        await ZooKeeper.SetDataAsync(_NewNode, _NewData, cancellationToken);
        await Assert.That(dispatched).IsEqualTo(1);
    }

    [Test]
    [DependsOn(nameof(AddWatchAsync_Persistent))]
    public async Task AddWatchAsync_Reconnect(CancellationToken cancellationToken)
    {
        bool dispatched;
        await using var watcher = await ZooKeeper.AddWatchAsync(_NewNode, (_, _) => dispatched = true, cancellationToken);

        dispatched = false;
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        await Assert.That(dispatched).IsTrue();

        await StopInstancesAsync(cancellationToken);
        await Assert.ThrowsAsync<ConnectionLostException>(() => ZooKeeper.PingAsync(cancellationToken));
        await StartInstancesAsync(cancellationToken);

        dispatched = false;
        await ZooKeeper.SetDataAsync(_NewNode, _SetData, cancellationToken);
        await Assert.That(dispatched).IsTrue();
    }

}

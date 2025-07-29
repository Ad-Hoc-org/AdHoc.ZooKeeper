using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public partial class ZooKeeperTests
{

    [Test]
    public async Task ExistsAsync_MissingNode(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.ExistsAsync(_NewNode, cancellationToken);
        await Assert.That(result.Node).IsNull();
    }

    [Test]
    [DependsOn(nameof(CreateAsync_NewNode))]
    public async Task ExistsAsync_ExistingNode(CancellationToken cancellationToken)
    {
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        var result = await ZooKeeper.ExistsAsync(_NewNode, cancellationToken);
        await Assert.That(result.Node).IsNotNull();
    }

    [Test]
    [DependsOn(nameof(CreateAsync_NewNode))]
    [DependsOn(nameof(DeleteAsync_ExistingNode))]
    public async Task ExistsAsync_Watch(CancellationToken cancellationToken)
    {
        bool dispatched;
        await using ((await ZooKeeper.ExistsAsync(_NewNode, (_, _) => dispatched = true, cancellationToken)).Watcher)
        {
            dispatched = false;
            await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
            await Assert.That(dispatched).IsTrue();
        }

        await using ((await ZooKeeper.ExistsAsync(_NewNode, (_, _) => dispatched = true, cancellationToken)).Watcher)
        {
            dispatched = false;
            await ZooKeeper.DeleteAsync(_NewNode, cancellationToken);
            await Assert.That(dispatched).IsTrue();
        }
    }

}

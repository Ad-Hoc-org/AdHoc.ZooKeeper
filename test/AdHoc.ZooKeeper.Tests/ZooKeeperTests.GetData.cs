using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public partial class ZooKeeperTests
{

    [Test]
    public async Task GetDataAsync_MissingNode(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.GetDataAsync(_NewNode, cancellationToken);
        await Assert.That(result.Node).IsNull();
        await Assert.That(result.Data.IsEmpty).IsTrue();
    }

    [Test]
    [DependsOn(nameof(CreateAsync_NewNode))]
    public async Task GetDataAsync_ExistingNode(CancellationToken cancellationToken)
    {
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        var result = await ZooKeeper.GetDataAsync(_NewNode, cancellationToken);
        await Assert.That(result.Node).IsNotNull();
        await Assert.That(result.Data.ToArray()).IsEquivalentTo(_NewData);
    }

    [Test]
    [DependsOn(nameof(CreateAsync_NewNode))]
    [DependsOn(nameof(SetDataAsync_ExistingNode))]
    public async Task GetDataAsync_Watch(CancellationToken cancellationToken)
    {
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);

        bool dispatched;
        await using var watcher = (await ZooKeeper.GetDataAsync(_NewNode, (_, _) => dispatched = true, cancellationToken)).Watcher;

        dispatched = false;
        await ZooKeeper.SetDataAsync(_NewNode, _SetData, cancellationToken);
        await Assert.That(dispatched).IsTrue();

        dispatched = false;
        await ZooKeeper.SetDataAsync(_NewNode, _SetData, cancellationToken);
        await Assert.That(dispatched).IsFalse();
    }

}

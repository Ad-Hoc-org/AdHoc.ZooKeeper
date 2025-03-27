using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public partial class ZooKeeperTests
{

    [Test]
    public async Task DeleteAsync_MissingNode(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.DeleteAsync(_NewNode, cancellationToken);
        await Assert.That(result.Deleted).IsFalse();
        await Assert.That(result.Existed).IsFalse();
    }

    [Test]
    [DependsOn(nameof(CreateAsync_NewNode))]
    public async Task DeleteAsync_ExistingNode(CancellationToken cancellationToken)
    {
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        var result = await ZooKeeper.DeleteAsync(_NewNode, cancellationToken);
        await Assert.That(result.Deleted).IsTrue();
        await Assert.That(result.Existed).IsTrue();
    }

    [Test]
    [DependsOn(nameof(CreateAsync_NewNode))]
    public async Task DeleteAsync_FullContainer(CancellationToken cancellationToken)
    {
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        await ZooKeeper.CreateAsync(_ChildNode, _NewData, cancellationToken);
        var result = await ZooKeeper.DeleteAsync(_NewNode, cancellationToken);
        await Assert.That(result.Deleted).IsFalse();
        await Assert.That(result.Existed).IsTrue();
    }

}

using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public partial class ZooKeeperTests
{

    private static ZooKeeperPath _NewNode = "new-node";
    private static ZooKeeperPath _ChildNode = "new-node/child";
    private static byte[] _NewData = [1, 2, 3, 4];

    [Test]
    public async Task CreateAsync_NewNode(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsFalse();
        await Assert.That(result.ContainerMissing).IsFalse();
    }

    [Test]
    public async Task CreateAsync_CaseSensitivity(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsFalse();
        await Assert.That(result.ContainerMissing).IsFalse();

        result = await ZooKeeper.CreateAsync(_NewNode.ToString().ToUpper(), _NewData, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsFalse();
        await Assert.That(result.ContainerMissing).IsFalse();
    }

    [Test]
    public async Task CreateAsync_MissingContainer(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.CreateAsync(_ChildNode, _NewData, cancellationToken);
        await Assert.That(result.ContainerMissing).IsTrue();
        await Assert.That(result.AlreadyExisted).IsFalse();
    }

    [Test]
    [DependsOn(nameof(CreateAsync_NewNode))]
    public async Task CreateAsync_ExistingNode(CancellationToken cancellationToken)
    {
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        var result = await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsTrue();
        await Assert.That(result.ContainerMissing).IsFalse();
    }

}

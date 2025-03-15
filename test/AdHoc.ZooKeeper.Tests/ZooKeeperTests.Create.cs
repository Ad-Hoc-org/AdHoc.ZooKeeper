using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public abstract partial class ZooKeeperTests
{

    private static ZooKeeperPath _NewNode = "new-node";
    private static byte[] _NewData = [1, 2, 3, 4];

    [Test]
    public async Task CreateAsync_NewNode(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsFalse();
        await Assert.That(result.ContainerMissing).IsFalse();
    }

}

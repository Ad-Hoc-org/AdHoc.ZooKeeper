using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public partial class ZooKeeperTests
{
    [Test]
    public async Task Transaction_WithoutError(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.StartTransaction()
            .Create("container")
            .Create("container/node")
            .CommitAsync(cancellationToken);

        await Assert.That(result.HasError).IsFalse();
        await Assert.That(result.Responses!.Value.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Transaction_HasError(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.StartTransaction()
            .Create("container")
            .Create("container/node/missing")
            .Create("container/node")
            .CommitAsync(cancellationToken);

        await Assert.That(result.HasError).IsTrue();
        await Assert.That(result.Error!.Value.Status).IsEqualTo(ZooKeeperStatus.NoNode);
        await Assert.That(result.Error!.Value.Index).IsEqualTo(1);
    }
}

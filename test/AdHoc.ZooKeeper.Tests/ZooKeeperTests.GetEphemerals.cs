using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public partial class ZooKeeperTests
{
    [Test]
    public async Task GetEphemeralsAsync_None(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.GetEphemeralsAsync(_NewNode, cancellationToken);
        await Assert.That(result.Ephemerals.Length).IsEqualTo(0);
    }
}

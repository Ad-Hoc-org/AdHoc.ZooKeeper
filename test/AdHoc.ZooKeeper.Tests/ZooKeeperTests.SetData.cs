using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public abstract partial class ZooKeeperTests
{

    private static byte[] _SetData = [4, 3, 2, 1];

    [Test]
    public async Task SetDataAsync_MissingNode(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.SetDataAsync(_NewNode, _SetData, cancellationToken);
        await Assert.That(result.Node).IsNull();
    }

    [Test]
    [DependsOn(nameof(CreateAsync_NewNode))]
    [DependsOn(nameof(GetDataAsync_ExistingNode))]
    public async Task SetDataAsync_ExistingNode(CancellationToken cancellationToken)
    {
        await ZooKeeper.CreateAsync(_NewNode, _NewData, cancellationToken);
        var result = await ZooKeeper.SetDataAsync(_NewNode, _SetData, cancellationToken);
        await Assert.That(result.Node).IsNotNull();
        await Assert.That(result.Node!.Value.Length).IsEqualTo(_SetData.Length);
        await Assert.That((await ZooKeeper.GetDataAsync(_NewNode, cancellationToken)).Data.ToArray())
            .IsEquivalentTo(_SetData);
    }

}

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

    [Test]
    [DependsOn(nameof(GetEphemeralsAsync_None))]
    public async Task CreateAsync_Ephemeral(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.CreateEphemeralAsync(_NewNode, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsFalse();
        await Assert.That(result.ContainerMissing).IsFalse();

        await Assert.That((await ZooKeeper.GetEphemeralsAsync(result.Path, cancellationToken)).Ephemerals.Length)
            .IsEqualTo(1);

        await NewSessionAsync(cancellationToken);

        await Assert.That((await ZooKeeper.GetEphemeralsAsync(result.Path, cancellationToken)).Ephemerals.Length)
            .IsEqualTo(0);
    }

    [Test]
    //[DependsOn(nameof(CreateAsync_Ephemeral))]
    public async Task CreateAsync_WithTimeToLive(CancellationToken cancellationToken)
    {
        var ttl = TimeSpan.FromSeconds(10);
        var result = await ZooKeeper.CreateAsync(_NewNode, ttl, cancellationToken);

        await NewSessionAsync(cancellationToken);
        var delay = Task.Delay(ttl, cancellationToken);

        // should be still alive
        await Assert.That((await ZooKeeper.ExistsAsync(result.Path, cancellationToken)).Node).IsNotNull();

        await delay;
        try
        {
            await Assert.That((await ZooKeeper.ExistsAsync(result.Path, cancellationToken)).Node).IsNull();
        }
        catch
        {
            // try it again maybe server was slower
            await Task.Delay(ttl / 2);
            await Assert.That((await ZooKeeper.ExistsAsync(result.Path, cancellationToken)).Node).IsNull();
        }
    }

    [Test]
    public async Task CreateAsync_MultipleEphemeralsWithoutSequential(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.CreateEphemeralAsync(_NewNode, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsFalse();
        await Assert.That(result.ContainerMissing).IsFalse();

        result = await ZooKeeper.CreateEphemeralAsync(_NewNode, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsTrue();
        await Assert.That(result.ContainerMissing).IsFalse();
    }
}

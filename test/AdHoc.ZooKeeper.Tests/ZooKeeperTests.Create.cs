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
    public async Task CreateAsync_WithTimeToLive(CancellationToken cancellationToken)
    {
        var ttl = TimeSpan.FromSeconds(5);
        var result = await ZooKeeper.CreateAsync(_NewNode, ttl, cancellationToken);

        await NewSessionAsync(cancellationToken);
        var delay = Task.Delay(ttl * 3, cancellationToken);

        // should be still alive
        await Assert.That((await ZooKeeper.ExistsAsync(result.Path, cancellationToken)).Node).IsNotNull();

        do
        {
            try
            {
                await Assert.That((await ZooKeeper.ExistsAsync(result.Path, cancellationToken)).Node).IsNull();
            }
            catch when (!delay.IsCompleted)
            {
                await Task.Delay(1000, cancellationToken);
            }
        } while (!delay.IsCompleted);
    }


    [Test]
    [DependsOn(nameof(GetEphemeralsAsync_None))]
    public async Task CreateEphemeralAsync_NewWithDisposing(CancellationToken cancellationToken)
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
    public async Task CreateEphemeralAsync_MultipleEphemeralsWithoutSequential(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.CreateEphemeralAsync(_NewNode, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsFalse();
        await Assert.That(result.ContainerMissing).IsFalse();

        result = await ZooKeeper.CreateEphemeralAsync(_NewNode, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsTrue();
        await Assert.That(result.ContainerMissing).IsFalse();
    }


    [Test]
    public async Task CreateContainerAsync_NewContainer(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.CreateContainerAsync(_NewNode, _NewData, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsFalse();
        await Assert.That(result.ContainerMissing).IsFalse();
    }

    [Test]
    public async Task CreateContainerAsync_ExistingContainer(CancellationToken cancellationToken)
    {
        await ZooKeeper.CreateAsync(_NewNode, cancellationToken);
        var result = await ZooKeeper.CreateContainerAsync(_NewNode, cancellationToken);
        await Assert.That(result.AlreadyExisted).IsTrue();
        await Assert.That(result.ContainerMissing).IsFalse();
    }

    [Test]
    public async Task CreateContainerAsync_MissingContainer(CancellationToken cancellationToken)
    {
        var result = await ZooKeeper.CreateContainerAsync(_ChildNode, cancellationToken);
        await Assert.That(result.ContainerMissing).IsTrue();
        await Assert.That(result.AlreadyExisted).IsFalse();
    }
}

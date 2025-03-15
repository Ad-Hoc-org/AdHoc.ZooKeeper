using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public abstract partial class ZooKeeperTests
{

    protected abstract IZooKeeper ZooKeeper { get; }

    protected abstract Task StartInstancesAsync(CancellationToken cancellationToken);
    protected abstract Task StopInstancesAsync(CancellationToken cancellationToken);
    protected abstract Task StopInstanceAsync(CancellationToken cancellationToken);


    [Test]
    public Task PingAsync_WithConnection(CancellationToken cancellationToken) =>
        ZooKeeper.PingAsync(cancellationToken);

    [Test]
    public async Task PingAsync_WithNoConnection(CancellationToken cancellationToken)
    {
        await StopInstancesAsync(cancellationToken);
        await Assert.ThrowsAsync<ConnectionException>(() => ZooKeeper.PingAsync(cancellationToken));
    }

    [Test]
    [DependsOn(nameof(PingAsync_WithConnection))]
    public async Task PingAsync_WithLostConnection(CancellationToken cancellationToken)
    {
        await ZooKeeper.PingAsync(cancellationToken);
        await StopInstancesAsync(cancellationToken);
        await Assert.ThrowsAsync<ConnectionLostException>(() => ZooKeeper.PingAsync(cancellationToken));
    }

    [Test]
    [DependsOn(nameof(PingAsync_WithLostConnection))]
    public async Task PingAsync_WithReconnect(CancellationToken cancellationToken)
    {
        await ZooKeeper.PingAsync(cancellationToken);
        await StopInstancesAsync(cancellationToken);
        await Assert.ThrowsAsync<ConnectionLostException>(() => ZooKeeper.PingAsync(cancellationToken));
        await StartInstancesAsync(cancellationToken);
        await ZooKeeper.PingAsync(cancellationToken);
    }

}

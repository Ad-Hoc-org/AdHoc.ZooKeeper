using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public partial class ZooKeeperTests
{
    [Test]
    public Task PingAsync_WithConnection(CancellationToken cancellationToken) =>
        ZooKeeper.PingAsync(cancellationToken);

    [Test]
    public async Task PingAsync_SessionExpired(CancellationToken cancellationToken)
    {
        await ZooKeeper.PingAsync(cancellationToken);
        await Task.Delay(SessionTimeout * 2, cancellationToken); // server tolerance
        await Assert.ThrowsAsync<SessionExpiredException>(ZooKeeper.PingAsync(cancellationToken));
        await ZooKeeper.PingAsync(cancellationToken);
    }

    [Test]
    [DependsOn(nameof(PingAsync_WithConnection))]
    public async Task PingAsync_MultipleParallel(CancellationToken cancellationToken)
    {
        using var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancelSource.CancelAfter(TimeSpan.FromSeconds(10));
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => ZooKeeper.PingAsync(cancelSource.Token)));
    }

    [Test]
    public async Task PingAsync_WithNoConnection(CancellationToken cancellationToken)
    {
        await StopInstancesAsync(cancellationToken);
        await Assert.ThrowsAsync<ConnectionException>(ZooKeeper.PingAsync(cancellationToken));
    }

    [Test]
    [DependsOn(nameof(PingAsync_WithConnection))]
    public async Task PingAsync_WithLostConnection(CancellationToken cancellationToken)
    {
        await ZooKeeper.PingAsync(cancellationToken);
        await StopInstancesAsync(cancellationToken);
        await Assert.ThrowsAsync<ConnectionLostException>(ZooKeeper.PingAsync(cancellationToken));
    }

    [Test]
    [DependsOn(nameof(PingAsync_WithLostConnection))]
    public async Task PingAsync_WithReconnect(CancellationToken cancellationToken)
    {
        await ZooKeeper.PingAsync(cancellationToken);
        await StopInstancesAsync(cancellationToken);
        await Assert.ThrowsAsync<ConnectionLostException>(ZooKeeper.PingAsync(cancellationToken));
        await StartInstancesAsync(cancellationToken);
        await ZooKeeper.PingAsync(cancellationToken);
    }
}

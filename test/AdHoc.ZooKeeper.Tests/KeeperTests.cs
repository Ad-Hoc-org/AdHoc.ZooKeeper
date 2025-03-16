// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Frozen;
using AdHoc.ZooKeeper.Abstractions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;

namespace AdHoc.ZooKeeper.Tests;
[InheritsTests]
public class KeeperTests
    : ZooKeeperTests
{

    private IContainer? _container;
    private readonly Session _session = new Session(new Host("localhost"), FrozenSet<Authentication>.Empty, DefaultConnectionTimeout, DefaultSessionTimeout, false);
    private Keeper? _keeper;

    [Before(Test)]
    public async Task BuildContainerAsync(CancellationToken cancellationToken)
    {
        _container = CreateContainer();
        await StartInstancesAsync(cancellationToken);
    }

    public static IContainer CreateContainer() =>
        new ContainerBuilder()
            .WithImage("zookeeper:latest")
            .WithPortBinding(2181, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(2181))
            .Build();

    [After(Test)]
    public async Task DisposeContainerAsync(CancellationToken cancellationToken)
    {
        if (_container is not null)
            await _container.DisposeAsync();
        if (_keeper is not null)
            await _keeper.DisposeAsync();
    }

    protected override IZooKeeper ZooKeeper => _keeper!;

    protected override async Task StartInstancesAsync(CancellationToken cancellationToken)
    {
        await _container!.StartAsync(cancellationToken);
        await _session.ReconnectAsync(new Host(_container.Hostname, _container.GetMappedPublicPort(2181)), cancellationToken);
        _keeper = new Keeper(_session, ZooKeeperPath.Root);
    }

    protected override Task StopInstanceAsync(CancellationToken cancellationToken) =>
        _container!.StopAsync(cancellationToken);

    protected override Task StopInstancesAsync(CancellationToken cancellationToken) => StopInstanceAsync(cancellationToken);


}

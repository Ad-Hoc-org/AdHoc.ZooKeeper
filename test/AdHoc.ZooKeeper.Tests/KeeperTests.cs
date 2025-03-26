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

    private static IContainer? _container;
    private static Session _session = new Session(new Host("localhost"), FrozenSet<Authentication>.Empty, DefaultConnectionTimeout, DefaultSessionTimeout, false);

    private Keeper? _keeper;
    protected override IZooKeeper ZooKeeper => _keeper!;
    private ZooKeeperPath _root;

    [Before(Class)]
    public static Task CreateContainerAsync(CancellationToken cancellationToken)
    {
        _container = CreateContainer();
        return Task.CompletedTask;
    }

    private static IContainer CreateContainer() =>
        new ContainerBuilder()
            .WithImage("zookeeper:latest")
            .WithPortBinding(2181, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(2181))
            .Build();

    [Before(Test)]
    public async Task PrepareContainerAsync(TestContext context, CancellationToken cancellationToken)
    {
        _root = context.TestDetails.TestMethod.Name;
        await StartInstancesAsync(cancellationToken);
        await ZooKeeper.CreateAsync("/", cancellationToken);
    }

    [After(Test)]
    public async Task DisposeZooKeeperAsync(CancellationToken cancellationToken)
    {
        if (_keeper is not null)
            await _keeper.DisposeAsync();
    }

    [After(Class)]
    public static async Task DisposeContainerAsync(CancellationToken cancellationToken)
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }


    protected override async Task StartInstancesAsync(CancellationToken cancellationToken)
    {
        await _container!.StartAsync(cancellationToken);
        _keeper = new Keeper(_session, _root);
        int i = 0;
        while (i++ < 10)
            try
            {
                if (!_session.IsConnected)
                    await _session.ReconnectAsync(new Host("localhost", _container.GetMappedPublicPort(2181)), cancellationToken);
                break;
            }
            catch
            {
                await Task.Delay(100);
            }
    }

    protected override Task StopInstanceAsync(CancellationToken cancellationToken) =>
        _container!.StopAsync(cancellationToken);

    protected override Task StopInstancesAsync(CancellationToken cancellationToken) => StopInstanceAsync(cancellationToken);


}

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Frozen;
using System.Collections.Immutable;
using AdHoc.ZooKeeper.Abstractions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;

namespace AdHoc.ZooKeeper.Tests;
[InheritsTests]
[NotInParallel]
public class ZooTests
    : ZooKeeperTests
{

    private const int _Instances = 3;
    private INetwork? _network;
    private List<IContainer> _containers = [];

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Session _session = new(new Host("localhost"), FrozenSet<Authentication>.Empty, DefaultConnectionTimeout, DefaultSessionTimeout, false);

    private Zoo? _zoo;


    [Before(Test)]
    public async Task BuildContainersAsync(CancellationToken cancellationToken)
    {
        _network = new NetworkBuilder()
            .Build();
        await _network.CreateAsync(cancellationToken);

        for (var i = 0; i < _Instances; i++)
            _containers.Add(CreateContainer(i + 1));

        await StartInstancesAsync(cancellationToken);
    }

    public IContainer CreateContainer(int i) =>
        new ContainerBuilder()
            .WithImage("zookeeper:latest")
            .WithName($"{_network!.Name}-keeper{i}")
            .WithEnvironment("ZOO_STANDALONE_ENABLED", "false")
            .WithEnvironment("ZOO_MY_ID", i.ToString())
            .WithEnvironment("ZOO_SERVERS", string.Join(' ', Enumerable.Range(1, _Instances).Select(j => $"server.{j}={_network!.Name}-keeper{j}:2888:3888;2181")))
            .WithPortBinding(2181, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(2181))
            .WithNetwork(_network)
            .Build();

    [After(Test)]
    public async Task DisposeContainerAsync(CancellationToken cancellationToken)
    {
        foreach (var container in _containers)
            await container.DisposeAsync();

        if (_network is not null)
            await _network.DisposeAsync();

        if (_zoo is not null)
            await _zoo.DisposeAsync();
    }

    protected override IZooKeeper ZooKeeper => _zoo!;

    protected override async Task StartInstancesAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(_containers.Select(c => c.StartAsync(cancellationToken)));

        ImmutableArray<Host> hosts = [.. _containers.Select(c => new Host("localhost", c.GetMappedPublicPort(2181)))];
        _zoo = new Zoo(_session, hosts, ZooKeeperPath.Root, _lock);
        int i = 0;
        while (i++ < 10)
            try
            {
                await _session.ReconnectAsync(hosts[0], cancellationToken);
                break;
            }
            catch
            {
                await Task.Delay(100);
            }
    }

    protected override Task StopInstanceAsync(CancellationToken cancellationToken) =>
        _containers.First(c => _session.Host.Port == c.GetMappedPublicPort(2181)).StopAsync(cancellationToken);

    protected override async Task StopInstancesAsync(CancellationToken cancellationToken)
    {
        foreach (var container in _containers)
            await container.StopAsync(cancellationToken);
    }


}

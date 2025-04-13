using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using AdHoc.ZooKeeper.Abstractions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using static AdHoc.ZooKeeper.Abstractions.ZooKeeperConnection;

namespace AdHoc.ZooKeeper.Tests;

[NotInParallel]
[Retry(3)]
public partial class ZooKeeperTests
{

    private const int _Instances = 3;
    private static INetwork? _network;
    private static List<IContainer> _containers = [];

    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static Session? _session;

    private static TimeSpan SessionTimeout = Debugger.IsAttached ? TimeSpan.FromSeconds(60) : TimeSpan.FromSeconds(15);
    private static Session Session => _session ??=
        _session = new(
            new Host("localhost"),
            FrozenSet<Authentication>.Empty,
            connectionTimeout: Debugger.IsAttached ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(5),
            sessionTimeout: SessionTimeout,
            false
        );

    private ZooKeeperPath _root;
    private ZooKeeper? _zoo;
    protected IZooKeeper ZooKeeper => _zoo!;

    [Before(Class)]
    public static async Task CreateZooAsync(CancellationToken cancellationToken)
    {
        _network = new NetworkBuilder()
            .Build();
        await _network.CreateAsync(cancellationToken);

        for (var i = 0; i < _Instances; i++)
            _containers.Add(CreateContainer(i + 1));
    }

    private static IContainer CreateContainer(int i) =>
        new ContainerBuilder()
            .WithImage("zookeeper:latest")
            .WithName($"{_network!.Name}-keeper{i}")
            .WithEnvironment("ZOO_CFG_EXTRA", "extendedTypesEnabled=true")
            .WithEnvironment("ZOO_STANDALONE_ENABLED", "false")
            .WithEnvironment("ZOO_MY_ID", i.ToString())
            .WithEnvironment("ZOO_SERVERS", string.Join(' ', Enumerable.Range(1, _Instances).Select(j => $"server.{j}={_network!.Name}-keeper{j}:2888:3888;2181")))
            .WithPortBinding(2181, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(2181))
            .WithNetwork(_network)
            .Build();

    [Before(Test)]
    public async Task PrepareContainerAsync(TestContext context, CancellationToken cancellationToken)
    {
        _root = context.TestDetails.TestMethod.Name;
        _root = _root.Absolute;
        await StartInstancesAsync(cancellationToken);
        await ZooKeeper.CreateAsync("/", cancellationToken);
    }

    private async Task NewSessionAsync(CancellationToken cancellationToken)
    {
        await Session.CloseAsync();
        _session = null;
        _zoo = null;
        await StartInstancesAsync(cancellationToken);
    }

    [After(Test)]
    public async Task DisposeZooKeeperAsync(CancellationToken cancellationToken)
    {
        if (_zoo is not null)
        {
            await _zoo.DisposeAsync();
            _zoo = null;
        }
    }

    [After(Class)]
    public static async Task DisposeZooAsync(CancellationToken cancellationToken)
    {
        if (_session is not null)
            await _session.CloseAsync();

        foreach (var container in _containers)
            await container.DisposeAsync();

        if (_network is not null)
            await _network.DisposeAsync();
    }


    private async Task StartInstancesAsync(CancellationToken cancellationToken)
    {
        int i = 0;
        int retries = 32;
        while (i++ < retries)
            try
            {
                await Task.WhenAll(_containers.Select(async c =>
                {
                    while (c.State != TestcontainersStates.Running)
                    {
                        await c.StartAsync(cancellationToken);
                        await Task.Delay(100);
                    }
                }));

                ImmutableArray<Host> hosts = [.. _containers.Select(c => new Host(c.Hostname, c.GetMappedPublicPort(2181)))];
                _zoo = new ZooKeeper(Session, hosts, _root, _lock);
                if (!Session.IsConnected)
                    await _zoo.TryReconnectAsync<object?>(Session, hosts[0], null, null, cancellationToken);
                else
                    await _zoo.PingAsync(cancellationToken);
                break;
            }
            catch (Exception ex)
            {
                if (i == retries)
                    Console.WriteLine("Failed to connect: " + ex);
                else
                    Console.WriteLine("Tried to connect: " + ex.Message);
                await Task.Delay(100 * i, cancellationToken);
            }
    }

    private async Task StopInstancesAsync(CancellationToken cancellationToken)
    {
        foreach (var container in _containers)
            await container.StopAsync(cancellationToken);
    }


}

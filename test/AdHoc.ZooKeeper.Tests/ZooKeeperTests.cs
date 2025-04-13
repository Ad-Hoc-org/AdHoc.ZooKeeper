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

    private static readonly int _ExposedPort = DefaultPort + Random.Shared.Next(1, 1000) * 3;

    private const int _Instances = 3;
    private static INetwork? _network;
    private static List<IContainer> _containers = [];

    private static TimeSpan SessionTimeout = Debugger.IsAttached ? TimeSpan.FromSeconds(60) : TimeSpan.FromSeconds(15);

    private ZooKeeperPath _root;
    private ZooKeeper? _zoo;
    protected IZooKeeper ZooKeeper => _zoo ??= new ZooKeeper(new ZooKeeperConnection(
        Enumerable.Range(1, _containers.Count).Select(i => new Host("localhost", _ExposedPort + i)).ToImmutableArray()
    )
    {
        Root = _root,
        ConnectionTimeout = Debugger.IsAttached ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(5),
        SessionTimeout = SessionTimeout,
    });

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
            .WithPortBinding(_ExposedPort + i, DefaultPort)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(2181))
            .WithNetwork(_network)
            .Build();

    [Before(Test)]
    public async Task PrepareContainerAsync(TestContext context, CancellationToken cancellationToken)
    {
        _root = context.TestDetails.TestMethod.Name;
        _root = _root.Absolute;
        await StartInstancesAsync(cancellationToken);
        int tries = 3;
        int i = 0;
        while (i++ < tries)
            try
            {
                await ZooKeeper.CreateAsync("/", cancellationToken);
            }
            catch when (i < tries)
            {
                await Task.Delay(1000 * i, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to create root: " + ex.Message);
                throw;
            }
    }

    private async Task NewSessionAsync(CancellationToken cancellationToken)
    {
        if (_zoo is not null)
            await _zoo.DisposeAsync();
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
        foreach (var container in _containers)
            await container.DisposeAsync();

        if (_network is not null)
            await _network.DisposeAsync();
    }


    private async Task StartInstancesAsync(CancellationToken cancellationToken)
    {
        int i = 0;
        int retries = 100;
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

                (await ZooKeeper.PingAsync(cancellationToken)).Status.ThrowIfError();
                break;
            }
            catch (Exception ex) when (i < retries)
            {
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

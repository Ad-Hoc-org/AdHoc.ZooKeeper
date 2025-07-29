// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Net.Sockets;
using AdHoc.ZooKeeper.Abstractions;
using BenchmarkDotNet.Attributes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using org.apache.zookeeper;
using ZooKeeperEx = org.apache.zookeeper.ZooKeeper;

namespace AdHoc.ZooKeeper.Benchmark;
public class Benchmark
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private IContainer _container;
    private int _port;

    private static readonly byte[] _Data = new byte[1024];

    private ZooKeeperPool _pool;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _container = new ContainerBuilder()
            .WithImage("zookeeper:latest")
            .WithEnvironment("ZOO_CFG_EXTRA", "extendedTypesEnabled=true")
            .WithEnvironment("ZOO_STANDALONE_ENABLED", "true")
            .WithPortBinding(2181, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(2181))
            .Build();
        await _container.StartAsync();
        _port = _container.GetMappedPublicPort(2181);
        _pool = new();
        while (true)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("localhost", _port);
                return;
            }
            catch
            {
                await Task.Delay(100);
            }
        }
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _container.DisposeAsync();
        await _pool.DisposeAsync();
    }

    [Params(100)]
    public int Operations { get; set; }

    [Benchmark]
    public async Task CreateDeleteAsync_AdHoc()
    {
        CancellationToken cancellationToken = CancellationToken.None;
        await using var zoo = new ZooKeeper($"localhost:{_port}");
        for (var i = 0; i < Operations; i++)
        {
            await zoo.CreateAsync("node", _Data, cancellationToken);
            await zoo.DeleteAsync("node", cancellationToken);
        }
    }

    [Benchmark]
    public async Task CreateDeleteAsync_Ex()
    {
        var zoo = new ZooKeeperEx($"localhost:{_port}", 3000, new Watcher());
        for (int i = 0; i < Operations; i++)
        {
            await zoo.createAsync("/node", _Data, ZooDefs.Ids.OPEN_ACL_UNSAFE, CreateMode.PERSISTENT);
            await zoo.deleteAsync("/node");
        }
        await zoo.closeAsync();
    }

    private class Watcher : org.apache.zookeeper.Watcher
    {
        public override Task process(WatchedEvent @event)
        {
            return Task.CompletedTask;
        }
    }

}

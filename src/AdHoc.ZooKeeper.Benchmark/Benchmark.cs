// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Diagnostics;
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
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private int _port;

    private static readonly byte[] _Data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 0];

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
    public async Task CleanupAsync() =>
        await _container.DisposeAsync();

    public async Task CreateDeleteAsync_AdHoc(int operations)
    {
        CancellationToken cancellationToken = CancellationToken.None;
        await using var zoo = new ZooKeeper($"localhost:{_port}");
        for (var i = 0; i < operations; i++)
        {
            await zoo.CreateAsync("node", _Data, cancellationToken);
            await zoo.DeleteAsync("node", cancellationToken);
        }
    }

    public async Task CreateDeleteAsync_Ex(int operations)
    {
        var zoo = new ZooKeeperEx($"localhost:{_port}", 3000, new Watcher());
        for (int i = 0; i < operations; i++)
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

    public async Task RunBenchmark(Func<int, Task> benchmarkMethod, int operations)
    {
        // Measure memory usage before the benchmark
        long memoryBefore = GC.GetTotalMemory(true);

        // Measure execution time
        var stopwatch = Stopwatch.StartNew();
        await benchmarkMethod(operations);
        stopwatch.Stop();

        // Measure memory usage after the benchmark
        long memoryAfter = GC.GetTotalMemory(true);

        Console.WriteLine($"Time elapsed: {stopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine($"Memory used: {memoryAfter - memoryBefore} bytes");
    }
}

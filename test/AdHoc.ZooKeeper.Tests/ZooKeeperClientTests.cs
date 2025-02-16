// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using AdHoc.ZooKeeper.Abstractions;

namespace AdHoc.ZooKeeper.Tests;

public class ZooKeeperClientTests
{

    [Fact]
    public async Task Test1()
    {
        CancellationToken cancellationToken = default;

        await using var client = new ZooKeeperClient("localhost/?sessionTimeout=35000");

        //Console.WriteLine(await client.ExistsAsync("foo", LogEvents, cancellationToken));
        Console.WriteLine(await client.GetChildrenAsync("foo", LogEvents, cancellationToken));

        Console.WriteLine(await client.CreateAsync("foo", "bar"u8.ToArray(), cancellationToken));
        Console.WriteLine(await client.CreateAsync("foo", cancellationToken));

        Console.WriteLine(await client.GetChildrenAsync("foo", LogEvents, cancellationToken));
        Console.WriteLine(await client.CreateAsync("foo/bar", cancellationToken));
        Console.WriteLine(await client.GetChildrenAsync("foo", LogEvents, cancellationToken));

        Console.WriteLine(await client.GetDataAsync("foo", LogEvents, cancellationToken));
        Console.WriteLine(await client.SetDataAsync("foo", "data"u8.ToArray(), cancellationToken));

        Console.WriteLine(await client.CreateEphemeralAsync("ephemeral", cancellationToken));

        Console.WriteLine(await client.ExistsAsync("empty", cancellationToken));
        Console.WriteLine(await client.GetDataAsync("empty", cancellationToken));

        Console.WriteLine(await client.DeleteAsync("foo", cancellationToken));
        Console.WriteLine(await client.DeleteAsync("foo/bar", cancellationToken));
        Console.WriteLine(await client.DeleteAsync("foo", cancellationToken));
        Console.WriteLine(await client.SetDataAsync("foo", "data"u8.ToArray(), cancellationToken));
        Console.WriteLine(await client.DeleteAsync("foo", cancellationToken));

        Console.WriteLine("done");

        void LogEvents(IZooKeeperWatcher _, ZooKeeperEvent ev)
        {
            var bg = Console.BackgroundColor;
            Console.BackgroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(ev);
            Console.BackgroundColor = bg;
        }
        //await client.PingAsync(cancellation);
        //await client.CreateAsync("/test", Encoding.UTF8.GetBytes("hello"), cancellation);

        //Console.WriteLine("Done!");

    }
}

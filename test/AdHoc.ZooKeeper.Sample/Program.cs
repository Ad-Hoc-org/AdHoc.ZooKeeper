// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using AdHoc.ZooKeeper;
using AdHoc.ZooKeeper.Abstractions;

CancellationToken cancellationToken = default;

await using var client = new ZooKeeperClient("localhost:8181/?sessionTimeout=35000&auth=digest:super:superpwd");

Console.WriteLine(await client.ExistsAsync("foo", LogEvents, cancellationToken));
Console.WriteLine(await client.GetChildrenAsync("foo", LogEvents, cancellationToken));

await Task.Delay(40000);

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

# ZooKeeper

**AdHoc.ZooKeeper** is a library designed to simplify interactions with ZooKeeper, providing abstractions and utilities for reading, writing, managing transactions, and handling watchers in a ZooKeeper environment.

## Features

- **Abstractions for ZooKeeper**: Simplified interfaces for interacting with ZooKeeper.
- **Read and Write Contexts**: Manage read and write operations with ease.
- **Transaction Support**: Handle ZooKeeper transactions through a clean and intuitive API.
- **Watchers**: Monitor changes to ZooKeeper nodes and react to events in real-time.

## Usage

```csharp
using AdHoc.ZooKeeper.Abstractions;

await using IZooKeeperProvider provider = [...]; // Obtain an instance of IZooKeeperProvider

await using IZooKeeper zooKeeper = provider.GetZooKeeper("localhost:2171/root");

var createResult = await zooKeeper.CreateAsync("/path/to/node", "data"u8.ToArray(), CreateMode.Persistent);

await using (await zooKeeper.ExistsAsync(_NewNode, (_, _) => {}, cancellationToken))
{
    // watcher is listening
}
// watcher is deregistrated
```

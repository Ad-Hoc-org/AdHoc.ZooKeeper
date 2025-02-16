// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public interface IZooKeeperOperation<TResult>
{
    public void WriteRequest(in ZooKeeperContext context);

    public TResult ReadResponse(in ZooKeeperResponse response, IZooKeeperWatcher? watcher);
}

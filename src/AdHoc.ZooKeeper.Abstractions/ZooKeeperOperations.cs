// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public enum ZooKeeperOperations
    : int
{
    //Error = -1,
    //Notification = 0,
    Create = 1,
    Delete = 2,
    Exists = 3,
    GetData = 4,
    SetData = 5,
    //GetAccessControlList = 6,
    //SetAccessControlList = 7,
    GetChildren = 8,
    //Sync = 9,
    Ping = 11,
    //GetChildren2 = 12,
    //Multi = 14,
    RemoveWatch = 18,
    Authentication = 100,
    SetWatches = 101,
    SetWatchesWithPersistent = 105,
    AddWatch = 106,
}

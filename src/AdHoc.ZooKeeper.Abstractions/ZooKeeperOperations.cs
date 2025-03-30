// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public enum ZooKeeperOperations
    : int
{
    CloseSession = -11,
    Error = -1,
    Notification = 0,
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
    GetChildrenWithNode = 12,
    //Check = 13,
    Transaction = 14,
    CreateWithNode = 15,
    //Configure = 16,
    //CheckWatch = 17,
    RemoveWatch = 18,
    CreateContainer = 19,
    DeleteContainer = 20,
    CreateWithTimeToLive = 21,
    ReadTransaction = 22,
    Authentication = 100,
    SetWatches = 101,
    //SASL = 102,
    GetEphemerals = 103,
    GetChildrenCount = 104,
    SetWatchesWithPersistent = 105,
    AddWatch = 106,
    WhoAmI = 107
}

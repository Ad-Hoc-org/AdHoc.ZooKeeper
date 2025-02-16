// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;

[Flags]
public enum ZooKeeperPermission : int
{
    None = 0,
    Read = 1 << 0,       // 1
    Write = 1 << 1,      // 2
    Create = 1 << 2,     // 4
    Delete = 1 << 3,     // 8
    Admin = 1 << 4,      // 16
    All = Read | Write | Create | Delete | Admin
}

// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public enum ZooKeeperStatus
    : int
{
    Ok = 0,
    SystemError = -1,
    RuntimeInconsistency = -2,
    DataInconsistency = -3,
    ConnectionLoss = -4,
    MarshallingError = -5,
    Unimplemented = -6,
    OperationTimeout = -7,
    BadArguments = -8,
    APIError = -100,
    NoNode = -101,
    NoAuthentication = -102,
    BadVersion = -103,
    NoChildrenForEphemerals = -108,
    NodeExists = -110,
    NotEmpty = -111,
    SessionExpired = -112,
    InvalidCallback = -113,
    InvalidAccessControlList = -114,
    AuthenticationFailed = -115,
    SessionMoved = -118
}

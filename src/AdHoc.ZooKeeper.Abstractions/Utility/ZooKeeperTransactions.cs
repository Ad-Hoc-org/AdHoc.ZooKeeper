// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

namespace AdHoc.ZooKeeper.Abstractions;
public static partial class ZooKeeperTransactions
{
    public const int BooleanSize = 1;
    public const int Int32Size = 4;
    public const int Int64Size = 8;

    public const int LengthSize = Int32Size;

    public const int RequestSize = Int32Size;
    public const int NoRequest = -1;

    public const int OperationSize = Int32Size;
    public const int RequestHeaderSize = LengthSize + RequestSize + OperationSize;

    public const int TransactionSize = Int64Size;
    public const int StatusSize = Int32Size;
    public const int MinimalResponseLength = RequestSize + TransactionSize + StatusSize;

    public const int VersionSize = Int32Size;
    public const int NoVersion = -1;

    public const int TimestampSize = Int64Size;
    public const int TimeSpanSize = Int64Size;


    public static int GetRequest(this ZooKeeperOperations operation, ref int previousRequest)
    {
        if (operation == ZooKeeperOperations.Ping)
            return PingTransaction.Request;
        if (operation == ZooKeeperOperations.Authentication)
            return AddAuthenticationTransaction.Request;
        if (operation is ZooKeeperOperations.SetWatches or ZooKeeperOperations.SetWatchesWithPersistent)
            return SetWatchersTransaction.Request;

        int oldValue, newValue;
        do
        {
            oldValue = previousRequest;
            newValue = oldValue + 1;
            if (newValue < 0)
                newValue = 1;
        } while (Interlocked.CompareExchange(ref previousRequest, newValue, oldValue) != oldValue);
        return newValue;
    }

}

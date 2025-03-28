// Copyright AdHoc Authors
// SPDX-License-Identifier: MIT

using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;

namespace AdHoc.ZooKeeper.Abstractions;
public sealed record ZooKeeperConnection
    : IEquatable<ZooKeeperConnection>
{
    public const int DefaultPort = 2181;
    public static TimeSpan DefaultSessionTimeout { get; } = TimeSpan.FromSeconds(30);
    public static TimeSpan DefaultConnectionTimeout { get; } = TimeSpan.FromMinutes(1);

    public readonly record struct Host(string Address, int Port = DefaultPort)
    {
        public static Host Default { get; } = new Host("localhost");

        public override string ToString() => $"{Address}:{Port}";
    }

    public readonly record struct Authentication(string Scheme, ReadOnlyMemory<byte> Data)
        : IEquatable<Authentication>
    {
        public override int GetHashCode() =>
            HashCode.Combine(Scheme, Data.Length);

        public bool Equals(Authentication other) =>
            Scheme == other.Scheme
            && Data.Span.SequenceEqual(other.Data.Span);

        public override string ToString() => $"{Scheme}:{Encoding.UTF8.GetString(Data.Span)}";
    }


    public IReadOnlySet<Host> Hosts { get; }

    public IReadOnlySet<Authentication> Authentications
    {
        get;
        init => field = value?.ToFrozenSet() ?? FrozenSet<Authentication>.Empty;
    } = FrozenSet<Authentication>.Empty;


    public TimeSpan SessionTimeout
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            field = value;
        }
    } = DefaultSessionTimeout;

    public TimeSpan ConnectionTimeout
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            field = value;
        }
    } = DefaultConnectionTimeout;


    public ZooKeeperPath Root
    {
        get;
        init => field = value.Absolute;
    } = ZooKeeperPath.Root;


    public bool ReadOnly { get; init; }


    public ZooKeeperConnection(
        params IEnumerable<Host> hosts
    )
    {
        ArgumentNullException.ThrowIfNull(hosts);
        Hosts = hosts.ToFrozenSet();
        if (Hosts.Count == 0)
            Hosts = new HashSet<Host> { Host.Default };
    }

    public ZooKeeperConnection(
        IEnumerable<Host> hosts,
        params IEnumerable<Authentication> authentications
    ) : this(hosts)
    {
        ArgumentNullException.ThrowIfNull(authentications);
        Authentications = authentications.ToFrozenSet();
    }

    public ZooKeeperConnection(
        Host host,
        params IEnumerable<Authentication> authentications
    ) : this([host], authentications) { }



    public const string Scheme = "zookeeper";

    private static readonly Regex ConnectionStringRegex =
        new Regex(@"^(?:zookeeper://)?(?<hosts>[^,/?]+(?:,[^,/?]+)*)(?<root>/[^?]*)?(?:\?(?<params>[^=&]+=[^&]+(?:&[^=&]+=[^&]+)*)?)?$", RegexOptions.Compiled);

    public static ZooKeeperConnection Parse(string connectionString)
    {
        var match = ConnectionStringRegex.Match(connectionString);
        if (!match.Success)
            throw new FormatException("Invalid connection string format.");

        var hostsGroup = match.Groups["hosts"].Value;
        var rootGroup = match.Groups["root"].Value;
        var paramsGroup = match.Groups["params"].Value;

        var hosts = hostsGroup.Split(',').Select(host =>
        {
            var parts = host.Split(':');
            return new Host(parts[0], parts.Length > 1 ? int.Parse(parts[1]) : DefaultPort);
        });

        var root = string.IsNullOrEmpty(rootGroup) ? ZooKeeperPath.Root : new ZooKeeperPath(rootGroup);
        root.ThrowIfInvalid();

        var authentications = new List<Authentication>();
        var sessionTimeout = DefaultSessionTimeout;
        var connectionTimeout = DefaultConnectionTimeout;
        bool readOnly = false;

        if (!string.IsNullOrEmpty(paramsGroup))
        {
            var parameters = paramsGroup.Split('&');
            foreach (var parameter in parameters)
            {
                var keyValue = parameter.Split('=');
                if (keyValue.Length != 2)
                    continue;

                var key = keyValue[0];
                var value = keyValue[1];

                switch (key.ToLowerInvariant())
                {
                    case "auth":
                        var authParts = value.Split(':', 2);
                        if (authParts.Length == 2)
                            authentications.Add(new Authentication(authParts[0], Encoding.UTF8.GetBytes(authParts[1])));
                        break;
                    case "sessiontimeout":
                        if (long.TryParse(value, out var ms))
                            sessionTimeout = TimeSpan.FromMilliseconds(ms);
                        else if (TimeSpan.TryParse(value, out var t))
                            sessionTimeout = t;
                        break;
                    case "connectiontimeout":
                        if (long.TryParse(value, out ms))
                            connectionTimeout = TimeSpan.FromMilliseconds(ms);
                        else if (TimeSpan.TryParse(value, out var t))
                            connectionTimeout = t;
                        break;
                    case "readonly":
                        if (bool.TryParse(value, out var b))
                            readOnly = b;
                        break;
                }
            }
        }

        return new ZooKeeperConnection(hosts, authentications)
        {
            Root = root,
            SessionTimeout = sessionTimeout,
            ConnectionTimeout = connectionTimeout,
            ReadOnly = readOnly
        };
    }


    public bool Equals(ZooKeeperConnection? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Hosts.SetEquals(other.Hosts) &&
               Authentications.SetEquals(other.Authentications) &&
               SessionTimeout == other.SessionTimeout &&
               ConnectionTimeout == other.ConnectionTimeout &&
               Root == other.Root &&
               ReadOnly == other.ReadOnly;
    }

    public override int GetHashCode() =>
        HashCode.Combine(
            unchecked((uint)Hosts.Sum(h => (uint)h.GetHashCode())),
            unchecked((uint)Authentications.Sum(a => (uint)a.GetHashCode())),
            SessionTimeout,
            ConnectionTimeout,
            Root,
            ReadOnly
        );

    public override string ToString() =>
        $"zookeeper://{string.Join(',', Hosts)}{Root}" +
        $"?sessionTimeout={SessionTimeout.Milliseconds}" +
        $"&connectionTimeout={ConnectionTimeout.Milliseconds}" +
        $"&readOnly={ReadOnly}" +
        $"{(Authentications.Count == 0 ? string.Empty
            : '&' + string.Join('&', Authentications.Select(a => $"auth={a}"))
        )}";
}

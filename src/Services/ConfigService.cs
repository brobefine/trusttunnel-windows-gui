using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Tomlyn;
using Tomlyn.Model;
using TrustTunnelGui.Models;

namespace TrustTunnelGui.Services;

public static class ConfigService
{
    public static string ToToml(ServerProfile p)
    {
        var root = new TomlTable
        {
            ["loglevel"]                   = p.LogLevel,
            ["vpn_mode"]                   = p.VpnMode,
            ["killswitch_enabled"]         = p.KillswitchEnabled,
            ["killswitch_allow_ports"]     = ToLongArray(ServerProfile.SplitPorts(p.KillswitchAllowPorts)),
            ["post_quantum_group_enabled"] = p.PostQuantumGroupEnabled,
            ["exclusions"]                 = ToStringArray(ExpandDomains(ServerProfile.SplitLines(p.Exclusions))),
            ["dns_upstreams"]              = ToStringArray(ServerProfile.SplitLines(p.DnsUpstreams)),
        };

        var endpoint = new TomlTable
        {
            ["hostname"]                   = p.Hostname,
            ["addresses"]                  = ToStringArray(ServerProfile.SplitLines(p.Addresses)),
            ["has_ipv6"]                   = p.HasIpv6,
            ["username"]                   = p.Username,
            ["password"]                   = p.Password,
            ["client_random"]              = p.ClientRandom,
            ["skip_verification"]          = p.SkipVerification,
            ["certificate"]                = p.Certificate,
            ["upstream_protocol"]          = p.UpstreamProtocol,
            ["upstream_fallback_protocol"] = p.UpstreamFallbackProtocol,
            ["anti_dpi"]                   = p.AntiDpi,
        };
        root["endpoint"] = endpoint;

        if (p.TunEnabled || p.Socks5Enabled)
        {
            var listener = new TomlTable();

            // "type" discriminator tells the client which listener(s) are active
            listener["type"] = (p.TunEnabled, p.Socks5Enabled) switch
            {
                (true,  false) => "tun",
                (false, true)  => "socks5",
                (true,  true)  => "tun_and_socks5",
                _              => "tun",
            };

            if (p.TunEnabled)
            {
                var tun = new TomlTable
                {
                    ["bound_if"]        = p.TunBoundIf,
                    ["included_routes"] = ToStringArray(ServerProfile.SplitLines(p.TunIncludedRoutes)),
                    ["excluded_routes"] = ToStringArray(ServerProfile.SplitLines(p.TunExcludedRoutes)),
                };
                if (p.TunMtu > 0) tun["mtu"] = (long)p.TunMtu;
                listener["tun"] = tun;
            }

            if (p.Socks5Enabled)
            {
                var socks5 = new TomlTable { ["address"] = p.Socks5Address };
                if (!string.IsNullOrEmpty(p.Socks5Username)) socks5["username"] = p.Socks5Username;
                if (!string.IsNullOrEmpty(p.Socks5Password)) socks5["password"] = p.Socks5Password;
                listener["socks5"] = socks5;
            }

            root["listener"] = listener;
        }

        return Toml.FromModel(root);
    }

    public static void Save(ServerProfile p, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, ToToml(p));
    }

    private static List<string> ExpandDomains(List<string> entries)
    {
        var result = new List<string>(entries.Count * 2);
        var seen   = new HashSet<string>();
        foreach (var entry in entries)
        {
            if (seen.Add(entry)) result.Add(entry);
            if (!entry.StartsWith('*') && !entry.Contains('/') && !entry.Contains(':')
                && entry.Contains('.') && !IPAddress.TryParse(entry, out _))
            {
                var w = "*." + entry;
                if (seen.Add(w)) result.Add(w);
            }
        }
        return result;
    }

    private static string[] ToStringArray(List<string> items)        => items.ToArray();
    private static string[] ToStringArray(IEnumerable<string> items) => items.ToArray();
    private static long[]   ToLongArray(List<int> items)             => items.Select(i => (long)i).ToArray();
}

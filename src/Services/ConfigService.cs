using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tomlyn;
using Tomlyn.Model;
using TrustTunnelGui.Models;

namespace TrustTunnelGui.Services;

/// <summary>
/// Generates the TOML file expected by trusttunnel_client.exe.
/// Schema reference: TrustTunnel/TrustTunnelClient/trusttunnel/README.md
/// </summary>
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
            ["exclusions"]                 = ToStringArray(ServerProfile.SplitLines(p.Exclusions)),
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

        var listener = new TomlTable();
        if (p.TunEnabled)
        {
            listener["tun"] = new TomlTable
            {
                ["bound_if"]        = p.TunBoundIf,
                ["included_routes"] = ToStringArray(ServerProfile.SplitLines(p.TunIncludedRoutes)),
                ["excluded_routes"] = ToStringArray(ServerProfile.SplitLines(p.TunExcludedRoutes)),
            };
        }
        if (p.Socks5Enabled)
        {
            listener["socks5"] = new TomlTable
            {
                ["address"]  = p.Socks5Address,
                ["username"] = p.Socks5Username,
                ["password"] = p.Socks5Password,
            };
        }
        if (listener.Count > 0) root["listener"] = listener;

        return Toml.FromModel(root);
    }

    public static void Save(ServerProfile p, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, ToToml(p));
    }

    // Use plain .NET arrays, NOT TomlArray.
    // TomlArray inherits List<object?> — Add(string) resolves to Add(object?),
    // so Tomlyn receives boxed objects and may serialise the whole array as one
    // concatenated string instead of individual quoted elements.
    // Passing string[] / long[] lets Tomlyn's own type detection handle
    // serialisation correctly and guarantees ["a", "b", ...] output.

    private static string[] ToStringArray(List<string> items) => items.ToArray();

    private static long[] ToLongArray(List<int> items) => items.Select(i => (long)i).ToArray();
}

using System.IO;
using System.Linq;
using Tomlyn;
using Tomlyn.Model;
using TrustTunnelGui.Models;

namespace TrustTunnelGui.Services;

public static class TomlImport
{
    public static ServerProfile FromFile(string path, string fallbackName)
    {
        var toml = Toml.ToModel(File.ReadAllText(path));
        var p = new ServerProfile { Name = fallbackName };

        S(toml, "loglevel", v => p.LogLevel = v);
        S(toml, "vpn_mode", v => p.VpnMode = v);
        B(toml, "killswitch_enabled", v => p.KillswitchEnabled = v);
        if (toml["killswitch_allow_ports"] is TomlArray ports)
            p.KillswitchAllowPorts = string.Join(",", ports);
        B(toml, "post_quantum_group_enabled", v => p.PostQuantumGroupEnabled = v);
        if (toml["exclusions"] is TomlArray ex)
            p.Exclusions = string.Join("\n", ex.Cast<object?>().Select(x => x?.ToString()));
        if (toml["dns_upstreams"] is TomlArray dns)
            p.DnsUpstreams = string.Join("\n", dns.Cast<object?>().Select(x => x?.ToString()));

        if (toml["endpoint"] is TomlTable ep)
        {
            S(ep, "hostname", v => p.Hostname = v);
            if (ep["addresses"] is TomlArray addrs)
                p.Addresses = string.Join("\n", addrs.Cast<object?>().Select(x => x?.ToString()));
            B(ep, "has_ipv6", v => p.HasIpv6 = v);
            S(ep, "username", v => p.Username = v);
            S(ep, "password", v => p.Password = v);
            S(ep, "client_random", v => p.ClientRandom = v);
            B(ep, "skip_verification", v => p.SkipVerification = v);
            S(ep, "certificate", v => p.Certificate = v);
            S(ep, "upstream_protocol", v => p.UpstreamProtocol = v);
            S(ep, "upstream_fallback_protocol", v => p.UpstreamFallbackProtocol = v);
            B(ep, "anti_dpi", v => p.AntiDpi = v);
        }

        if (toml["listener"] is TomlTable l)
        {
            if (l["tun"] is TomlTable tun)
            {
                p.TunEnabled = true;
                S(tun, "bound_if", v => p.TunBoundIf = v);
                if (tun["included_routes"] is TomlArray ir)
                    p.TunIncludedRoutes = string.Join("\n", ir.Cast<object?>().Select(x => x?.ToString()));
                if (tun["excluded_routes"] is TomlArray er)
                    p.TunExcludedRoutes = string.Join("\n", er.Cast<object?>().Select(x => x?.ToString()));
            }
            if (l["socks5"] is TomlTable s5)
            {
                p.Socks5Enabled = true;
                S(s5, "address", v => p.Socks5Address = v);
                S(s5, "username", v => p.Socks5Username = v);
                S(s5, "password", v => p.Socks5Password = v);
            }
        }
        return p;
    }

    private static void S(TomlTable t, string key, System.Action<string> set)
    {
        if (t.TryGetValue(key, out var v) && v is string s) set(s);
    }
    private static void B(TomlTable t, string key, System.Action<bool> set)
    {
        if (t.TryGetValue(key, out var v) && v is bool b) set(b);
    }
}

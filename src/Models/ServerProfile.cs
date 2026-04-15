using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustTunnelGui.Models;

/// <summary>
/// Mirrors trusttunnel_client.toml schema.
/// Uses partial property syntax (CommunityToolkit 8.x) for WinUI 3 AOT compatibility.
/// </summary>
public partial class ServerProfile : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty] public partial string Name { get; set; } = "New server";

    // Top-level
    [ObservableProperty] public partial string LogLevel  { get; set; } = "info";
    [ObservableProperty] public partial string VpnMode   { get; set; } = "general";
    [ObservableProperty] public partial bool   KillswitchEnabled      { get; set; } = true;
    [ObservableProperty] public partial string KillswitchAllowPorts   { get; set; } = "";
    [ObservableProperty] public partial bool   PostQuantumGroupEnabled { get; set; } = true;
    [ObservableProperty] public partial string Exclusions   { get; set; } = "";
    [ObservableProperty] public partial string DnsUpstreams { get; set; } = "tls://1.1.1.1";

    // [endpoint]
    [ObservableProperty] public partial string Hostname   { get; set; } = "";
    [ObservableProperty] public partial string Addresses  { get; set; } = "";
    [ObservableProperty] public partial bool   HasIpv6    { get; set; } = true;
    [ObservableProperty] public partial string Username   { get; set; } = "";
    [ObservableProperty] public partial string Password   { get; set; } = "";
    [ObservableProperty] public partial string ClientRandom       { get; set; } = "";
    [ObservableProperty] public partial bool   SkipVerification   { get; set; } = false;
    [ObservableProperty] public partial string Certificate        { get; set; } = "";
    [ObservableProperty] public partial string UpstreamProtocol         { get; set; } = "http2";
    [ObservableProperty] public partial string UpstreamFallbackProtocol { get; set; } = "";
    [ObservableProperty] public partial bool   AntiDpi { get; set; } = false;

    // [listener.tun]
    [ObservableProperty] public partial bool   TunEnabled       { get; set; } = true;
    [ObservableProperty] public partial string TunBoundIf       { get; set; } = "";
    [ObservableProperty] public partial string TunIncludedRoutes { get; set; } = "0.0.0.0/0\n2000::/3";
    [ObservableProperty] public partial string TunExcludedRoutes { get; set; } = "";
    /// <summary>MTU for the TUN interface. 0 = let trusttunnel choose default.</summary>
    [ObservableProperty] public partial int    TunMtu { get; set; } = 0;

    // [listener.socks5]
    [ObservableProperty] public partial bool   Socks5Enabled  { get; set; } = false;
    [ObservableProperty] public partial string Socks5Address  { get; set; } = "127.0.0.1:1080";
    [ObservableProperty] public partial string Socks5Username { get; set; } = "";
    [ObservableProperty] public partial string Socks5Password { get; set; } = "";

    public static List<string> SplitLines(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new();
        var seen   = new HashSet<string>();
        var result = new List<string>();
        foreach (var raw in s.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = raw.Trim();
            if (t.Length > 0 && seen.Add(t)) result.Add(t);
        }
        return result;
    }

    public static List<int> SplitPorts(string s)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(s)) return result;
        foreach (var t in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(t, out var p)) result.Add(p);
        return result;
    }
}

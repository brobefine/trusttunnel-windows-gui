using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TrustTunnelGui.Models;

/// <summary>
/// Mirrors trusttunnel_client.toml schema.
/// Field names match TOML keys 1:1 (snake_case via [TomlPropertyName]).
/// </summary>
public partial class ServerProfile : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty] private string name = "New server";

    // Top-level
    [ObservableProperty] private string logLevel = "info";       // loglevel
    [ObservableProperty] private string vpnMode = "general";     // vpn_mode
    [ObservableProperty] private bool killswitchEnabled = true;  // killswitch_enabled
    [ObservableProperty] private string killswitchAllowPorts = ""; // CSV of ports
    [ObservableProperty] private bool postQuantumGroupEnabled = true;
    [ObservableProperty] private string exclusions = "";         // newline-separated
    [ObservableProperty] private string dnsUpstreams = "tls://1.1.1.1"; // newline-separated

    // [endpoint]
    [ObservableProperty] private string hostname = "";
    [ObservableProperty] private string addresses = "";   // newline-separated host[:port]
    [ObservableProperty] private bool hasIpv6 = true;
    [ObservableProperty] private string username = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string clientRandom = "";
    [ObservableProperty] private bool skipVerification = false;
    [ObservableProperty] private string certificate = "";       // PEM blob
    [ObservableProperty] private string upstreamProtocol = "http2";   // http2 | http3
    [ObservableProperty] private string upstreamFallbackProtocol = ""; // none | http2 | http3
    [ObservableProperty] private bool antiDpi = false;

    // [listener.tun]
    [ObservableProperty] private bool tunEnabled = true;
    [ObservableProperty] private string tunBoundIf = "";
    [ObservableProperty] private string tunIncludedRoutes = "0.0.0.0/0\n2000::/3";
    [ObservableProperty] private string tunExcludedRoutes = "";

    // [listener.socks5]
    [ObservableProperty] private bool socks5Enabled = false;
    [ObservableProperty] private string socks5Address = "127.0.0.1:1080";
    [ObservableProperty] private string socks5Username = "";
    [ObservableProperty] private string socks5Password = "";

    public static List<string> SplitLines(string s) =>
        string.IsNullOrWhiteSpace(s)
            ? new List<string>()
            : new List<string>(s.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries));

    public static List<int> SplitPorts(string s)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(s)) return result;
        foreach (var token in s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(token, out var p)) result.Add(p);
        return result;
    }
}

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;

namespace TrustTunnelGui.Models;

public partial class ServerProfile : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty] private string name = "New server";

    // Top-level
    [ObservableProperty] private string logLevel  = "info";
    [ObservableProperty] private string vpnMode   = "general";
    [ObservableProperty] private bool   killswitchEnabled = true;
    [ObservableProperty] private string killswitchAllowPorts = "";
    [ObservableProperty] private bool   postQuantumGroupEnabled = true;
    [ObservableProperty] private string exclusions   = "";
    [ObservableProperty] private string dnsUpstreams = "tls://1.1.1.1";

    // [endpoint]
    [ObservableProperty] private string hostname   = "";
    [ObservableProperty] private string addresses  = "";
    [ObservableProperty] private bool   hasIpv6    = true;
    [ObservableProperty] private string username   = "";
    [ObservableProperty] private string password   = "";
    [ObservableProperty] private string clientRandom = "";
    [ObservableProperty] private bool   skipVerification = false;
    [ObservableProperty] private string certificate = "";
    [ObservableProperty] private string upstreamProtocol = "http2";
    [ObservableProperty] private string upstreamFallbackProtocol = "";
    [ObservableProperty] private bool   antiDpi = false;

    // [listener.tun]
    [ObservableProperty] private bool   tunEnabled = true;
    [ObservableProperty] private string tunBoundIf = "";
    [ObservableProperty] private string tunIncludedRoutes = "0.0.0.0/0\n2000::/3";
    [ObservableProperty] private string tunExcludedRoutes = "";
    /// <summary>MTU for the TUN interface. 0 = let trusttunnel choose default.</summary>
    [ObservableProperty] private int    tunMtu = 0;

    // [listener.socks5]
    [ObservableProperty] private bool   socks5Enabled  = false;
    [ObservableProperty] private string socks5Address  = "127.0.0.1:1080";
    [ObservableProperty] private string socks5Username = "";
    [ObservableProperty] private string socks5Password = "";

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

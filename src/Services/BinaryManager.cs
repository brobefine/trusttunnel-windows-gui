using System;
using System.IO;

namespace TrustTunnelGui.Services;

/// <summary>
/// Resolves paths to trusttunnel_client.exe and wintun.dll.
/// Looks first in the app directory, then in user override path.
/// </summary>
public class BinaryManager
{
    public string AppDir { get; } = AppContext.BaseDirectory;
    public string UserOverridePath { get; set; } = "";

    public string ClientExePath
    {
        get
        {
            var local = Path.Combine(AppDir, "trusttunnel_client.exe");
            if (File.Exists(local)) return local;
            if (!string.IsNullOrEmpty(UserOverridePath))
            {
                var ovr = Path.Combine(UserOverridePath, "trusttunnel_client.exe");
                if (File.Exists(ovr)) return ovr;
            }
            return local; // return expected path even if missing — caller checks
        }
    }

    public string WintunDllPath => Path.Combine(
        Path.GetDirectoryName(ClientExePath) ?? AppDir, "wintun.dll");

    public bool ClientExeExists => File.Exists(ClientExePath);
    public bool WintunExists => File.Exists(WintunDllPath);
}

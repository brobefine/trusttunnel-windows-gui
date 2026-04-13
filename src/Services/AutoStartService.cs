using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace TrustTunnelGui.Services;

public static class AutoStartService
{
    private const string TaskName = "TrustTunnelGuiAutostart";
    private const string SettingsKey = @"Software\TrustTunnelGui";

    public static bool AutoConnect
    {
        get => ReadBool("AutoConnect", false);
        set => WriteBool("AutoConnect", value);
    }

    public static bool StartHidden
    {
        get => ReadBool("StartHidden", true);
        set => WriteBool("StartHidden", value);
    }

    public static bool IsAutostartEnabled()
    {
        var (code, _) = Run("schtasks", "/Query", "/TN", TaskName);
        return code == 0;
    }

    public static void EnableAutostart()
    {
        var exe = Process.GetCurrentProcess().MainModule!.FileName!;
        // /RL HIGHEST = run with highest privileges (без UAC)
        // /SC ONLOGON = триггер при входе пользователя
        Run("schtasks", "/Create", "/F", "/SC", "ONLOGON", "/RL", "HIGHEST",
            "/TN", TaskName, "/TR", $"\"{exe}\"");
    }

    public static void DisableAutostart()
    {
        Run("schtasks", "/Delete", "/F", "/TN", TaskName);
    }

    private static (int code, string output) Run(string exe, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, output);
    }

    private static bool ReadBool(string name, bool def)
    {
        using var key = Registry.CurrentUser.OpenSubKey(SettingsKey);
        return key?.GetValue(name) is int i ? i != 0 : def;
    }

    private static void WriteBool(string name, bool value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SettingsKey);
        key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
    }
}
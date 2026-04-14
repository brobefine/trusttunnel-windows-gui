using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TrustTunnelGui.Services;

public enum TunnelStatus { Stopped, Starting, Running, Stopping, Error }

public class TrustTunnelService
{
    private Process? _proc;
    public TunnelStatus Status { get; private set; } = TunnelStatus.Stopped;

    public event Action<string>? LogReceived;
    public event Action<TunnelStatus>? StatusChanged;

    public ConcurrentQueue<string> Buffer { get; } = new();
    private const int BufferLimit = 5000;

    public bool IsRunning => _proc is { HasExited: false };

    public async Task StartAsync(string exePath, string configPath)
    {
        if (IsRunning) return;
        if (!File.Exists(exePath))
        {
            EmitLog($"[gui] Binary not found: {exePath}");
            SetStatus(TunnelStatus.Error);
            return;
        }
        if (!File.Exists(configPath))
        {
            EmitLog($"[gui] Config not found: {configPath}");
            SetStatus(TunnelStatus.Error);
            return;
        }

        SetStatus(TunnelStatus.Starting);

        // Only reset the adapter if the user explicitly enabled it in settings.
        // By default this is OFF — resetting the adapter while the client is
        // starting causes "Couldn't detect active network interface" errors
        // because the NIC isn't fully up yet when trusttunnel_client.exe runs.
        if (AutoStartService.ResetAdapterOnConnect)
            await ResetActiveNetworkAdapterAsync();

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute  = false,
            CreateNoWindow   = true,
        };
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(configPath);

        try
        {
            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (_, e) => { if (e.Data != null) EmitLog(e.Data); };
            _proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) EmitLog(e.Data); };
            _proc.Exited += (_, _) =>
            {
                EmitLog($"[gui] Process exited with code {_proc?.ExitCode}");
                SetStatus(TunnelStatus.Stopped);
            };
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            SetStatus(TunnelStatus.Running);
            EmitLog($"[gui] Started PID {_proc.Id}");
        }
        catch (Exception ex)
        {
            EmitLog($"[gui] Failed to start: {ex.Message}");
            SetStatus(TunnelStatus.Error);
        }
    }

    public async Task StopAsync()
    {
        if (_proc is null || _proc.HasExited) { SetStatus(TunnelStatus.Stopped); return; }
        SetStatus(TunnelStatus.Stopping);
        try
        {
            var sentCtrlC = SendCtrlC(_proc.Id);
            if (sentCtrlC)
            {
                EmitLog("[gui] Sent Ctrl+C, waiting for graceful shutdown...");
                try
                {
                    await _proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (TimeoutException)
                {
                    EmitLog("[gui] Graceful shutdown timed out, killing process");
                    _proc.Kill(entireProcessTree: true);
                    await _proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                }
            }
            else
            {
                EmitLog("[gui] Could not send Ctrl+C, killing process");
                _proc.Kill(entireProcessTree: true);
                await _proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            EmitLog($"[gui] Stop error: {ex.Message}");
            try { _proc.Kill(entireProcessTree: true); } catch { }
        }
        SetStatus(TunnelStatus.Stopped);
    }

    public async Task<(int code, string output)> RunOnceAsync(string exePath, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute = false,
            CreateNoWindow  = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, stdout + stderr);
    }

    public Task<(int, string)> InstallServiceAsync(string exePath, string configPath) =>
        RunOnceAsync(exePath, "--service-install", "-c", configPath);

    public Task<(int, string)> UninstallServiceAsync(string exePath) =>
        RunOnceAsync(exePath, "--service-uninstall");

    private void EmitLog(string line)
    {
        Buffer.Enqueue(line);
        while (Buffer.Count > BufferLimit) Buffer.TryDequeue(out _);
        LogReceived?.Invoke(line);
    }

    private void SetStatus(TunnelStatus s)
    {
        if (Status == s) return;
        Status = s;
        StatusChanged?.Invoke(s);
    }

    private async Task ResetActiveNetworkAdapterAsync()
    {
        try
        {
            EmitLog("[gui] Resetting active network adapter...");
            var script =
                "$a = Get-NetAdapter -Physical | " +
                "Where-Object {$_.Status -eq 'Up' -and " +
                "$_.InterfaceDescription -notlike '*Wintun*' -and " +
                "$_.InterfaceDescription -notlike '*TAP*' -and " +
                "$_.InterfaceDescription -notlike '*WireGuard*'} | " +
                "Sort-Object -Property LinkSpeed -Descending | Select-Object -First 1; " +
                "if ($a) { " +
                "Write-Output \"Cycling adapter: $($a.Name)\"; " +
                "Disable-NetAdapter -Name $a.Name -Confirm:$false; " +
                "Start-Sleep -Seconds 3; " +
                "Enable-NetAdapter -Name $a.Name -Confirm:$false; " +
                // Wait until the adapter reports Up, up to 15 s
                "$deadline = (Get-Date).AddSeconds(15); " +
                "do { Start-Sleep -Seconds 1 } while " +
                "((Get-NetAdapter -Name $a.Name).Status -ne 'Up' -and (Get-Date) -lt $deadline); " +
                "Write-Output \"Adapter back up\"; " +
                "} else { Write-Output 'No active physical adapter found' }";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute = false,
                CreateNoWindow  = true,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(script);

            using var p = Process.Start(psi)!;
            var output = (await p.StandardOutput.ReadToEndAsync())
                       + (await p.StandardError.ReadToEndAsync());
            await p.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(output))
                foreach (var line in output.Split('\n'))
                    if (!string.IsNullOrWhiteSpace(line))
                        EmitLog($"[gui] {line.Trim()}");
        }
        catch (Exception ex)
        {
            EmitLog($"[gui] Network reset failed (non-fatal): {ex.Message}");
        }
    }

    // ---- Ctrl+C ----

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);

    private const uint CTRL_C_EVENT = 0;

    private static bool SendCtrlC(int pid)
    {
        try
        {
            FreeConsole();
            if (!AttachConsole((uint)pid)) return false;
            SetConsoleCtrlHandler(IntPtr.Zero, true);
            var ok = GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0);
            FreeConsole();
            SetConsoleCtrlHandler(IntPtr.Zero, false);
            return ok;
        }
        catch
        {
            try { FreeConsole(); SetConsoleCtrlHandler(IntPtr.Zero, false); } catch { }
            return false;
        }
    }
}

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

    // ── Job Object: kill child process when GUI exits for any reason ───────
    // When our process is terminated (Task Manager, crash, etc.) Windows
    // closes all HANDLEs, including the job handle, which triggers
    // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE and terminates the child process.
    private static readonly IntPtr s_jobHandle = CreateKillOnCloseJob();

    private static IntPtr CreateKillOnCloseJob()
    {
        try
        {
            var job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero) return IntPtr.Zero;

            // JOBOBJECT_BASIC_LIMIT_INFORMATION via raw buffer to avoid
            // struct padding differences between x86 and x64.
            // LimitFlags is always at byte offset 16 (after two LARGE_INTEGERs).
            int size = IntPtr.Size == 8 ? 64 : 44;
            var buf  = new byte[size];
            const uint KILL_ON_JOB_CLOSE = 0x00002000;
            BitConverter.GetBytes(KILL_ON_JOB_CLOSE).CopyTo(buf, 16);

            var pin = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try   { SetInformationJobObject(job, 2 /*BasicLimitInformation*/, pin.AddrOfPinnedObject(), size); }
            finally { pin.Free(); }

            return job;
        }
        catch { return IntPtr.Zero; }
    }

    // ── Start ──────────────────────────────────────────────────────────────

    public async Task StartAsync(string exePath, string configPath)
    {
        if (IsRunning) return;
        if (!File.Exists(exePath))   { EmitLog($"[gui] Binary not found: {exePath}");  SetStatus(TunnelStatus.Error); return; }
        if (!File.Exists(configPath)){ EmitLog($"[gui] Config not found: {configPath}"); SetStatus(TunnelStatus.Error); return; }

        SetStatus(TunnelStatus.Starting);

        WarnIfOtherVpnRunning();

        if (AutoStartService.ResetAdapterOnConnect)
            await ResetActiveNetworkAdapterAsync();

        var psi = new ProcessStartInfo
        {
            FileName         = exePath,
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
            _proc.Exited += OnProcExited;
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            // Assign to job so the child is killed if our process dies
            if (s_jobHandle != IntPtr.Zero)
                AssignProcessToJobObject(s_jobHandle, _proc.Handle);

            SetStatus(TunnelStatus.Running);
            EmitLog($"[gui] Started PID {_proc.Id}");
        }
        catch (Exception ex)
        {
            EmitLog($"[gui] Failed to start: {ex.Message}");
            SetStatus(TunnelStatus.Error);
        }
    }

    private void OnProcExited(object? sender, EventArgs e)
    {
        int code = -1;
        try { code = _proc?.ExitCode ?? -1; } catch { }
        EmitLog($"[gui] Process exited with code {code}");
        SetStatus(TunnelStatus.Stopped);
    }

    // ── Stop ───────────────────────────────────────────────────────────────

    public async Task StopAsync()
    {
        if (_proc is null || _proc.HasExited)
        {
            SetStatus(TunnelStatus.Stopped);
            return;
        }

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
                    KillSafe();
                    try { await _proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)); } catch { }
                }
            }
            else
            {
                EmitLog("[gui] Could not send Ctrl+C, killing process");
                KillSafe();
                try { await _proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
            }
        }
        catch (Exception ex)
        {
            EmitLog($"[gui] Stop error: {ex.Message}");
            KillSafe();
        }

        SetStatus(TunnelStatus.Stopped);
    }

    private void KillSafe()
    {
        try { _proc?.Kill(entireProcessTree: true); } catch { }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

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

    /// <summary>
    /// Logs a warning if known VPN / proxy processes are already running.
    /// Does not block startup — trusttunnel supports running alongside some tools.
    /// </summary>
    private void WarnIfOtherVpnRunning()
    {
        string[] knownVpnProcesses = {
            "v2rayN", "v2ray", "xray", "sing-box", "clash", "clash-verge",
            "wireguard", "wg-service", "openvpn", "nordvpn", "expressvpn",
        };

        var found = new System.Collections.Generic.List<string>();
        foreach (var name in knownVpnProcesses)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0) found.Add(name);
                foreach (var p in procs) p.Dispose();
            }
            catch { }
        }

        if (found.Count > 0)
            EmitLog($"[gui] WARNING: other VPN processes running: {string.Join(", ", found)}. " +
                    "This may conflict with TUN adapter creation.");
    }

    /// <summary>
    /// Resets the active NIC to clear residual state from other VPN clients.
    /// Uses the same approach as the reference .bat:
    ///   1. Kill v2rayN if running and clear its proxy settings
    ///   2. netsh disable → wait → netsh enable → wait for IP
    /// </summary>
    private async Task ResetActiveNetworkAdapterAsync()
    {
        try
        {
            EmitLog("[gui] Resetting active network adapter...");

            // Step 1: kill v2rayN and clear its WinInet proxy if present
            var killScript =
                "$v2 = Get-Process -Name 'v2rayN' -ErrorAction SilentlyContinue; " +
                "if ($v2) { " +
                "  Stop-Process -Name 'v2rayN' -Force; " +
                "  Start-Sleep -Seconds 2; " +
                "  # Clear WinInet proxy that v2rayN leaves behind " +
                "  $path = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings'; " +
                "  Set-ItemProperty -Path $path -Name ProxyEnable -Value 0 -ErrorAction SilentlyContinue; " +
                "  Set-ItemProperty -Path $path -Name ProxyServer -Value '' -ErrorAction SilentlyContinue; " +
                "  Write-Output 'Killed v2rayN and cleared proxy'; " +
                "} ";

            await RunPowerShell(killScript);

            // Step 2: find the active physical NIC name and toggle via netsh
            var toggleScript =
                "$a = Get-NetAdapter -Physical | " +
                "  Where-Object {$_.Status -eq 'Up' -and " +
                "    $_.InterfaceDescription -notlike '*Wintun*' -and " +
                "    $_.InterfaceDescription -notlike '*TAP*' -and " +
                "    $_.InterfaceDescription -notlike '*WireGuard*'} | " +
                "  Sort-Object LinkSpeed -Descending | Select-Object -First 1; " +
                "if (-not $a) { Write-Output 'No active physical adapter found'; exit 0 } " +
                "Write-Output \"Cycling adapter: $($a.Name)\"; " +
                "netsh interface set interface \"$($a.Name)\" disable | Out-Null; " +
                "Start-Sleep -Seconds 3; " +
                "netsh interface set interface \"$($a.Name)\" enable | Out-Null; " +
                // Poll until the adapter has an IP address (link Up is not enough)
                "$deadline = (Get-Date).AddSeconds(20); " +
                "do { " +
                "  Start-Sleep -Seconds 1; " +
                "  $up = (Get-NetAdapter -Name $a.Name -ErrorAction SilentlyContinue).Status -eq 'Up'; " +
                "  $hasIp = (Get-NetIPAddress -InterfaceAlias $a.Name -AddressFamily IPv4 -ErrorAction SilentlyContinue) -ne $null; " +
                "} while ((-not $up -or -not $hasIp) -and (Get-Date) -lt $deadline); " +
                "Write-Output 'Adapter ready'";

            await RunPowerShell(toggleScript);
        }
        catch (Exception ex)
        {
            EmitLog($"[gui] Network reset failed (non-fatal): {ex.Message}");
        }
    }

    private async Task RunPowerShell(string script)
    {
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

        foreach (var line in output.Split('\n'))
            if (!string.IsNullOrWhiteSpace(line))
                EmitLog($"[gui] {line.Trim()}");
    }

    // ── P/Invoke ───────────────────────────────────────────────────────────

    [DllImport("kernel32.dll")] static extern IntPtr CreateJobObject(IntPtr a, string? n);
    [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int cls, IntPtr info, int sz);
    [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr proc);

    [DllImport("kernel32.dll", SetLastError = true)] static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool FreeConsole();
    [DllImport("kernel32.dll")] static extern bool GenerateConsoleCtrlEvent(uint ev, uint pg);
    [DllImport("kernel32.dll")] static extern bool SetConsoleCtrlHandler(IntPtr h, bool add);

    private static bool SendCtrlC(int pid)
    {
        try
        {
            FreeConsole();
            if (!AttachConsole((uint)pid)) return false;
            SetConsoleCtrlHandler(IntPtr.Zero, true);
            var ok = GenerateConsoleCtrlEvent(0 /*CTRL_C*/, 0);
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

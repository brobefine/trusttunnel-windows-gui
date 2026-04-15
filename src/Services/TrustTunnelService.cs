using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
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

    // ── Job Object: kill child when GUI dies for any reason ────────────────
    private static readonly IntPtr s_jobHandle = CreateKillOnCloseJob();

    private static IntPtr CreateKillOnCloseJob()
    {
        try
        {
            var job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero) return IntPtr.Zero;
            int size = IntPtr.Size == 8 ? 64 : 44;
            var buf  = new byte[size];
            BitConverter.GetBytes(0x00002000u /*KILL_ON_JOB_CLOSE*/).CopyTo(buf, 16);
            var pin = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try   { SetInformationJobObject(job, 2, pin.AddrOfPinnedObject(), size); }
            finally { pin.Free(); }
            return job;
        }
        catch { return IntPtr.Zero; }
    }

    // ── Start ──────────────────────────────────────────────────────────────

    public async Task StartAsync(string exePath, string configPath)
    {
        if (IsRunning) return;
        if (!File.Exists(exePath))    { EmitLog($"[gui] Binary not found: {exePath}");   SetStatus(TunnelStatus.Error); return; }
        if (!File.Exists(configPath)) { EmitLog($"[gui] Config not found: {configPath}"); SetStatus(TunnelStatus.Error); return; }

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
            _proc.Exited += (_, _) =>
            {
                int code = -1;
                try { code = _proc.ExitCode; } catch { }
                EmitLog($"[gui] Process exited with code {code}");
                SetStatus(TunnelStatus.Stopped);
            };
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();

            // Assign to job object — child dies when GUI process dies
            if (s_jobHandle != IntPtr.Zero)
            {
                try { AssignProcessToJobObject(s_jobHandle, _proc.SafeHandle.DangerousGetHandle()); }
                catch { /* non-fatal if already in another job */ }
            }

            SetStatus(TunnelStatus.Running);
            EmitLog($"[gui] Started PID {_proc.Id}");
        }
        catch (Exception ex)
        {
            EmitLog($"[gui] Failed to start: {ex.Message}");
            SetStatus(TunnelStatus.Error);
        }
    }

    // ── Stop ───────────────────────────────────────────────────────────────

    public async Task StopAsync()
    {
        var proc = _proc;
        if (proc is null || proc.HasExited)
        {
            SetStatus(TunnelStatus.Stopped);
            return;
        }

        SetStatus(TunnelStatus.Stopping);

        try
        {
            var sentCtrlC = SendCtrlC(proc.Id);
            if (sentCtrlC)
            {
                EmitLog("[gui] Sent Ctrl+C, waiting for graceful shutdown...");
                try
                {
                    await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (TimeoutException)
                {
                    EmitLog("[gui] Graceful shutdown timed out, killing process");
                    KillSafe(proc);
                    await WaitSafe(proc, 3);
                }
            }
            else
            {
                EmitLog("[gui] Could not send Ctrl+C, killing process");
                KillSafe(proc);
                await WaitSafe(proc, 5);
            }
        }
        catch (Exception ex)
        {
            EmitLog($"[gui] Stop error: {ex.Message}");
            KillSafe(proc);
        }

        SetStatus(TunnelStatus.Stopped);
    }

    private static void KillSafe(Process p)
    {
        try { p.Kill(entireProcessTree: true); } catch { }
    }

    private static async Task WaitSafe(Process p, int seconds)
    {
        try { await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(seconds)); }
        catch { }
    }

    // ── Misc ───────────────────────────────────────────────────────────────

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

    private void WarnIfOtherVpnRunning()
    {
        string[] names = { "v2rayN","v2ray","xray","sing-box","clash","clash-verge","wireguard","wg-service","openvpn","nordvpn","expressvpn" };
        var found = new System.Collections.Generic.List<string>();
        foreach (var n in names)
        {
            try
            {
                var pp = Process.GetProcessesByName(n);
                if (pp.Length > 0) found.Add(n);
                foreach (var x in pp) x.Dispose();
            }
            catch { }
        }
        if (found.Count > 0)
            EmitLog($"[gui] WARNING: other VPN processes running: {string.Join(", ", found)}. May conflict with TUN.");
    }

    private async Task ResetActiveNetworkAdapterAsync()
    {
        try
        {
            EmitLog("[gui] Resetting active network adapter...");

            await RunPowerShell(
                "$v2 = Get-Process -Name 'v2rayN' -ErrorAction SilentlyContinue; " +
                "if ($v2) { Stop-Process -Name 'v2rayN' -Force; Start-Sleep -Seconds 2; " +
                "$p='HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings';" +
                "Set-ItemProperty $p ProxyEnable 0 -EA 0; Set-ItemProperty $p ProxyServer '' -EA 0;" +
                "Write-Output 'Killed v2rayN and cleared proxy' }");

            await RunPowerShell(
                "$a = Get-NetAdapter -Physical | " +
                "  Where-Object {$_.Status -eq 'Up' -and $_.InterfaceDescription -notlike '*Wintun*' " +
                "    -and $_.InterfaceDescription -notlike '*TAP*' -and $_.InterfaceDescription -notlike '*WireGuard*'} | " +
                "  Sort-Object LinkSpeed -Descending | Select-Object -First 1; " +
                "if (-not $a) { Write-Output 'No active physical adapter found'; exit 0 } " +
                "Write-Output \"Cycling adapter: $($a.Name)\"; " +
                "netsh interface set interface \"$($a.Name)\" disable | Out-Null; " +
                "Start-Sleep -Seconds 3; " +
                "netsh interface set interface \"$($a.Name)\" enable | Out-Null; " +
                "$deadline = (Get-Date).AddSeconds(20); " +
                "do { Start-Sleep 1; $up=(Get-NetAdapter -Name $a.Name -EA 0).Status -eq 'Up';" +
                "  $ip=(Get-NetIPAddress -InterfaceAlias $a.Name -AddressFamily IPv4 -EA 0) -ne $null " +
                "} while ((-not $up -or -not $ip) -and (Get-Date) -lt $deadline); " +
                "Write-Output 'Adapter ready'");
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
        var output = (await p.StandardOutput.ReadToEndAsync()) + (await p.StandardError.ReadToEndAsync());
        await p.WaitForExitAsync();
        foreach (var line in output.Split('\n'))
            if (!string.IsNullOrWhiteSpace(line)) EmitLog($"[gui] {line.Trim()}");
    }

    // ── P/Invoke ───────────────────────────────────────────────────────────

    [DllImport("kernel32.dll")] static extern IntPtr CreateJobObject(IntPtr a, string? n);
    [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int cls, IntPtr info, int sz);
    [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr proc);

    [DllImport("kernel32.dll", SetLastError = true)] static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool FreeConsole();
    [DllImport("kernel32.dll")] static extern bool GenerateConsoleCtrlEvent(uint ev, uint pg);

    // Use a typed delegate — NULL handler (IntPtr.Zero) is unreliable on GUI processes
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool ConsoleCtrlHandlerDelegate(uint ctrlType);
    [DllImport("kernel32.dll")] static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandlerDelegate? h, bool add);

    // Keep delegate alive (GC must not collect it while installed)
    private static ConsoleCtrlHandlerDelegate? _ctrlDelegate;
    private static readonly object _ctrlLock = new();

    /// <summary>
    /// Sends Ctrl+C to the child process without crashing the GUI.
    /// The GUI app has no console by default, so we temporarily attach to
    /// the child's console, install a handler that absorbs ALL ctrl signals
    /// for our process, fire the event, hold the handler for 500 ms so the
    /// signal reaches the child, then detach.
    /// </summary>
    private static bool SendCtrlC(int pid)
    {
        lock (_ctrlLock)
        {
            try
            {
                FreeConsole();
                if (!AttachConsole((uint)pid)) return false;

                // Install BEFORE generating — absorb every ctrl event for our process
                _ctrlDelegate = _ => true;
                SetConsoleCtrlHandler(_ctrlDelegate, true);

                var ok = GenerateConsoleCtrlEvent(0 /*CTRL_C_EVENT*/, 0);

                // Hold handler for 500 ms so the async signal reaches the child
                Thread.Sleep(500);

                SetConsoleCtrlHandler(_ctrlDelegate, false);
                _ctrlDelegate = null;
                FreeConsole();
                return ok;
            }
            catch
            {
                try
                {
                    if (_ctrlDelegate != null) SetConsoleCtrlHandler(_ctrlDelegate, false);
                    _ctrlDelegate = null;
                    FreeConsole();
                }
                catch { }
                return false;
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
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

    public void Start(string exePath, string configPath)
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

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--config");
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
            // Try graceful first via CloseMainWindow, fallback to Kill
            if (!_proc.CloseMainWindow())
            {
                _proc.Kill(entireProcessTree: true);
            }
            await _proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch { try { _proc.Kill(entireProcessTree: true); } catch { } }
        SetStatus(TunnelStatus.Stopped);
    }

    /// <summary>Runs trusttunnel_client.exe with arbitrary args and returns stdout+stderr.</summary>
    public async Task<(int code, string output)> RunOnceAsync(string exePath, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        return (p.ExitCode, stdout + stderr);
    }

    public Task<(int, string)> InstallServiceAsync(string exePath, string configPath) =>
        RunOnceAsync(exePath, "--service-install", "--config", configPath);

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
}

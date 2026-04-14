using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TrustTunnelGui.Models;
using TrustTunnelGui.Services;

namespace TrustTunnelGui.Views;

public sealed partial class ConnectionPage : Page
{
    private const int MaxLines = 2000;
    private readonly List<string> _lines = new(MaxLines + 10);

    public ConnectionPage()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ProfileBox.ItemsSource  = App.Profiles.Profiles;
        ProfileBox.SelectedItem = App.Profiles.Active ?? App.Profiles.Profiles.FirstOrDefault();

        // Populate log from buffer
        _lines.Clear();
        foreach (var line in App.Tunnel.Buffer) _lines.Add(line);
        RebuildLogText();

        App.Tunnel.LogReceived   += OnLog;
        App.Tunnel.StatusChanged += OnStatus;
        OnStatus(App.Tunnel.Status);

        if (!App.Binaries.ClientExeExists)
        {
            WarnBar.IsOpen  = true;
            WarnBar.Title   = "Бинарь не найден";
            WarnBar.Message = $"Положите trusttunnel_client.exe и wintun.dll рядом с приложением:\n{App.Binaries.AppDir}";
        }
        else if (!App.Binaries.WintunExists)
        {
            WarnBar.IsOpen  = true;
            WarnBar.Title   = "Нет wintun.dll";
            WarnBar.Message = "TUN-листенер не запустится без wintun.dll";
        }

        if (AutoScroll.IsOn) ScrollToEnd();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Tunnel.LogReceived   -= OnLog;
        App.Tunnel.StatusChanged -= OnStatus;
    }

    // ── Logging ────────────────────────────────────────────────────────────

    private void OnLog(string line) => Ui.Run(() =>
    {
        _lines.Add(line);
        if (_lines.Count > MaxLines) _lines.RemoveAt(0);

        LogBox.Text += line + "\n";
        // Keep TextBox in sync with the capped _lines list
        if (_lines.Count == MaxLines)
        {
            var idx = LogBox.Text.IndexOf('\n');
            if (idx >= 0) LogBox.Text = LogBox.Text[(idx + 1)..];
        }

        if (AutoScroll.IsOn) ScrollToEnd();
    });

    private void RebuildLogText()
    {
        var sb = new StringBuilder();
        foreach (var l in _lines) sb.Append(l).Append('\n');
        LogBox.Text = sb.ToString();
    }

    private void ScrollToEnd()
    {
        var len = LogBox.Text.Length;
        if (len > 0)
        {
            LogBox.SelectionStart  = len;
            LogBox.SelectionLength = 0;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _lines.Clear();
        LogBox.Text = string.Empty;
        App.Tunnel.Buffer.Clear();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            var path = Win32FileDialog.ShowSave(
                hwnd,
                "Log file\0*.log\0Text file\0*.txt\0All files\0*.*\0\0",
                "log",
                $"trusttunnel-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            if (!string.IsNullOrEmpty(path))
                System.IO.File.WriteAllText(path, LogBox.Text);
        }
        catch { }
    }

    // ── Status ─────────────────────────────────────────────────────────────

    private void OnStatus(TunnelStatus s) => Ui.Run(() =>
    {
        (StatusText.Text, var color, var enableConnect, var enableDisconnect) = s switch
        {
            TunnelStatus.Stopped  => ("Отключено",    Colors.Gray,      true,  false),
            TunnelStatus.Starting => ("Запуск...",    Colors.Orange,    false, false),
            TunnelStatus.Running  => ("Подключено",   Colors.LimeGreen, false, true ),
            TunnelStatus.Stopping => ("Остановка...", Colors.Orange,    false, false),
            TunnelStatus.Error    => ("Ошибка",       Colors.OrangeRed, true,  false),
            _                     => ("?",            Colors.Gray,      true,  false)
        };
        StatusIcon.Foreground   = new SolidColorBrush(color);
        ConnectBtn.IsEnabled    = enableConnect && ProfileBox.SelectedItem != null;
        DisconnectBtn.IsEnabled = enableDisconnect;
    });

    private void ProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileBox.SelectedItem is ServerProfile p)
        {
            App.Profiles.SetActive(p);
            ConnectBtn.IsEnabled = App.Tunnel.Status is TunnelStatus.Stopped or TunnelStatus.Error;
        }
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not ServerProfile p) return;
        try
        {
            var path = App.Profiles.ConfigPathFor(p);
            ConfigService.Save(p, path);
            await App.Tunnel.StartAsync(App.Binaries.ClientExePath, path);
        }
        catch (Exception ex)
        {
            App.Tunnel.Buffer.Enqueue($"[gui] Connect error: {ex.Message}");
        }
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await App.Tunnel.StopAsync();
        }
        catch (Exception ex)
        {
            App.Tunnel.Buffer.Enqueue($"[gui] Disconnect error: {ex.Message}");
        }
    }
}

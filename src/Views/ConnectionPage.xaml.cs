using System;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TrustTunnelGui.Models;
using TrustTunnelGui.Services;

namespace TrustTunnelGui.Views;

public sealed partial class ConnectionPage : Page
{
    // Keep only the last N lines in the mini log tail on the main page.
    private const int TailMaxLines = 200;
    private int _tailLineCount = 0;

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
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Tunnel.LogReceived   -= OnLog;
        App.Tunnel.StatusChanged -= OnStatus;
    }

    private void OnLog(string line) => Ui.Run(() =>
    {
        LogTail.Text += line + "\n";
        _tailLineCount++;

        // Trim by line count, not character count — more predictable behaviour.
        if (_tailLineCount > TailMaxLines)
        {
            var idx = LogTail.Text.IndexOf('\n');
            if (idx >= 0) LogTail.Text = LogTail.Text[(idx + 1)..];
            _tailLineCount--;
        }

        LogScroll.ChangeView(null, double.MaxValue, null, disableAnimation: true);
    });

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
        StatusIcon.Foreground = new SolidColorBrush(color);
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
        var path = App.Profiles.ConfigPathFor(p);
        ConfigService.Save(p, path);
        await App.Tunnel.StartAsync(App.Binaries.ClientExePath, path);
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        await App.Tunnel.StopAsync();
    }
}

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using TrustTunnelGui.Services;

namespace TrustTunnelGui.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Refresh();
        AutostartToggle.IsOn    = AutoStartService.IsAutostartEnabled();
        AutoConnectToggle.IsOn  = AutoStartService.AutoConnect;
        StartHiddenToggle.IsOn  = AutoStartService.StartHidden;
        ResetAdapterToggle.IsOn = AutoStartService.ResetAdapterOnConnect;
    }

    private void Autostart_Toggled(object sender, RoutedEventArgs e)
    {
        if (AutostartToggle.IsOn) AutoStartService.EnableAutostart();
        else                       AutoStartService.DisableAutostart();
    }

    private void AutoConnect_Toggled(object sender, RoutedEventArgs e)
        => AutoStartService.AutoConnect = AutoConnectToggle.IsOn;

    private void StartHidden_Toggled(object sender, RoutedEventArgs e)
        => AutoStartService.StartHidden = StartHiddenToggle.IsOn;

    private void ResetAdapter_Toggled(object sender, RoutedEventArgs e)
        => AutoStartService.ResetAdapterOnConnect = ResetAdapterToggle.IsOn;

    private void Refresh()
    {
        ExePathText.Text    = $"Клиент: {App.Binaries.ClientExePath}  ({(App.Binaries.ClientExeExists ? "OK" : "НЕТ")})";
        WintunPathText.Text = $"wintun.dll: {App.Binaries.WintunDllPath}  ({(App.Binaries.WintunExists ? "OK" : "НЕТ")})";
    }

    private void OpenConfigs_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrustTunnelGui");
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    private void MainScroll_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        var point = e.GetCurrentPoint(sv);
        if (point.Properties.IsHorizontalMouseWheel) return;
        var delta = point.Properties.MouseWheelDelta * 2.0;
        var newOffset = Math.Clamp(sv.VerticalOffset - delta, 0, sv.ScrollableHeight);
        sv.ChangeView(null, newOffset, null, disableAnimation: false);
        e.Handled = true;
    }
}

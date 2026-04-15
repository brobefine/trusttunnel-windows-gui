using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrustTunnelGui.Services;

namespace TrustTunnelGui.Views;

public sealed partial class SettingsPage : Page
{
    private ReleaseInfo? _pendingRelease;
    private CancellationTokenSource? _updateCts;

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

    private void AutoConnect_Toggled(object sender, RoutedEventArgs e) => AutoStartService.AutoConnect = AutoConnectToggle.IsOn;
    private void StartHidden_Toggled(object sender, RoutedEventArgs e) => AutoStartService.StartHidden = StartHiddenToggle.IsOn;
    private void ResetAdapter_Toggled(object sender, RoutedEventArgs e) => AutoStartService.ResetAdapterOnConnect = ResetAdapterToggle.IsOn;

    private void Refresh()
    {
        ExePathText.Text    = $"Клиент: {App.Binaries.ClientExePath}  ({(App.Binaries.ClientExeExists ? "OK" : "НЕТ")})";
        WintunPathText.Text = $"wintun.dll: {App.Binaries.WintunDllPath}  ({(App.Binaries.WintunExists ? "OK" : "НЕТ")})";
    }

    private void OpenConfigs_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrustTunnelGui");
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    // ── Updates ────────────────────────────────────────────────────────────

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        UpdateBar.IsOpen = false;
        SetInfo("Проверка обновлений...", InfoBarSeverity.Informational);

        _pendingRelease = await UpdateService.GetLatestClientReleaseAsync();

        if (_pendingRelease == null)
        {
            SetInfo("Не удалось получить информацию о релизах. Проверьте интернет-соединение.", InfoBarSeverity.Warning);
        }
        else
        {
            SetInfo($"Последняя версия: {_pendingRelease.TagName}  ({_pendingRelease.AssetName})", InfoBarSeverity.Success);
            UpdateClientBtn.IsEnabled = true;
        }
        CheckUpdateBtn.IsEnabled = true;
    }

    private async void UpdateClient_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingRelease == null) return;
        if (App.Tunnel.IsRunning)
        {
            SetInfo("Остановите VPN перед обновлением.", InfoBarSeverity.Warning);
            return;
        }

        SetBusy(true);
        _updateCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<double>(v => UpdateProgress.Value = v * 100);
            var path = await UpdateService.UpdateClientAsync(
                _pendingRelease, App.Binaries.AppDir, progress, _updateCts.Token);
            SetInfo($"Обновлено: {path}", InfoBarSeverity.Success);
            Refresh();
            UpdateClientBtn.IsEnabled = false;
        }
        catch (OperationCanceledException)
        {
            SetInfo("Обновление отменено.", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            SetInfo($"Ошибка: {ex.Message}", InfoBarSeverity.Error);
        }
        finally { SetBusy(false); }
    }

    private async void UpdateWintun_Click(object sender, RoutedEventArgs e)
    {
        if (App.Tunnel.IsRunning)
        {
            SetInfo("Остановите VPN перед обновлением wintun.", InfoBarSeverity.Warning);
            return;
        }
        SetBusy(true);
        try
        {
            var progress = new Progress<double>(v => UpdateProgress.Value = v * 100);
            var path = await UpdateService.UpdateWintunAsync(App.Binaries.AppDir, progress);
            SetInfo($"wintun обновлён: {path}", InfoBarSeverity.Success);
            Refresh();
        }
        catch (Exception ex) { SetInfo($"Ошибка: {ex.Message}", InfoBarSeverity.Error); }
        finally { SetBusy(false); }
    }

    private void SetInfo(string msg, InfoBarSeverity severity)
    {
        UpdateBar.Message  = msg;
        UpdateBar.Severity = severity;
        UpdateBar.IsOpen   = true;
    }

    private void SetBusy(bool busy)
    {
        CheckUpdateBtn.IsEnabled  = !busy;
        UpdateClientBtn.IsEnabled = !busy;
        UpdateWintunBtn.IsEnabled = !busy;
        UpdateProgress.Value      = 0;
        UpdateProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
}

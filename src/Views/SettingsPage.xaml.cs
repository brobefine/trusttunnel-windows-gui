using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrustTunnelGui.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;

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
        ServiceProfileBox.ItemsSource = App.Profiles.Profiles;
        ServiceProfileBox.SelectedItem = App.Profiles.Active ?? App.Profiles.Profiles.FirstOrDefault();
    }

    private void Refresh()
    {
        ExePathText.Text    = $"Клиент: {App.Binaries.ClientExePath}  ({(App.Binaries.ClientExeExists ? "OK" : "НЕТ")})";
        WintunPathText.Text = $"wintun.dll: {App.Binaries.WintunDllPath}  ({(App.Binaries.WintunExists ? "OK" : "НЕТ")})";
    }

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder == null) return;
        App.Binaries.UserOverridePath = folder.Path;
        Refresh();
    }

    private void OpenConfigs_Click(object sender, RoutedEventArgs e)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrustTunnelGui");
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    private async void ServiceInstall_Click(object sender, RoutedEventArgs e)
    {
        if (ServiceProfileBox.SelectedItem is not ServerProfile p) return;
        if (!App.Binaries.ClientExeExists) { ServiceOutput.Text = "Бинарь не найден"; return; }
        var path = App.Profiles.ConfigPathFor(p);
        Services.ConfigService.Save(p, path);
        var (code, output) = await App.Tunnel.InstallServiceAsync(App.Binaries.ClientExePath, path);
        ServiceOutput.Text = $"exit={code}\n{output}";
    }

    private async void ServiceUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (!App.Binaries.ClientExeExists) { ServiceOutput.Text = "Бинарь не найден"; return; }
        var (code, output) = await App.Tunnel.UninstallServiceAsync(App.Binaries.ClientExePath);
        ServiceOutput.Text = $"exit={code}\n{output}";
    }
}

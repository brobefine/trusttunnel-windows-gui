using System;
using System.IO;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrustTunnelGui.Models;
using TrustTunnelGui.Services;

namespace TrustTunnelGui.Views;

public sealed partial class ServersPage : Page
{
    private static readonly HttpClient _http = new();

    public ServersPage()
    {
        InitializeComponent();
        Loaded += (_, _) => List.ItemsSource = App.Profiles.Profiles;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var p = new ServerProfile();
        App.Profiles.Add(p);
        Frame.Navigate(typeof(ServerEditPage), p.Id);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is ServerProfile p)
            Frame.Navigate(typeof(ServerEditPage), p.Id);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ServerProfile p) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = "Удалить сервер?", Content = p.Name,
            PrimaryButtonText = "Удалить", CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            App.Profiles.Remove(p);
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            var path = Win32FileDialog.ShowOpen(hwnd, "TOML config\0*.toml\0All files\0*.*\0\0");
            if (string.IsNullOrEmpty(path)) return;
            var p = TomlImport.FromFile(path, Path.GetFileNameWithoutExtension(path));
            App.Profiles.Add(p);
        }
        catch (Exception ex) { await ShowError(ex.Message); }
    }

    /// <summary>
    /// Import a server profile from a URL pointing to a TOML config.
    /// Usage: paste a direct TOML URL (e.g. raw GitHub link or your server's share endpoint).
    /// </summary>
    private async void ImportUrl_Click(object sender, RoutedEventArgs e)
    {
        var tb = new TextBox { PlaceholderText = "https://example.com/profile.toml", MinWidth = 400 };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = "Импорт профиля по URL",
            Content = tb, PrimaryButtonText = "Импорт", CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        var url = tb.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "trusttunnel-gui");
            var toml = await _http.GetStringAsync(url);
            var tmp  = Path.GetTempFileName();
            await File.WriteAllTextAsync(tmp, toml);
            var name = Path.GetFileNameWithoutExtension(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(name)) name = "imported";
            var p = TomlImport.FromFile(tmp, name);
            File.Delete(tmp);
            App.Profiles.Add(p);
            Frame.Navigate(typeof(ServerEditPage), p.Id);
        }
        catch (Exception ex) { await ShowError(ex.Message); }
    }

    private async System.Threading.Tasks.Task ShowError(string msg)
    {
        await new ContentDialog
        {
            XamlRoot = XamlRoot, Title = "Ошибка", Content = msg, CloseButtonText = "OK"
        }.ShowAsync();
    }
}

using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TrustTunnelGui.Models;
using TrustTunnelGui.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.IO;

namespace TrustTunnelGui.Views;

public sealed partial class ServerEditPage : Page
{
    public ServerProfile P { get; private set; } = new();

    public ServerEditPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is Guid id)
        {
            var found = App.Profiles.Profiles.FirstOrDefault(x => x.Id == id);
            if (found != null) P = found;
        }
        Bindings.Update();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        App.Profiles.Save();
        StatusBar.Severity = InfoBarSeverity.Success;
        StatusBar.Title = "Сохранено";
        StatusBar.IsOpen = true;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            var path = Win32FileDialog.ShowSave(
                hwnd,
                "TOML config\0*.toml\0All files\0*.*\0\0",
                "toml",
                $"{SafeName(P.Name)}.toml");

            if (string.IsNullOrEmpty(path))
            {
                StatusBar.Severity = InfoBarSeverity.Informational;
                StatusBar.Title = "Отменено";
                StatusBar.IsOpen = true;
                return;
            }

            File.WriteAllText(path, ConfigService.ToToml(P));
            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Title = $"Экспортировано: {path}";
            StatusBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.Title = "Ошибка экспорта";
            StatusBar.Message = ex.Message;
            StatusBar.IsOpen = true;
        }
    }

    private static string SafeName(string n)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            n = n.Replace(c, '_');
        return string.IsNullOrWhiteSpace(n) ? "profile" : n;
    }
}

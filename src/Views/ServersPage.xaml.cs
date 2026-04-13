using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrustTunnelGui.Models;
using Windows.Storage.Pickers;
using WinRT.Interop;
using TrustTunnelGui.Services;

namespace TrustTunnelGui.Views;

public sealed partial class ServersPage : Page
{
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
            XamlRoot = XamlRoot,
            Title = "Удалить сервер?",
            Content = p.Name,
            PrimaryButtonText = "Удалить",
            CloseButtonText = "Отмена",
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
        catch (Exception ex)
        {
            await new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Ошибка импорта",
                Content = ex.Message,
                CloseButtonText = "OK"
            }.ShowAsync();
        }
    }
}

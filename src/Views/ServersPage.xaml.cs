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
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".toml");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        try
        {
            var p = TomlImport.FromFile(file.Path, Path.GetFileNameWithoutExtension(file.Path));
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

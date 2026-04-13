using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using TrustTunnelGui.Models;
using TrustTunnelGui.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TrustTunnelGui.Views;

public sealed partial class ServerEditPage : Page
{
    public ServerProfile P { get; private set; } = new();

    public ServerEditPage() => InitializeComponent();

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

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                SuggestedFileName = SafeName(P.Name)
            };
            picker.FileTypeChoices.Add("TOML config", new System.Collections.Generic.List<string> { ".toml" });
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file == null)
            {
                StatusBar.Severity = InfoBarSeverity.Informational;
                StatusBar.Title = "Отменено";
                StatusBar.IsOpen = true;
                return;
            }

            await Windows.Storage.FileIO.WriteTextAsync(file, ConfigService.ToToml(P));

            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Title = $"Экспортировано: {file.Path}";
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

using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using TrustTunnelGui.Models;
using TrustTunnelGui.Services;
using System.IO;

namespace TrustTunnelGui.Views;

public sealed partial class ServerEditPage : Page
{
    public ServerProfile P { get; private set; } = new();

    public ServerEditPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
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

    // ── Smooth mouse-wheel scrolling ───────────────────────────────────────
    // WinUI 3's ScrollViewer already does smooth touch/trackpad scrolling via
    // the compositor, but mouse wheel by default jumps in large chunks.
    // We intercept the wheel event and call ChangeView with animation enabled
    // to get the same smooth behaviour for mouse users.
    private void MainScroll_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;

        var point = e.GetCurrentPoint(sv);
        if (point.Properties.IsHorizontalMouseWheel) return; // let horizontal pass through

        // MouseWheelDelta: positive = scroll up, negative = scroll down
        // Typical value per notch: ±120. We use 2× as pixel step so one
        // notch scrolls ~240 px — feels natural for a settings page.
        var delta = point.Properties.MouseWheelDelta * 2.0;
        var newOffset = Math.Clamp(sv.VerticalOffset - delta, 0, sv.ScrollableHeight);
        sv.ChangeView(null, newOffset, null, disableAnimation: false);
        e.Handled = true;
    }

    // ── Toolbar buttons ───────────────────────────────────────────────────
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        App.Profiles.Save();
        StatusBar.Severity = InfoBarSeverity.Success;
        StatusBar.Title    = "Сохранено";
        StatusBar.IsOpen   = true;
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
                StatusBar.Title    = "Отменено";
                StatusBar.IsOpen   = true;
                return;
            }

            File.WriteAllText(path, ConfigService.ToToml(P));
            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Title    = $"Экспортировано: {path}";
            StatusBar.IsOpen   = true;
        }
        catch (Exception ex)
        {
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.Title    = "Ошибка экспорта";
            StatusBar.Message  = ex.Message;
            StatusBar.IsOpen   = true;
        }
    }

    private static string SafeName(string n)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            n = n.Replace(c, '_');
        return string.IsNullOrWhiteSpace(n) ? "profile" : n;
    }
}

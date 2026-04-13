using System;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using TrustTunnelGui.Services;

namespace TrustTunnelGui;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    public static ProfileStore Profiles { get; } = new();
    public static TrustTunnelService Tunnel { get; } = new();
    public static BinaryManager Binaries { get; } = new();

    private TaskbarIcon? _trayIcon;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();

        _trayIcon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("ms-appx:///Assets/app.ico")),
            ToolTipText = "TrustTunnel",
        };

        var menu = new MenuFlyout();
        var showItem = new MenuFlyoutItem { Text = "Показать" };
        showItem.Click += (_, _) => ShowWindow();
        var exitItem = new MenuFlyoutItem { Text = "Выход" };
        exitItem.Click += async (_, _) =>
        {
            await Tunnel.StopAsync();
            _trayIcon?.Dispose();
            Environment.Exit(0);
        };
        menu.Items.Add(showItem);
        menu.Items.Add(exitItem);
        _trayIcon.ContextFlyout = menu;
        _trayIcon.LeftClickCommand = new RelayCommand(ShowWindow);

        _trayIcon.ForceCreate();

        if (AutoStartService.AutoConnect && Profiles.Active != null)
        {
            var path = Profiles.ConfigPathFor(Profiles.Active);
            ConfigService.Save(Profiles.Active, path);
            Tunnel.Start(Binaries.ClientExePath, path);
        }

        if (AutoStartService.StartHidden)
        {
            // окно не активируем
        }
        else
        {
            MainWindow.Activate();
        }
    }

    void ShowWindow() => MainWindow?.Activate();
}

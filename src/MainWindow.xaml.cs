using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrustTunnelGui.Views;
using System;
using System.IO;

namespace TrustTunnelGui;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "TrustTunnel";

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        this.Closed += (_, args) =>
        {
            args.Handled = true;
            AppWindow.Hide();
        };

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);

        ContentFrame.Navigate(typeof(ConnectionPage));
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        var page = item.Tag switch
        {
            "connection" => typeof(ConnectionPage),
            "servers"    => typeof(ServersPage),
            "settings"   => typeof(SettingsPage),
            _            => typeof(ConnectionPage)
        };
        ContentFrame.Navigate(page);
    }
}

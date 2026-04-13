using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrustTunnelGui.Views;

namespace TrustTunnelGui;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "TrustTunnel";

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/app.ico");

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
            "logs"       => typeof(LogsPage),
            "settings"   => typeof(SettingsPage),
            _            => typeof(ConnectionPage)
        };
        ContentFrame.Navigate(page);
    }
}

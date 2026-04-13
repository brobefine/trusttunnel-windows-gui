using Microsoft.UI.Xaml;
using TrustTunnelGui.Services;

namespace TrustTunnelGui;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    public static ProfileStore Profiles { get; } = new();
    public static TrustTunnelService Tunnel { get; } = new();
    public static BinaryManager Binaries { get; } = new();

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}

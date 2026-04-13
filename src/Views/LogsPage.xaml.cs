using System;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrustTunnelGui.Services;

namespace TrustTunnelGui.Views;

public sealed partial class LogsPage : Page
{
    public LogsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => App.Tunnel.LogReceived -= OnLog;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        foreach (var line in App.Tunnel.Buffer) sb.AppendLine(line);
        LogBox.Text = sb.ToString();
        App.Tunnel.LogReceived += OnLog;
    }

    private void OnLog(string line) => Ui.Run(() =>
    {
        LogBox.Text += line + "\n";
        if (AutoScroll.IsOn)
            LogScroll.ChangeView(null, LogScroll.ScrollableHeight, null, true);
    });

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        LogBox.Text = "";
        while (App.Tunnel.Buffer.TryDequeue(out _)) { }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            var path = Win32FileDialog.ShowSave(
                hwnd,
                "Log file\0*.log\0Text file\0*.txt\0All files\0*.*\0\0",
                "log",
                $"trusttunnel-{DateTime.Now:yyyyMMdd-HHmmss}.log");

            if (string.IsNullOrEmpty(path)) return;
            System.IO.File.WriteAllText(path, LogBox.Text);
        }
        catch { /* можно вывести InfoBar если есть */ }
    }
}

using System;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrustTunnelGui.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

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

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = $"trusttunnel-{DateTime.Now:yyyyMMdd-HHmmss}.log"
        };
        picker.FileTypeChoices.Add("Log", new System.Collections.Generic.List<string> { ".log", ".txt" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        var file = await picker.PickSaveFileAsync();
        if (file != null)
            await Windows.Storage.FileIO.WriteTextAsync(file, LogBox.Text);
    }
}

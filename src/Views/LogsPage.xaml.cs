using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TrustTunnelGui.Services;

namespace TrustTunnelGui.Views;

public sealed partial class LogsPage : Page
{
    private const int MaxLines = 2000;
    private readonly ObservableCollection<string> _lines = new();
    private ScrollViewer? _scroll;

    public LogsPage()
    {
        InitializeComponent();
        LogList.ItemsSource = _lines;
        Loaded += OnLoaded;
        Unloaded += (_, _) => App.Tunnel.LogReceived -= OnLog;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _lines.Clear();
        foreach (var line in App.Tunnel.Buffer) _lines.Add(line);
        App.Tunnel.LogReceived += OnLog;

        _scroll = FindChild<ScrollViewer>(LogList);
        ScrollToEnd();
    }

    private void OnLog(string line) => Ui.Run(() =>
    {
        _lines.Add(line);
        while (_lines.Count > MaxLines) _lines.RemoveAt(0);

        if (AutoScroll.IsOn) ScrollToEnd();
    });

    private void ScrollToEnd()
    {
        if (_lines.Count == 0) return;
        LogList.DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () =>
            {
                _scroll ??= FindChild<ScrollViewer>(LogList);
                if (_scroll != null)
                    _scroll.ChangeView(null, _scroll.ScrollableHeight, null, disableAnimation: true);
                else
                    LogList.ScrollIntoView(_lines[^1]);
            });
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _lines.Clear();
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

            var sb = new StringBuilder();
            foreach (var l in _lines) sb.AppendLine(l);
            System.IO.File.WriteAllText(path, sb.ToString());
        }
        catch { /* можно вывести InfoBar если есть */ }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int n = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var deeper = FindChild<T>(child);
            if (deeper != null) return deeper;
        }
        return null;
    }
}
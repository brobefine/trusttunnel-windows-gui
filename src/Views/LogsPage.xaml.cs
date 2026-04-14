using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrustTunnelGui.Services;

namespace TrustTunnelGui.Views;

public sealed partial class LogsPage : Page
{
    private const int MaxLines = 2000;

    // We keep lines in a list so we can enforce the cap without re-parsing the TextBox string.
    private readonly List<string> _lines = new(MaxLines + 10);

    public LogsPage()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += (_, _) => App.Tunnel.LogReceived -= OnLog;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _lines.Clear();
        foreach (var line in App.Tunnel.Buffer)
            _lines.Add(line);

        RebuildText();
        App.Tunnel.LogReceived += OnLog;

        if (AutoScroll.IsOn)
            ScrollToEnd();
    }

    private void OnLog(string line) => Ui.Run(() =>
    {
        _lines.Add(line);
        if (_lines.Count > MaxLines)
            _lines.RemoveAt(0);

        // Append directly instead of rebuilding the whole string on every line —
        // this is O(line) instead of O(total) and keeps the UI responsive.
        LogBox.Text += line + "\n";

        // Mirror the cap: if we trimmed a line from the front, trim from the TextBox too.
        if (_lines.Count == MaxLines)
        {
            var idx = LogBox.Text.IndexOf('\n');
            if (idx >= 0) LogBox.Text = LogBox.Text[(idx + 1)..];
        }

        if (AutoScroll.IsOn)
            ScrollToEnd();
    });

    private void RebuildText()
    {
        var sb = new StringBuilder();
        foreach (var l in _lines) sb.Append(l).Append('\n');
        LogBox.Text = sb.ToString();
    }

    private void ScrollToEnd()
    {
        // Move caret to end — TextBox scrolls to follow it.
        var len = LogBox.Text.Length;
        if (len > 0)
        {
            LogBox.SelectionStart  = len;
            LogBox.SelectionLength = 0;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _lines.Clear();
        LogBox.Text = string.Empty;
        App.Tunnel.Buffer.Clear();
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
        catch { }
    }
}

using System;
using Microsoft.UI.Dispatching;

namespace TrustTunnelGui.Services;

public static class Ui
{
    public static void Run(Action a)
    {
        var dq = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dq == null) { a(); return; }
        if (dq.HasThreadAccess) a();
        else dq.TryEnqueue(() => a());
    }
}

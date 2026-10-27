using System;
using System.Threading;
using System.Windows;

namespace SSSPen;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        using var mutex = new Mutex(true, "SSSPen_SingleInstance", out bool created);
        if (!created)
            return;   // уже запущена

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        new AppController().Start();
        app.Run();
    }
}

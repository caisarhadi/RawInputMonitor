using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RawInputMonitor.Core;
using RawInputMonitor.Server;
using RawInputMonitor.Win32;

namespace RawInputMonitor;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("RawInputMonitor starting...");

        var deviceManager = new DeviceManager();
        var webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dashboard");
        var devPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Dashboard"));
        if (Directory.Exists(devPath))
        {
            webRoot = devPath;
        }
        else if (!Directory.Exists(webRoot))
        {
            Directory.CreateDirectory(webRoot);
        }

        var webSocketServer = new WebSocketServer(deviceManager, webRoot);
        var cts = new CancellationTokenSource();

        var wsTask = webSocketServer.StartAsync(cts.Token);

        using (var messageWindow = new MessageWindow(deviceManager))
        {
            Console.WriteLine("Message pump started. Press Ctrl+C to exit.");
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            messageWindow.Run();
        }

        await wsTask;
        Console.WriteLine("Shutdown complete.");
    }
}

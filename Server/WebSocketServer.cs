using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RawInputMonitor.Core;

namespace RawInputMonitor.Server;

public class WebSocketServer
{
    private readonly HttpListener _listener;
    private readonly DeviceManager _deviceManager;
    private readonly List<WebSocket> _clients = new();
    private readonly string _webRoot;

    public WebSocketServer(DeviceManager deviceManager, string webRoot)
    {
        _deviceManager = deviceManager;
        _webRoot = webRoot;
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:9100/");
    }

    public async Task StartAsync(CancellationToken token)
    {
        _listener.Start();
        Console.WriteLine("[WebSocketServer] Listening on http://localhost:9100/");

        var broadcastTask = BroadcastLoopAsync(token);

        try
        {
            while (!token.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    _ = HandleWebSocketAsync(context);
                }
                else
                {
                    ServeStaticFile(context);
                }
            }
        }
        catch (HttpListenerException)
        {
            // Normal shutdown
        }

        await broadcastTask;
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context)
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var socket = wsContext.WebSocket;

        lock (_clients)
        {
            _clients.Add(socket);
        }
        Console.WriteLine("[WebSocketServer] Client connected.");

        // Send initial device list
        await SendDeviceListAsync(socket);

        var buffer = new byte[1024];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebSocketServer] Client error: {ex.Message}");
        }
        finally
        {
            lock (_clients)
            {
                _clients.Remove(socket);
            }
            if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            socket.Dispose();
            Console.WriteLine("[WebSocketServer] Client disconnected.");
        }
    }

    private void ServeStaticFile(HttpListenerContext context)
    {
        var requestPath = context.Request.Url?.AbsolutePath ?? "/";
        if (requestPath == "/") requestPath = "/index.html";

        var filePath = Path.Combine(_webRoot, requestPath.TrimStart('/'));

        if (File.Exists(filePath))
        {
            try
            {
                var content = File.ReadAllBytes(filePath);
                
                if (filePath.EndsWith(".html")) context.Response.ContentType = "text/html";
                else if (filePath.EndsWith(".css")) context.Response.ContentType = "text/css";
                else if (filePath.EndsWith(".js")) context.Response.ContentType = "application/javascript";
                
                context.Response.ContentLength64 = content.Length;
                context.Response.OutputStream.Write(content, 0, content.Length);
                context.Response.StatusCode = 200;
            }
            catch
            {
                context.Response.StatusCode = 500;
            }
        }
        else
        {
            context.Response.StatusCode = 404;
        }

        context.Response.OutputStream.Close();
    }

    private async Task BroadcastLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_deviceManager.TryDequeueEvent(out var evt))
            {
                var msg = new
                {
                    type = "input",
                    device = evt.DeviceName,
                    deviceId = evt.DeviceId,
                    sourceType = evt.SourceType,
                    channel = evt.Channel,
                    value = evt.Value,
                    raw = evt.RawValue,
                    ts = evt.Timestamp
                };

                var json = JsonSerializer.Serialize(msg, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var bytes = Encoding.UTF8.GetBytes(json);

                List<WebSocket> currentClients;
                lock (_clients)
                {
                    currentClients = new List<WebSocket>(_clients);
                }

                foreach (var client in currentClients)
                {
                    if (client.State == WebSocketState.Open)
                    {
                        try
                        {
                            await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        catch
                        {
                            // Ignore, will be cleaned up in the receive loop
                        }
                    }
                }
            }
            else
            {
                await Task.Delay(1, token); // Small delay when idle
            }
        }
    }

    private async Task SendDeviceListAsync(WebSocket socket)
    {
        var devices = _deviceManager.GetConnectedDevices();
        var list = new List<object>();
        foreach (var d in devices)
        {
            list.Add(new
            {
                deviceId = $"{d.VendorId:X4}:{d.ProductId:X4}",
                name = d.ProductName,
                sourceType = d.SourceType,
                connected = d.IsConnected
            });
        }

        var msg = new
        {
            type = "deviceList",
            devices = list
        };

        var json = JsonSerializer.Serialize(msg, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var bytes = Encoding.UTF8.GetBytes(json);
        
        if (socket.State == WebSocketState.Open)
        {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}

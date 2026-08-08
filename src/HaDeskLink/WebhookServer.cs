
// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
#nullable enable
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// HTTP listener for receiving commands and notifications from Home Assistant.
/// Commands: http://PC-IP:59123/command?token=xxx&amp;action=shutdown
/// Notifications come via the mobile_app webhook protocol.
/// </summary>
public class WebhookServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _token;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private NotifyIcon? _trayIcon;

    public int Port { get; } = 59123;

    public WebhookServer(string token, int port = 59123, string bindAddress = "+")
    {
        _token = token;
        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://{bindAddress}:{port}/command/");
        _listener.Prefixes.Add($"http://{bindAddress}:{port}/webhook/");
    }

    public void SetTrayIcon(NotifyIcon? trayIcon) => _trayIcon = trayIcon;

    public void Start()
    {
        _listener.Start();
        ThreadPool.QueueUserWorkItem(_ => Listen());
    }

    private void Listen()
    {
        while (true)
        {
            try
            {
                if (_cts.IsCancellationRequested) break;
                var context = _listener.GetContext();
                ProcessRequest(context);
            }
            catch (HttpListenerException) when (_cts.IsCancellationRequested) { break; }
            catch (ObjectDisposedException) { break; }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                try { File.AppendAllText(Program.LogFile(), $"[WebhookServer] Listener error (retrying): {ex}\n"); }
                catch { }
            }
        }
    }

    private bool ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(token))
            return false;
        var expectedBytes = Encoding.UTF8.GetBytes(_token);
        var actualBytes = Encoding.UTF8.GetBytes(token);
        if (expectedBytes.Length != actualBytes.Length)
            return false;
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "";

        // Extract token from Authorization header (preferred) or query string (legacy)
        var authHeader = context.Request.Headers["Authorization"];
        var token = "";
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader.Substring("Bearer ".Length).Trim();
        }
        else
        {
            // Legacy: token in query string (less secure — logs may capture it)
            token = context.Request.QueryString["token"] ?? "";
        }

        if (path.Contains("/webhook"))
        {
            // HA mobile_app notification webhook — requires token auth
            if (!ValidateToken(token))
            {
                Console.WriteLine("[WebhookServer] Unauthorized webhook attempt");
                context.Response.StatusCode = 401;
                context.Response.Close();
                return;
            }

            try
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var body = reader.ReadToEnd();

                if (NotificationHandler.TryHandleNotification(body, _trayIcon))
                {
                    RespondJson(context, new { success = true });
                }
                else
                {
                    RespondJson(context, new { success = true, note = "unknown webhook type" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebhookServer] Webhook error: {ex.Message}");
                RespondJson(context, new { success = false, error = ex.Message }, 400);
            }
            return;
        }

        // Command endpoint: /command?action=shutdown (token via Authorization header)
        var action = context.Request.QueryString["action"] ?? "";

        if (!ValidateToken(token))
        {
            Console.WriteLine("[WebhookServer] Unauthorized command attempt");
            context.Response.StatusCode = 401;
            context.Response.Close();
            return;
        }

        try
        {
            CommandHandler.Execute(action);
            RespondJson(context, new { success = true, action });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WebhookServer] Command error: {ex.Message}");
            RespondJson(context, new { success = false, error = ex.Message }, 400);
        }
    }

    private static void RespondJson(HttpListenerContext context, object data, int statusCode = 200)
    {
        var json = JsonSerializer.Serialize(data);
        var buffer = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.Close();
    }

    public void Stop() => _cts.Cancel();

    public void Dispose()
    {
        if (!_disposed)
        {
            _cts.Cancel();
            _listener.Stop();
            _cts.Dispose();
            _disposed = true;
        }
    }
}
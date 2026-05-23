// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Handles notifications from Home Assistant with modern dark-themed toasts.
/// Rounded corners via GraphicsPath (no P/Invoke), hover-pause auto-close, slide-in animation.
/// </summary>
public static class NotificationHandler
{
    private static readonly Color BgColor = Color.FromArgb(22, 33, 62);
    private static readonly Color AccentBlue = Color.FromArgb(66, 133, 244);
    private static readonly Color TextWhite = Color.FromArgb(230, 230, 240);
    private static readonly Color TextGray = Color.FromArgb(160, 160, 180);
    private static readonly Color BtnBg = Color.FromArgb(15, 52, 96);
    private static readonly Color BtnHover = Color.FromArgb(25, 72, 136);

    public static bool TryHandleNotification(string jsonBody, NotifyIcon? trayIcon)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(jsonBody);
            var root = doc.RootElement;

            string title = "HA DeskLink";
            string message = "";
            string? command = null;
            List<NotificationAction>? actions = null;
            string? commandOnAction = null;

            if (root.TryGetProperty("title", out var t1)) title = t1.GetString() ?? title;
            if (root.TryGetProperty("message", out var m1)) message = m1.GetString() ?? "";
            if (root.TryGetProperty("command", out var c1)) command = c1.GetString();

            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("title", out var t2)) title = t2.GetString() ?? title;
                if (data.TryGetProperty("message", out var m2)) message = m2.GetString() ?? message;
                if (data.TryGetProperty("command", out var c2)) command = c2.GetString();
                if (data.TryGetProperty("command_on_action", out var coa)) commandOnAction = coa.GetString();
                if (data.TryGetProperty("actions", out var actionsArr))
                {
                    actions = new List<NotificationAction>();
                    foreach (var a in actionsArr.EnumerateArray())
                    {
                        var act = a.GetProperty("action").GetString() ?? "";
                        var actTitle = a.TryGetProperty("title", out var at) ? at.GetString() ?? act : act;
                        var actCommand = a.TryGetProperty("command", out var ac) ? ac.GetString() : null;
                        actions.Add(new NotificationAction(act, actTitle, actCommand));
                    }
                }
            }

            if (!string.IsNullOrEmpty(command))
            {
                try { CommandHandler.Execute(command!); } catch { }
            }

            if (!string.IsNullOrEmpty(message))
            {
                var toast = new NotificationToast(title, message, actions, commandOnAction);
                toast.Show();
                return true;
            }

            if (!string.IsNullOrEmpty(command)) return true;
        }
        catch { }
        return false;
    }

    public static void ShowNotification(string title, string message, NotifyIcon? trayIcon = null)
    {
        var toast = new NotificationToast(title, message);
        toast.Show();
    }

    public static void ShowActionableNotification(string title, string message,
        List<NotificationAction> actions, string? commandOnAction = null, NotifyIcon? trayIcon = null)
    {
        var toast = new NotificationToast(title, message, actions, commandOnAction);
        toast.Show();
    }

    /// <summary>
    /// Show a connection status toast (used for WebSocket events).
    /// </summary>
    public static void ShowConnectionToast(string title, string message)
    {
        var toast = new NotificationToast(title, message, accentOverride: Color.FromArgb(46, 204, 113));
        toast.Show();
    }
}

/// <summary>
/// Modern dark-themed toast notification popup.
/// Rounded corners via GraphicsPath, hover-pause auto-close, accent-colored left bar.
/// </summary>
public class NotificationToast : Form
{
    private readonly Timer _autoCloseTimer;
    private readonly List<NotificationAction>? _actions;
    private readonly string? _commandOnAction;

    public NotificationToast(string title, string message,
        List<NotificationAction>? actions = null, string? commandOnAction = null,
        Color? accentOverride = null)
    {
        _actions = actions;
        _commandOnAction = commandOnAction;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        Size = new Size(400, CalculateHeight(message, actions));
        BackColor = Color.FromArgb(22, 33, 62);

        // Region for rounded corners (no P/Invoke needed — .NET can do this)
        Region = CreateRoundedRegion(0, 0, Width, Height, 16);

        BuildContent(title, message, actions, accentOverride ?? Color.FromArgb(66, 133, 244));

        _autoCloseTimer = new Timer { Interval = 8000 };
        _autoCloseTimer.Tick += (s, e) => { _autoCloseTimer.Stop(); Close(); };
        _autoCloseTimer.Start();

        Load += (s, e) => PositionTopRight();
    }

    private int CalculateHeight(string message, List<NotificationAction>? actions)
    {
        var lines = Math.Max(1, message.Length / 45 + 1);
        var h = 60 + lines * 20;
        if (actions != null && actions.Count > 0) h += 50;
        return Math.Max(100, Math.Min(h, 300));
    }

    private void BuildContent(string title, string message, List<NotificationAction>? actions, Color accentColor)
    {
        // Left accent bar
        var accentBar = new Panel { BackColor = accentColor, Size = new Size(4, Height), Dock = DockStyle.Left };

        // Title
        var titleLabel = new Label
        {
            Text = title, Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White, AutoSize = true,
            Location = new Point(16, 12)
        };

        // Close button ✕
        var closeBtn = new Label
        {
            Text = "✕", Font = new Font("Segoe UI", 11f),
            ForeColor = Color.FromArgb(160, 160, 180),
            Location = new Point(Width - 30, 8), AutoSize = true, Cursor = Cursors.Hand
        };
        closeBtn.Click += (s, e) => Close();

        // Message
        var msgLabel = new Label
        {
            Text = message, Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(200, 200, 215),
            Location = new Point(16, 40), MaximumSize = new Size(360, 0), AutoSize = true
        };

        // Timestamp
        var timeLabel = new Label
        {
            Text = DateTime.Now.ToString("HH:mm"), Font = new Font("Segoe UI", 8f),
            ForeColor = Color.FromArgb(140, 140, 160),
            Location = new Point(Width - 55, Height - 22), AutoSize = true
        };

        Controls.AddRange(new Control[] { accentBar, titleLabel, closeBtn, msgLabel, timeLabel });

        // Action buttons
        if (actions != null && actions.Count > 0)
        {
            var btnX = 16;
            var btnY = msgLabel.Bottom + 10;
            foreach (var action in actions)
            {
                var btn = new Button
                {
                    Text = action.Title, Font = new Font("Segoe UI", 9f),
                    BackColor = Color.FromArgb(15, 52, 96), ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat, Size = new Size(120, 32),
                    Location = new Point(btnX, btnY), Cursor = Cursors.Hand, Tag = action
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 72, 136);
                btn.Click += ActionButtonClick;
                Controls.Add(btn);
                btnX += btn.Width + 8;
            }
        }
    }

    private void ActionButtonClick(object? sender, EventArgs e)
    {
        var btn = sender as Button;
        if (btn?.Tag is NotificationAction a)
        {
            if (!string.IsNullOrEmpty(a.Command))
            {
                try { CommandHandler.Execute(a.Command!); } catch { }
            }
            else if (!string.IsNullOrEmpty(_commandOnAction))
            {
                try { CommandHandler.Execute(_commandOnAction); } catch { }
            }
        }
        Close();
    }

    private void PositionTopRight()
    {
        var screen = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
        Location = new Point(screen.Right - Width - 20, screen.Top + 20);
    }

    protected override void OnMouseEnter(EventArgs e) { _autoCloseTimer.Stop(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _autoCloseTimer.Start(); base.OnMouseLeave(e); }

    /// <summary>
    /// Create a rounded rectangle region using GraphicsPath (no P/Invoke).
    /// </summary>
    private static Region CreateRoundedRegion(int x, int y, int width, int height, int radius)
    {
        var path = new GraphicsPath();
        path.AddArc(x, y, radius, radius, 180, 90);
        path.AddArc(x + width - radius, y, radius, radius, 270, 90);
        path.AddArc(x + width - radius, y + height - radius, radius, radius, 0, 90);
        path.AddArc(x, y + height - radius, radius, radius, 90, 90);
        path.CloseFigure();
        return new Region(path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _autoCloseTimer?.Stop();
            _autoCloseTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}

public class NotificationAction
{
    public string ActionKey { get; }
    public string Title { get; }
    public string? Command { get; }

    public NotificationAction(string actionKey, string title, string? command = null)
    {
        ActionKey = actionKey;
        Title = title;
        Command = command;
    }
}
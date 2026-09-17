// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
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

    /// <summary>UI thread SynchronizationContext captured at startup. Thread-safe static.</summary>
    internal static SynchronizationContext? UiContext { get; set; }

    public static bool TryHandleNotification(string jsonBody, NotifyIcon? trayIcon)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(jsonBody);
            var root = doc.RootElement;

            string title = "HA DeskLink";
            string message = "";
            string? command = null;
            string? commandOnAction = null;
            string? imageUrl = null;
            List<NotificationAction>? actions = null;

            if (root.TryGetProperty("title", out var t1)) title = t1.GetString() ?? title;
            if (root.TryGetProperty("message", out var m1)) message = m1.GetString() ?? "";
            if (root.TryGetProperty("command", out var c1)) command = c1.GetString();

            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("title", out var t2)) title = t2.GetString() ?? title;
                if (data.TryGetProperty("message", out var m2)) message = m2.GetString() ?? message;
                if (data.TryGetProperty("command", out var c2)) command = c2.GetString();
                if (data.TryGetProperty("command_on_action", out var coa)) commandOnAction = coa.GetString();
                // Companion-style image: data.image, or data.attachment.url override
                if (data.TryGetProperty("image", out var img)) imageUrl = img.GetString();
                if (data.TryGetProperty("attachment", out var att) &&
                    att.TryGetProperty("url", out var attUrl))
                    imageUrl = attUrl.GetString() ?? imageUrl;
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
                try { CommandHandler.Execute(command!); } catch (Exception ex) { Console.WriteLine($"[Notification] Command error: {ex.Message}"); }
            }

            if (!string.IsNullOrEmpty(message))
            {
                var image = TryLoadImage(imageUrl);
                if (actions != null && actions.Count > 0)
                    ShowActionableNotification(title, message, actions, commandOnAction, trayIcon, image);
                else
                    ShowNotification(title, message, trayIcon, image);
                return true;
            }

            if (!string.IsNullOrEmpty(command)) return true;
        }
        catch (Exception ex) { Console.WriteLine($"[Notification] Parse error: {ex.Message}"); }
        return false;
    }

    /// <summary>
    /// Resolves an HA companion-style image reference to a local file.
    /// Returns null (with log line) when no image or on failure - the toast
    /// then shows a hint instead of silently dropping the feature.
    /// </summary>
    internal static NotificationImageLoader.ImageResult? TryLoadImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        try
        {
            var config = Config.Load();
            // Token: encrypted config preferred (DPAPI), falls back to env vars
            var token = string.IsNullOrEmpty(config.HaToken)
                     ? Environment.GetEnvironmentVariable("HA_TOKEN")
                       ?? Environment.GetEnvironmentVariable("HASS_TOKEN")
                       ?? string.Empty
                     : config.HaToken;
            var result = NotificationImageLoader.Load(imageUrl, config.HaUrl, token);
            if (result.LocalPath != null)
                return result;
            Console.WriteLine($"[Notification] Image load failed: {result.Error}");
        }
        catch (Exception ex) { Console.WriteLine($"[Notification] Image error: {ex.Message}"); }
        return null;
    }

    public static void ShowNotification(string title, string message, NotifyIcon? trayIcon = null,
        NotificationImageLoader.ImageResult? image = null)
    {
        ShowToastOnUiThread(() =>
        {
            var toast = new NotificationToast(title, message, image: image);
            toast.FormClosed += (s, e) => toast.Dispose();
            toast.Show();
        });
    }

    public static void ShowActionableNotification(string title, string message,
        List<NotificationAction> actions, string? commandOnAction = null, NotifyIcon? trayIcon = null,
        NotificationImageLoader.ImageResult? image = null)
    {
        ShowToastOnUiThread(() =>
        {
            var toast = new NotificationToast(title, message, actions, commandOnAction, image: image);
            toast.FormClosed += (s, e) => toast.Dispose();
            toast.Show();
        });
    }

    /// <summary>
    /// Show a connection status toast (used for WebSocket events).
    /// </summary>
    public static void ShowConnectionToast(string title, string message)
    {
        ShowToastOnUiThread(() =>
        {
            var toast = new NotificationToast(title, message, accentOverride: Color.FromArgb(46, 204, 113));
            toast.FormClosed += (s, e) => toast.Dispose();
            toast.Show();
        });
    }

    /// <summary>Marshals toast creation to the UI thread to prevent cross-thread exceptions.</summary>
    private static void ShowToastOnUiThread(Action createAndShow)
    {
        var ctx = UiContext;
        if (ctx != null && SynchronizationContext.Current != ctx)
        {
            ctx.Post(_ => createAndShow(), null);
        }
        else
        {
            createAndShow();
        }
    }
}

/// <summary>
/// Modern dark-themed toast notification popup.
/// Rounded corners via GraphicsPath, hover-pause auto-close, accent-colored left bar.
/// </summary>
public class NotificationToast : Form
{
    private readonly System.Windows.Forms.Timer _autoCloseTimer;
    private readonly List<NotificationAction>? _actions;
    private readonly string? _commandOnAction;
    private readonly NotificationImageLoader.ImageResult? _image;

    public NotificationToast(string title, string message,
        List<NotificationAction>? actions = null, string? commandOnAction = null,
        Color? accentOverride = null, NotificationImageLoader.ImageResult? image = null)
    {
        _actions = actions;
        _commandOnAction = commandOnAction;
        _image = image;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        Size = new Size(400, CalculateHeight(message, actions, image));
        BackColor = Color.FromArgb(22, 33, 62);

        // Region for rounded corners (no P/Invoke needed — .NET can do this)
        Region = CreateRoundedRegion(0, 0, Width, Height, 16);

        BuildContent(title, message, actions, accentOverride ?? Color.FromArgb(66, 133, 244));

        _autoCloseTimer = new System.Windows.Forms.Timer { Interval = image != null ? 12000 : 8000 };
        _autoCloseTimer.Tick += (s, e) => { _autoCloseTimer.Stop(); Close(); };
        _autoCloseTimer.Start();

        Load += (s, e) => PositionNotification();
    }

    private int CalculateHeight(string message, List<NotificationAction>? actions,
        NotificationImageLoader.ImageResult? image)
    {
        var lines = Math.Max(1, message.Length / 45 + 1);
        var h = 60 + lines * 20;
        if (actions != null && actions.Count > 0) h += 50;
        if (image != null && image.LocalPath != null) h += 200; // image preview block
        return Math.Max(100, Math.Min(h, 500));
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

        // Image preview (companion-style camera snapshot etc.)
        if (_image != null && _image.LocalPath != null && File.Exists(_image.LocalPath))
        {
            try
            {
                var pic = new PictureBox
                {
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Location = new Point(16, msgLabel.Bottom + 8),
                    MaximumSize = new Size(368, 190),
                    BackColor = Color.FromArgb(12, 20, 38),
                    Cursor = Cursors.Hand,
                    Tag = _image.LocalPath
                };
                using (var stream = new FileStream(_image.LocalPath, FileMode.Open, FileAccess.Read))
                    pic.Image = Image.FromStream(stream);
                // Click opens full-size viewer
                pic.Click += (s, e) =>
                {
                    try
                    {
                        var path = (string)((PictureBox)s!).Tag!;
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex) { Console.WriteLine($"[Notification] Image open failed: {ex.Message}"); }
                };
                Controls.Add(pic);
            }
            catch (Exception ex)
            {
                // No silent fallback: load error gets a visible hint in the toast
                var hint = new Label
                {
                    Text = "[Bild konnte nicht geladen werden: " + (_image.Error ?? "unbekannt") + "]",
                    Font = new Font("Segoe UI", 8f, FontStyle.Italic),
                    ForeColor = Color.FromArgb(220, 130, 100),
                    Location = new Point(16, msgLabel.Bottom + 8),
                    MaximumSize = new Size(360, 0), AutoSize = true
                };
                Controls.Add(hint);
                Console.WriteLine($"[Notification] Image render failed: {ex.Message}");
            }
        }
        else if (_image != null && _image.LocalPath == null)
        {
            // Download failed: visible hint, not a silent drop (Sir-Regel: kein stiller Fallback)
            var hint = new Label
            {
                Text = "[Bild konnte nicht geladen werden" + (_image.Error != null ? ": " + _image.Error : "") + "]",
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(220, 130, 100),
                Location = new Point(16, msgLabel.Bottom + 8),
                MaximumSize = new Size(360, 0), AutoSize = true
            };
            Controls.Add(hint);
        }

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
                try { CommandHandler.Execute(a.Command!); } catch (Exception ex) { Console.WriteLine($"[Notification] Action command error: {ex.Message}"); }
            }
            else if (!string.IsNullOrEmpty(_commandOnAction))
            {
                try { CommandHandler.Execute(_commandOnAction); } catch (Exception ex) { Console.WriteLine($"[Notification] Fallback command error: {ex.Message}"); }
            }
        }
        Close();
    }

    private void PositionNotification()
    {
        try
        {
            var config = Config.Load();
            var monitorIndex = config.NotificationMonitor;
            var position = config.NotificationPosition ?? "bottom_left";

            // Monitor-Auswahl: 0 = Primary, 1+ = spezifischer Monitor
            Screen? screen = null;
            if (monitorIndex >= 0 && monitorIndex < Screen.AllScreens.Length)
                screen = Screen.AllScreens[monitorIndex];
            else
                screen = Screen.PrimaryScreen;

            var area = screen?.WorkingArea ?? SystemInformation.WorkingArea;
            var margin = 20;

            Location = position.ToLowerInvariant() switch
            {
                "bottom_right" => new Point(area.Right - Width - margin, area.Bottom - Height - margin),
                "top_left" => new Point(area.Left + margin, area.Top + margin),
                "top_right" => new Point(area.Right - Width - margin, area.Top + margin),
                _ => new Point(area.Left + margin, area.Bottom - Height - margin), // bottom_left (default)
            };
        }
        catch
        {
            // Fallback: unten links auf Primary Screen
            var screen = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
            Location = new Point(screen.Left + 20, screen.Bottom - Height - 20);
        }
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
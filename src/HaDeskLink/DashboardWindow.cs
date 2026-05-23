// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace HaDeskLink;

/// <summary>
/// Embedded HA Dashboard using WebView2.
/// Opens the HA login page — user logs in once with username/password,
/// then WebView2 remembers the session (just like a regular browser).
/// Falls back to the default browser if WebView2 is not available.
/// </summary>
public class DashboardWindow : Form
{
    private WebView2? _webView;
    private readonly string _haUrl;
    private Label? _errorLabel;
    private Panel? _loadingPanel;
    private static bool _installPrompted = false;
    private static DashboardWindow? _instance;

    public DashboardWindow(string haUrl)
    {
        _haUrl = haUrl.TrimEnd('/');

        Text = "HA DeskLink - Dashboard";
        Size = new Size(1300, 850);
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(26, 26, 46);

        BuildContent();
    }

    private void BuildContent()
    {
        _loadingPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(26, 26, 46)
        };

        var loadTitle = new Label
        {
            Text = "🏠 Dashboard wird geladen…",
            Font = new Font("Segoe UI", 18f),
            ForeColor = Color.White,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top
        };

        var loadSub = new Label
        {
            Text = "Verbinde mit Home Assistant…",
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.Gray,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Bottom
        };

        _loadingPanel.Controls.Add(loadTitle);
        _loadingPanel.Controls.Add(loadSub);

        _errorLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 12f),
            ForeColor = Color.OrangeRed,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        Controls.Add(_loadingPanel);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _webView = new WebView2 { Dock = DockStyle.Fill };

        try
        {
            // Use persistent user data folder so login session survives restarts
            var userDataDir = Path.Combine(Config.GetConfigDir(), "WebView2Data");
            Directory.CreateDirectory(userDataDir);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // Navigate directly to HA — user logs in once, session persists
            _webView.CoreWebView2.Navigate(_haUrl);

            // Replace loading panel with WebView
            Controls.Clear();
            Controls.Add(_webView);
        }
        catch (Exception ex)
        {
            // WebView2 not installed — offer install or fallback to browser
            if (!_installPrompted && ex.Message.Contains("WebView2"))
            {
                _installPrompted = true;
                Close();

                var result = MessageBox.Show(
                    "WebView2 Runtime wird für das eingebettete Dashboard benötigt.\n\n" +
                    "Jetzt herunterladen und installieren?\n(Nach Installation App neu starten)",
                    "WebView2 fehlt", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        var tmpPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
                        using var http = new System.Net.Http.HttpClient();
                        var bytes = await http.GetByteArrayAsync("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                        File.WriteAllBytes(tmpPath, bytes);
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tmpPath) { UseShellExecute = true });
                    }
                    catch (Exception dex)
                    {
                        MessageBox.Show($"Download fehlgeschlagen: {dex.Message}\n\n" +
                            "Bitte manuell installieren:\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/",
                            "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    // Fallback: open in default browser
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_haUrl) { UseShellExecute = true });
                }
            }
            else
            {
                ShowError($"Fehler beim Laden: {ex.Message}");
            }
        }
    }

    private void ShowError(string message)
    {
        if (_loadingPanel != null) _loadingPanel.Visible = false;
        if (_errorLabel != null)
        {
            _errorLabel.Text = message;
            _errorLabel.Visible = true;
            if (!Controls.Contains(_errorLabel)) Controls.Add(_errorLabel);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _webView?.Dispose();
        base.OnFormClosing(e);
    }

    /// <summary>
    /// Opens the dashboard window. If already open, activates it.
    /// No token needed — user logs in once via normal HA login page.
    /// </summary>
    public static void Open(string haUrl)
    {
        if (_instance != null && !_instance.IsDisposed)
        {
            _instance.Activate();
            return;
        }
        _instance = new DashboardWindow(haUrl);
        _instance.Show();
    }
}
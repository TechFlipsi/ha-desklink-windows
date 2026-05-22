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
/// Embedded HA Dashboard using WebView2 with external_auth API.
/// Auto-logs in using the Long-Lived Access Token from config.
/// Includes rate-limiting and IP-ban prevention via AuthGuard.
/// </summary>
public class DashboardWindow : Form
{
    private WebView2? _webView;
    private readonly string _haUrl;
    private readonly string _token;
    private readonly AuthGuard _authGuard;
    private Label? _errorLabel;
    private Panel? _loadingPanel;
    private static bool _installPrompted = false;
    private static DashboardWindow? _instance;

    public DashboardWindow(string haUrl, string token)
    {
        _haUrl = haUrl.TrimEnd('/');
        _token = token;
        _authGuard = new AuthGuard();

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

        if (_authGuard.IsBlocked)
        {
            ShowError(_authGuard.BlockMessage);
            return;
        }

        _webView = new WebView2 { Dock = DockStyle.Fill };

        try
        {
            var userDataDir = Path.Combine(Config.GetConfigDir(), "WebView2Data");
            Directory.CreateDirectory(userDataDir);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // Navigate with external_auth parameter
            _webView.CoreWebView2.Navigate($"{_haUrl}?external_auth=1");

            // Inject externalAuth after page loads
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            // Replace loading panel with WebView
            Controls.Clear();
            Controls.Add(_webView);
        }
        catch (Exception ex)
        {
            _authGuard.RecordFailure(ex.Message);

            if (_authGuard.IsBlocked)
            {
                ShowError(_authGuard.BlockMessage);
                return;
            }

            // WebView2 not installed — offer install
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
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_haUrl) { UseShellExecute = true });
                }
            }
            else
            {
                ShowError($"Fehler beim Laden: {ex.Message}");
            }
        }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_webView?.CoreWebView2 == null || _authGuard.IsBlocked) return;

        if (e.IsSuccess)
        {
            try
            {
                var js = BuildExternalAuthScript();
                await _webView.CoreWebView2.ExecuteScriptAsync(js);
                _authGuard.RecordSuccess();
            }
            catch (Exception ex)
            {
                _authGuard.RecordFailure($"Auth inject failed: {ex.Message}");
            }
        }
        else
        {
            _authGuard.RecordFailure($"Navigation failed: HTTP {e.HttpStatusCode}");
        }
    }

    private string BuildExternalAuthScript()
    {
        var t = _token.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "").Replace("\r", "");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("(function() {");
        sb.AppendLine("  if (window._externalAuthInjected) return;");
        sb.AppendLine("  window._externalAuthInjected = true;");
        sb.AppendLine("  window.externalApp = {");
        sb.AppendLine("    getExternalAuth: function(cb, force) {");
        sb.AppendLine("      try { cb({ access_token: '" + t + "', expires_in: 900, refresh_token: '" + t + "', token_type: 'Bearer' }); }");
        sb.AppendLine("      catch(e) { console.error('[HA DeskLink] getExternalAuth error:', e); }");
        sb.AppendLine("    },");
        sb.AppendLine("    saveExternalAuth: function(data, cb) { try { if (cb) cb(); } catch(e) {} },");
        sb.AppendLine("    revokeExternalAuth: function(cb) { try { if (cb) cb(); } catch(e) {} if (window.close) window.close(); }");
        sb.AppendLine("  };");
        sb.AppendLine("  console.log('[HA DeskLink] externalAuth interface injected');");
        sb.AppendLine("})();");
        return sb.ToString();
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

    public static void Open(string haUrl, string token)
    {
        if (_instance != null && !_instance.IsDisposed)
        {
            _instance.Activate();
            return;
        }
        _instance = new DashboardWindow(haUrl, token);
        _instance.Show();
    }
}
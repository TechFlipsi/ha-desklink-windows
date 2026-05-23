
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
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// First-time setup wizard for connecting to Home Assistant and MQTT.
/// </summary>
public class SetupWizard : Form
{
    private TextBox _urlBox = null!;
    private TextBox _tokenBox = null!;
    private CheckBox _sslCheck = null!;
    private TableLayoutPanel _mainPanel = null!;

    // MQTT fields populated by the HA connection step
    private string _savedHaUrl = "";
    private string _savedHaToken = "";

    public string HaUrl => _savedHaUrl;
    public string HaToken => _savedHaToken;
    public bool VerifySsl => _sslCheck?.Checked ?? false;
    public bool MqttConfigured { get; private set; }

    public SetupWizard()
    {
        Text = "HA DeskLink - Setup";
        Size = new Size(520, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        _mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(20),
        };
        _mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        _mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "HA DeskLink Setup",
            Font = new Font("", 14, FontStyle.Bold),
            AutoSize = true,
        };
        _mainPanel.Controls.Add(title, 0, 0);
        _mainPanel.SetColumnSpan(title, 2);

        var subtitle = new Label { Text = "Verbinde deinen PC mit Home Assistant", AutoSize = true };
        _mainPanel.Controls.Add(subtitle, 0, 1);
        _mainPanel.SetColumnSpan(subtitle, 2);

        _mainPanel.Controls.Add(new Label { Text = "HA URL:", AutoSize = true }, 0, 2);
        _urlBox = new TextBox { Text = "https://homeassistant.local:8123", Dock = DockStyle.Fill };
        _mainPanel.Controls.Add(_urlBox, 1, 2);

        _mainPanel.Controls.Add(new Label { Text = "Long-Lived Token:", AutoSize = true }, 0, 3);
        _tokenBox = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
        _mainPanel.Controls.Add(_tokenBox, 1, 3);

        _sslCheck = new CheckBox { Text = "SSL-Zertifikat pr\u00fcfen", AutoSize = true };
        _mainPanel.Controls.Add(_sslCheck, 0, 4);
        _mainPanel.SetColumnSpan(_sslCheck, 2);

        var connectBtn = new Button
        {
            Text = "Verbinden",
            Size = new Size(150, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        connectBtn.Click += OnConnect;
        _mainPanel.Controls.Add(connectBtn, 1, 5);

        var hint = new Label
        {
            Text = "Token: HA \u2192 Profil \u2192 Sicherheit \u2192 Long-Lived Access Tokens",
            Font = new Font("", 8),
            ForeColor = Color.Gray,
            AutoSize = true,
        };
        _mainPanel.Controls.Add(hint, 0, 5);

        Controls.Add(_mainPanel);
    }

    private async void OnConnect(object? sender, EventArgs e)
    {
        var btn = (Button)sender!;
        btn.Enabled = false;
        btn.Text = "Verbinde...";

        try
        {
            var configDir = Config.GetConfigDir();
            var api = new HaApiClient(configDir, _sslCheck.Checked);
            await api.RegisterAsync(_urlBox.Text.Trim(), _tokenBox.Text.Trim());

            // Save the HA credentials for MQTT step
            _savedHaUrl = _urlBox.Text.Trim();
            _savedHaToken = _tokenBox.Text.Trim();

            // Transition to MQTT step
            ShowMqttStep();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Verbindung fehlgeschlagen:\n{ex.Message}", "Fehler",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            btn.Enabled = true;
            btn.Text = "Verbinden";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // STEP 2: MQTT Configuration
    // ═══════════════════════════════════════════════════════════════

    private void ShowMqttStep()
    {
        // Clear the main panel and rebuild with MQTT step
        _mainPanel.Controls.Clear();
        _mainPanel.RowCount = 9;

        // Title
        var stepTitle = new Label
        {
            Text = "HA DeskLink Setup - MQTT",
            Font = new Font("", 14, FontStyle.Bold),
            AutoSize = true,
        };
        _mainPanel.Controls.Add(stepTitle, 0, 0);
        _mainPanel.SetColumnSpan(stepTitle, 2);

        // Feature comparison
        var featurePanel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 6,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 8),
        };
        featurePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        featurePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        featurePanel.Controls.Add(new Label { Text = "\U0001f539 Ohne MQTT", Font = new Font("", 9, FontStyle.Bold), AutoSize = true }, 0, 0);
        featurePanel.Controls.Add(new Label { Text = "\U0001f539 Mit MQTT", Font = new Font("", 9, FontStyle.Bold), AutoSize = true }, 1, 0);

        featurePanel.Controls.Add(new Label { Text = "\u2713 PC Status", Font = new Font("", 8), ForeColor = Color.Gray, AutoSize = true }, 0, 1);
        featurePanel.Controls.Add(new Label { Text = "\u2713 PC Status", Font = new Font("", 8), ForeColor = Color.Gray, AutoSize = true }, 1, 1);

        featurePanel.Controls.Add(new Label { Text = "\u2713 Sensoren", Font = new Font("", 8), ForeColor = Color.Gray, AutoSize = true }, 0, 2);
        featurePanel.Controls.Add(new Label { Text = "\u2713 Sensoren", Font = new Font("", 8), ForeColor = Color.Gray, AutoSize = true }, 1, 2);

        featurePanel.Controls.Add(new Label { Text = "\u2713 Quick Actions", Font = new Font("", 8), ForeColor = Color.Gray, AutoSize = true }, 0, 3);
        featurePanel.Controls.Add(new Label { Text = "\u2713 Quick Actions", Font = new Font("", 8), ForeColor = Color.Gray, AutoSize = true }, 1, 3);

        featurePanel.Controls.Add(new Label { Text = "\u2717 Mediensteuerung", Font = new Font("", 8), ForeColor = Color.Red, AutoSize = true }, 0, 4);
        featurePanel.Controls.Add(new Label { Text = "\u2713 Mediensteuerung", Font = new Font("", 8), ForeColor = Color.Green, AutoSize = true }, 1, 4);

        featurePanel.Controls.Add(new Label { Text = "\u2717 Schnelle Updates", Font = new Font("", 8), ForeColor = Color.Red, AutoSize = true }, 0, 5);
        featurePanel.Controls.Add(new Label { Text = "\u2713 Schnelle Updates", Font = new Font("", 8), ForeColor = Color.Green, AutoSize = true }, 1, 5);

        _mainPanel.Controls.Add(featurePanel, 0, 1);
        _mainPanel.SetColumnSpan(featurePanel, 2);

        // MQTT description
        var mqttDesc = new Label
        {
            Text = "MQTT erm\u00f6glicht Echtzeit-Mediensteuerung und schnellere Sensor-Updates.\nHA DeskLink kann den MQTT-Broker automatisch konfigurieren.",
            Font = new Font("", 9),
            ForeColor = Color.DarkBlue,
            AutoSize = true,
            MaximumSize = new Size(460, 0),
        };
        _mainPanel.Controls.Add(mqttDesc, 0, 2);
        _mainPanel.SetColumnSpan(mqttDesc, 2);

        // Status label
        var mqttStatus = new Label
        {
            Text = "",
            Font = new Font("", 9),
            ForeColor = Color.Gray,
            AutoSize = true,
        };
        _mainPanel.Controls.Add(mqttStatus, 0, 3);
        _mainPanel.SetColumnSpan(mqttStatus, 2);

        // "MQTT nutzen" button
        var mqttBtn = new Button
        {
            Text = "MQTT nutzen",
            Size = new Size(150, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        mqttBtn.FlatAppearance.BorderSize = 0;
        _mainPanel.Controls.Add(mqttBtn, 1, 4);

        // "Ohne MQTT fortfahren" button
        var skipBtn = new Button
        {
            Text = "Ohne MQTT fortfahren",
            Size = new Size(170, 40),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            FlatStyle = FlatStyle.Flat,
        };
        skipBtn.FlatAppearance.BorderSize = 0;
        _mainPanel.Controls.Add(skipBtn, 0, 5);

        // Progress bar (hidden initially)
        var progressBar = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            Visible = false,
            Size = new Size(400, 20),
        };
        _mainPanel.Controls.Add(progressBar, 0, 6);
        _mainPanel.SetColumnSpan(progressBar, 2);

        // Manual config fields (hidden initially, shown if auto-config fails)
        var manualPanel = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 5,
            AutoSize = true,
            Visible = false,
            Margin = new Padding(0, 4, 0, 0),
        };
        manualPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        manualPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        manualPanel.Controls.Add(new Label { Text = "Broker:", AutoSize = true }, 0, 0);
        var manualBroker = new TextBox { Dock = DockStyle.Fill, Text = _savedHaUrl.Replace("https://", "").Replace("http://", "").Split('/')[0].Split(':')[0] };
        manualPanel.Controls.Add(manualBroker, 1, 0);

        manualPanel.Controls.Add(new Label { Text = "Port:", AutoSize = true }, 0, 1);
        var manualPort = new TextBox { Dock = DockStyle.Fill, Text = "1883" };
        manualPanel.Controls.Add(manualPort, 1, 1);

        manualPanel.Controls.Add(new Label { Text = "Benutzername:", AutoSize = true }, 0, 2);
        var manualUser = new TextBox { Dock = DockStyle.Fill };
        manualPanel.Controls.Add(manualUser, 1, 2);

        manualPanel.Controls.Add(new Label { Text = "Passwort:", AutoSize = true }, 0, 3);
        var manualPass = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
        manualPanel.Controls.Add(manualPass, 1, 3);

        var manualSslCheck = new CheckBox { Text = "SSL/TLS verwenden", AutoSize = true };
        manualPanel.Controls.Add(manualSslCheck, 0, 4);
        manualPanel.SetColumnSpan(manualSslCheck, 2);

        var manualTestBtn = new Button
        {
            Text = "Testen",
            AutoSize = true,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        manualTestBtn.FlatAppearance.BorderSize = 0;

        var manualSaveBtn = new Button
        {
            Text = "\u00dcbernehmen & fortfahren",
            AutoSize = true,
            BackColor = Color.FromArgb(0, 134, 100),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        manualSaveBtn.FlatAppearance.BorderSize = 0;

        var manualBtnPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
        };
        manualBtnPanel.Controls.Add(manualTestBtn);
        manualBtnPanel.Controls.Add(manualSaveBtn);
        manualPanel.Controls.Add(new Label(), 0, 5);
        manualPanel.Controls.Add(manualBtnPanel, 1, 5);

        _mainPanel.Controls.Add(manualPanel, 0, 7);
        _mainPanel.SetColumnSpan(manualPanel, 2);

        // Retry button for Mosquitto (hidden initially)
        var retryBtn = new Button
        {
            Text = "Erneut pr\u00fcfen",
            Size = new Size(140, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Visible = false,
        };
        retryBtn.FlatAppearance.BorderSize = 0;
        _mainPanel.Controls.Add(retryBtn, 0, 8);

        // ── Button handlers ──

        mqttBtn.Click += async (s, args) =>
        {
            mqttBtn.Enabled = false;
            mqttBtn.Text = "Konfiguriere...";
            skipBtn.Enabled = false;
            progressBar.Visible = true;
            mqttStatus.Text = "Verbinde mit MQTT-Broker...";
            mqttStatus.ForeColor = Color.Gray;

            try
            {
                var result = await MqttSetupHelper.AutoConfigureAsync(_savedHaUrl, _savedHaToken);

                if (result.Success)
                {
                    var config = Config.Load();
                    config.MqttEnabled = true;
                    config.MqttBroker = result.BrokerHost ?? "";
                    config.MqttPort = result.BrokerPort;
                    config.MqttUsername = result.Username ?? "";
                    config.MqttPassword = result.Password ?? "";
                    config.MqttUseSsl = result.UseSsl;
                    config.MqttAutoConfigured = true;
                    config.HaUrl = _savedHaUrl;
                    config.HaToken = _savedHaToken;
                    config.VerifySsl = _sslCheck.Checked;
                    config.Save();

                    mqttStatus.Text = $"\u2713 MQTT erfolgreich konfiguriert!\nBroker: {result.BrokerHost}:{result.BrokerPort}";
                    mqttStatus.ForeColor = Color.Green;
                    mqttBtn.Text = "\u2713 Konfiguriert";
                    mqttBtn.BackColor = Color.FromArgb(0, 134, 100);
                    skipBtn.Enabled = true;
                    MqttConfigured = true;

                    await Task.Delay(500);
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else if (result.MosquittoNotInstalled)
                {
                    progressBar.Visible = false;
                    mqttStatus.Text = "\u26a0\ufe0f Mosquitto MQTT-Broker nicht gefunden.\n\nInstalliere den Mosquitto Broker Add-on in Home Assistant:\nEinstellungen \u2192 Add-ons \u2192 Mosquitto Broker installieren & starten.";
                    mqttStatus.ForeColor = Color.FromArgb(180, 80, 0);
                    retryBtn.Visible = true;
                    mqttBtn.Enabled = true;
                    mqttBtn.Text = "MQTT nutzen";
                    skipBtn.Enabled = true;
                }
                else
                {
                    progressBar.Visible = false;
                    mqttStatus.Text = $"\u26a0\ufe0f Automatische Konfiguration fehlgeschlagen:\n{result.ErrorMessage ?? "Unbekannter Fehler"}\n\nBitte MQTT-Daten manuell eingeben:";
                    mqttStatus.ForeColor = Color.FromArgb(180, 80, 0);
                    manualPanel.Visible = true;
                    mqttBtn.Enabled = true;
                    mqttBtn.Text = "MQTT nutzen";
                    skipBtn.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                progressBar.Visible = false;
                mqttStatus.Text = $"\u2717 Fehler: {ex.Message}";
                mqttStatus.ForeColor = Color.Red;
                mqttBtn.Enabled = true;
                mqttBtn.Text = "MQTT nutzen";
                skipBtn.Enabled = true;
            }
        };

        retryBtn.Click += async (s, args) =>
        {
            retryBtn.Visible = false;
            mqttBtn.PerformClick();
        };

        manualTestBtn.Click += async (s, args) =>
        {
            manualTestBtn.Enabled = false;
            manualTestBtn.Text = "Teste...";
            var host = manualBroker.Text.Trim();
            var portStr = manualPort.Text.Trim();
            if (!int.TryParse(portStr, out var port)) port = 1883;
            var user = manualUser.Text.Trim();
            var pass = manualPass.Text;
            var ssl = manualSslCheck.Checked;

            var ok = await MqttSetupHelper.TestConnectionAsync(host, port,
                string.IsNullOrEmpty(user) ? null : user,
                string.IsNullOrEmpty(pass) ? null : pass, ssl);

            if (ok)
            {
                mqttStatus.Text = $"\u2713 Verbindung zu {host}:{port} erfolgreich!";
                mqttStatus.ForeColor = Color.Green;
            }
            else
            {
                mqttStatus.Text = $"\u2717 Verbindung zu {host}:{port} fehlgeschlagen!";
                mqttStatus.ForeColor = Color.Red;
            }
            manualTestBtn.Enabled = true;
            manualTestBtn.Text = "Testen";
        };

        manualSaveBtn.Click += (s, args) =>
        {
            var host = manualBroker.Text.Trim();
            var portStr = manualPort.Text.Trim();
            if (!int.TryParse(portStr, out var port)) port = 1883;

            var config = Config.Load();
            config.MqttEnabled = true;
            config.MqttBroker = host;
            config.MqttPort = port;
            config.MqttUsername = manualUser.Text.Trim();
            config.MqttPassword = manualPass.Text;
            config.MqttUseSsl = manualSslCheck.Checked;
            config.MqttAutoConfigured = false;
            config.HaUrl = _savedHaUrl;
            config.HaToken = _savedHaToken;
            config.VerifySsl = _sslCheck.Checked;
            config.Save();

            MqttConfigured = true;
            mqttStatus.Text = "\u2713 MQTT manuell konfiguriert!";
            mqttStatus.ForeColor = Color.Green;

            Task.Delay(500).ContinueWith(_ =>
            {
                BeginInvoke(new Action(() =>
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }));
            });
        };

        skipBtn.Click += (s, args) =>
        {
            var config = Config.Load();
            config.MqttEnabled = false;
            config.MqttAutoConfigured = false;
            config.HaUrl = _savedHaUrl;
            config.HaToken = _savedHaToken;
            config.VerifySsl = _sslCheck.Checked;
            config.Save();

            DialogResult = DialogResult.OK;
            Close();
        };
    }
}
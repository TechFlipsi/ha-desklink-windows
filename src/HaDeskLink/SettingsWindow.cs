// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace HaDeskLink;

public class SettingsWindow : Form
{
    private readonly Config _config;
    private readonly Action _onReconnect;
    private readonly HaApiClient? _api;
    private TextBox _urlBox = null!;
    private TextBox _tokenBox = null!;
    private CheckBox _sslCheck = null!;
    private CheckBox _autostartCheck = null!;
    private NumericUpDown _intervalBox = null!;
    private ComboBox _updateChannelBox = null!;
    private ComboBox _languageBox = null!;
    private ComboBox _themeBox = null!;
    private ComboBox _notifPosBox = null!;
    private ComboBox _notifMonitorBox = null!;
    private ComboBox _hotkeyModBox = null!;
    private ComboBox _hotkeyKeyBox = null!;
    private ComboBox _hotkeyDashModBox = null!;
    private ComboBox _hotkeyDashKeyBox = null!;
    private ComboBox _hotkeySettingsModBox = null!;
    private ComboBox _hotkeySettingsKeyBox = null!;
    private Label _statusLabel = null!;
    private ListBox _qaList = null!;
    private List<(string entityId, string friendlyName)> _entities = new();

    // MQTT-Steuerlemente
    private CheckBox _mqttEnabledCheck = null!;
    private TextBox _mqttBrokerBox = null!;
    private TextBox _mqttPortBox = null!;
    private TextBox _mqttUserBox = null!;
    private TextBox _mqttPassBox = null!;
    private CheckBox _mqttSslCheck = null!;
    private TextBox _mqttFallbackBox = null!;
    private Label _mqttStatusLabel = null!;

    // Layout-Panel — wird für ApplyTheme benötigt, damit Section-Cards neu eingefärbt werden können
    private FlowLayoutPanel _contentFlow = null!;

    // Dark Theme Farben
    private static readonly Color DarkBg = Color.FromArgb(32, 32, 32);
    private static readonly Color DarkFg = Color.FromArgb(230, 230, 230);
    private static readonly Color DarkInput = Color.FromArgb(48, 48, 48);
    private static readonly Color DarkSectionBg = Color.FromArgb(40, 40, 40);
    private static readonly Color AccentBlue = Color.FromArgb(0, 120, 215);
    private static readonly Color SuccessGreen = Color.FromArgb(0, 134, 100);
    private static readonly Color WarningOrange = Color.FromArgb(180, 80, 0);
    private static readonly Color DangerRed = Color.FromArgb(200, 50, 50);

    public SettingsWindow(Config config, Action onReconnect, HaApiClient? api = null)
    {
        _config = config;
        _onReconnect = onReconnect;
        _api = api;
        Text = $"HA DeskLink - {Localization.Get("settings_title")}";
        Size = new Size(640, 1000);
        MinimumSize = new Size(520, 700);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        InitializeComponents();
        LoadSettings();
        LoadQuickActionsList();
        ApplyTheme(_config.Theme);
    }

    // ═══════════════════════════════════════════════════════
    // INITIALISIERUNG — Layout mit Bottom-Bar und scrollbarem Content
    // ═══════════════════════════════════════════════════════

    private void InitializeComponents()
    {
        // Scroll-Bereich für den gesamten Content
        var mainPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24, 24, 24, 8) };

        // FlowLayoutPanel für die Sections (von oben nach unten gestapelt)
        _contentFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoScroll = false,
            Width = mainPanel.Width - 60,
        };
        mainPanel.Resize += (s, e) => { _contentFlow.Width = mainPanel.Width - 60; };

        // ═══════════════════════════════════════════
        // 1. 🔌 VERBINDUNG
        // ═══════════════════════════════════════════
        _contentFlow.Controls.Add(BuildConnectionSection());

        // ═══════════════════════════════════════════
        // 2. ⚙️ ALLGEMEIN (Autostart, Sensor-Intervall, Update-Kanal + Reset/Reregister)
        // ═══════════════════════════════════════════
        _contentFlow.Controls.Add(BuildGeneralSection());

        // ═══════════════════════════════════════════
        // 3. 🎨 ERSCHEINUNGSBILD (Sprache, Theme)
        // ═══════════════════════════════════════════
        _contentFlow.Controls.Add(BuildAppearanceSection());

        // ═══════════════════════════════════════════
        // 4. 🔔 BENACHRICHTIGUNGEN (Position, Monitor)
        // ═══════════════════════════════════════════
        _contentFlow.Controls.Add(BuildNotificationsSection());

        // ═══════════════════════════════════════════
        // 5. ⌨️ TASTENKOMBINATIONEN (3 Hotkey Rows)
        // ═══════════════════════════════════════════
        _contentFlow.Controls.Add(BuildHotkeysSection());

        // ═══════════════════════════════════════════
        // 6. 📡 MQTT-EINSTELLUNGEN
        // ═══════════════════════════════════════════
        _contentFlow.Controls.Add(BuildMqttSection());

        // ═══════════════════════════════════════════
        // 7. ⚡ QUICK ACTIONS
        // ═══════════════════════════════════════════
        _contentFlow.Controls.Add(BuildQuickActionsSection());

        mainPanel.Controls.Add(_contentFlow);
        Controls.Add(mainPanel);

        // ═══════════════════════════════════════════
        // 8. BOTTOM BAR — Save, Reconnect, Status (immer sichtbar)
        // ═══════════════════════════════════════════
        BuildBottomBar();
    }

    // ═══════════════════════════════════════════════════════
    // SECTION-BUILDER — Jede Section ist ein Card-artiges Panel
    // ═══════════════════════════════════════════════════════

    // ─── Helper: Card-Panel erstellen (Hintergrund, Padding, Border) ───
    private Panel MakeCardPanel()
    {
        return new Panel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(16),
            Margin = new Padding(0, 0, 0, 16),  // 16px Abstand zwischen Sections
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = DarkSectionBg,
            BorderStyle = BorderStyle.FixedSingle,
        };
    }

    // ─── Helper: TableLayoutPanel für 2-Spalten Layout ───
    private TableLayoutPanel MakeFieldTable(int rowCount)
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = rowCount,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0, 8, 0, 0),
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    // ─── Section 1: 🔌 Verbindung ───
    private Panel BuildConnectionSection()
    {
        var card = MakeCardPanel();
        card.Controls.Add(MakeSectionHeader("🔌 " + Localization.Get("settings_connection", "Verbindung")));

        var table = MakeFieldTable(3);

        _urlBox = new TextBox { Dock = DockStyle.Fill, Text = "https://homeassistant.local:8123", Height = 28 };
        AddTooltip(_urlBox, Localization.Get("tooltip_ha_url"));
        table.Controls.Add(MakeLabel(Localization.Get("settings_ha_url")), 0, 0);
        table.Controls.Add(_urlBox, 1, 0);

        _tokenBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, Height = 28 };
        AddTooltip(_tokenBox, Localization.Get("tooltip_token"));
        table.Controls.Add(MakeLabel(Localization.Get("settings_token")), 0, 1);
        table.Controls.Add(_tokenBox, 1, 1);

        _sslCheck = new CheckBox { Text = Localization.Get("settings_verify_ssl"), AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        AddTooltip(_sslCheck, Localization.Get("tooltip_ssl"));
        table.Controls.Add(_sslCheck, 0, 2);
        table.SetColumnSpan(_sslCheck, 2);

        card.Controls.Add(table);
        return card;
    }

    // ─── Section 2: ⚙️ Allgemein (Autostart, Sensor-Intervall, Update-Kanal, Reset/Reregister) ───
    private Panel BuildGeneralSection()
    {
        var card = MakeCardPanel();
        card.Controls.Add(MakeSectionHeader("⚙️ " + Localization.Get("settings_general", "Allgemein")));

        var table = MakeFieldTable(5);

        // Autostart
        _autostartCheck = new CheckBox { Text = Localization.Get("settings_autostart"), AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        AddTooltip(_autostartCheck, Localization.Get("tooltip_autostart"));
        table.Controls.Add(_autostartCheck, 0, 0);
        table.SetColumnSpan(_autostartCheck, 2);

        // Sensor-Intervall
        table.Controls.Add(MakeLabel(Localization.Get("settings_sensor_interval")), 0, 1);
        _intervalBox = new NumericUpDown { Minimum = 10, Maximum = 300, Value = 30, Dock = DockStyle.Fill, Height = 28 };
        AddTooltip(_intervalBox, Localization.Get("tooltip_sensor_interval"));
        var intervalHint = new Label
        {
            Text = Localization.Get("sensors_interval_hint"),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8f),
            Margin = new Padding(0, 2, 0, 0),
        };
        table.Controls.Add(_intervalBox, 1, 1);
        table.Controls.Add(intervalHint, 0, 2);
        table.SetColumnSpan(intervalHint, 2);

        // Update-Kanal
        table.Controls.Add(MakeLabel(Localization.Get("settings_update_channel")), 0, 3);
        _updateChannelBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
        AddTooltip(_updateChannelBox, Localization.Get("tooltip_update_channel"));
        _updateChannelBox.Items.AddRange(new object[] { Localization.Get("settings_channel_stable"), Localization.Get("settings_channel_prerelease") });
        table.Controls.Add(_updateChannelBox, 1, 3);

        card.Controls.Add(table);

        // Reset Device ID und Re-register Sensors in der Allgemein-Section
        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0, 12, 0, 0),
        };

        var resetBtn = MakeButton("🔑 " + Localization.Get("settings_reset_device", "Geräte-ID zurücksetzen"), WarningOrange, OnResetDeviceId);
        AddTooltip(resetBtn, Localization.Get("tooltip_reset_device"));

        var reregisterBtn = MakeButton("📊 " + Localization.Get("settings_reregister_sensors", "Sensoren neu registrieren"), SuccessGreen, OnReRegisterSensors);
        AddTooltip(reregisterBtn, Localization.Get("tooltip_reregister"));

        actionPanel.Controls.Add(resetBtn);
        actionPanel.Controls.Add(reregisterBtn);
        card.Controls.Add(actionPanel);

        return card;
    }

    // ─── Section 3: 🎨 Erscheinungsbild (Sprache, Theme) ───
    private Panel BuildAppearanceSection()
    {
        var card = MakeCardPanel();
        card.Controls.Add(MakeSectionHeader("🎨 " + Localization.Get("settings_appearance", "Erscheinungsbild")));

        var table = MakeFieldTable(2);

        // Sprache
        table.Controls.Add(MakeLabel(Localization.Get("settings_language")), 0, 0);
        _languageBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
        foreach (var lang in Localization.AvailableLanguages)
            _languageBox.Items.Add($"{Localization.GetLanguageName(lang)} ({lang})");
        table.Controls.Add(_languageBox, 1, 0);

        // Theme
        table.Controls.Add(MakeLabel(Localization.Get("settings_theme")), 0, 1);
        _themeBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
        AddTooltip(_themeBox, Localization.Get("tooltip_theme"));
        _themeBox.Items.AddRange(new object[] { Localization.Get("settings_theme_system"), Localization.Get("settings_theme_light"), Localization.Get("settings_theme_dark") });
        table.Controls.Add(_themeBox, 1, 1);

        card.Controls.Add(table);
        return card;
    }

    // ─── Section 4: 🔔 Benachrichtigungen (Position, Monitor) ───
    private Panel BuildNotificationsSection()
    {
        var card = MakeCardPanel();
        card.Controls.Add(MakeSectionHeader("🔔 " + Localization.Get("settings_notifications", "Benachrichtigungen")));

        var table = MakeFieldTable(2);

        // Position
        table.Controls.Add(MakeLabel(Localization.Get("settings_notif_position")), 0, 0);
        _notifPosBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
        AddTooltip(_notifPosBox, Localization.Get("tooltip_notif_position"));
        _notifPosBox.Items.AddRange(new object[] {
            Localization.Get("settings_notif_bottom_left"),
            Localization.Get("settings_notif_bottom_right"),
            Localization.Get("settings_notif_top_left"),
            Localization.Get("settings_notif_top_right")
        });
        table.Controls.Add(_notifPosBox, 1, 0);

        // Monitor
        table.Controls.Add(MakeLabel(Localization.Get("settings_notif_monitor")), 0, 1);
        _notifMonitorBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Height = 28 };
        AddTooltip(_notifMonitorBox, Localization.Get("tooltip_notif_monitor"));
        for (int i = 0; i < Screen.AllScreens.Length; i++)
        {
            var label = i == 0
                ? $"{Localization.Get("settings_notif_primary_monitor")} ({Screen.AllScreens[i].DeviceName?.Trim(':')})"
                : $"Monitor {i + 1} ({Screen.AllScreens[i].DeviceName?.Trim(':')})";
            _notifMonitorBox.Items.Add(label);
        }
        table.Controls.Add(_notifMonitorBox, 1, 1);

        card.Controls.Add(table);
        return card;
    }

    // ─── Section 5: ⌨️ Tastenkombinationen (3 Hotkey Rows) ───
    private Panel BuildHotkeysSection()
    {
        var card = MakeCardPanel();
        card.Controls.Add(MakeSectionHeader("⌨️ " + Localization.Get("settings_hotkeys", "Tastenkombinationen")));

        var table = MakeFieldTable(3);

        // Quick Actions Hotkey
        table.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_qa")), 0, 0);
        var hotkeyPanel = CreateHotkeyRow(out _hotkeyModBox, out _hotkeyKeyBox);
        AddTooltip(_hotkeyModBox, Localization.Get("tooltip_hotkey_qa"));
        table.Controls.Add(hotkeyPanel, 1, 0);

        // Dashboard Hotkey
        table.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_dashboard")), 0, 1);
        var dashPanel = CreateHotkeyRow(out _hotkeyDashModBox, out _hotkeyDashKeyBox);
        AddTooltip(dashPanel, Localization.Get("tooltip_hotkey_dashboard"));
        table.Controls.Add(dashPanel, 1, 1);

        // Settings Hotkey
        table.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_settings")), 0, 2);
        var settingsPanel = CreateHotkeyRow(out _hotkeySettingsModBox, out _hotkeySettingsKeyBox);
        AddTooltip(settingsPanel, Localization.Get("tooltip_hotkey_settings"));
        table.Controls.Add(settingsPanel, 1, 2);

        card.Controls.Add(table);
        return card;
    }

    // ─── Section 6: 📡 MQTT ───
    private Panel BuildMqttSection()
    {
        var card = MakeCardPanel();
        card.Controls.Add(MakeSectionHeader("📡 " + Localization.Get("mqtt_settings")));

        var table = MakeFieldTable(9);

        // MQTT aktivieren
        _mqttEnabledCheck = new CheckBox { Text = Localization.Get("mqtt_enabled"), AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        AddTooltip(_mqttEnabledCheck, "MQTT für Echtzeit-Mediensteuerung und schnelle Sensor-Updates aktivieren");
        table.Controls.Add(_mqttEnabledCheck, 0, 0);
        table.SetColumnSpan(_mqttEnabledCheck, 2);

        // Broker
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_broker")), 0, 1);
        _mqttBrokerBox = new TextBox { Dock = DockStyle.Fill, Height = 28 };
        AddTooltip(_mqttBrokerBox, "MQTT-Broker Hostname (z.B. homeassistant.local)");
        table.Controls.Add(_mqttBrokerBox, 1, 1);

        // Port
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_port")), 0, 2);
        _mqttPortBox = new TextBox { Dock = DockStyle.Fill, Text = "1883", Height = 28 };
        AddTooltip(_mqttPortBox, "MQTT-Broker Port (Standard: 1883, SSL: 8883)");
        table.Controls.Add(_mqttPortBox, 1, 2);

        // Username
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_username")), 0, 3);
        _mqttUserBox = new TextBox { Dock = DockStyle.Fill, Height = 28 };
        AddTooltip(_mqttUserBox, "MQTT-Benutzername (optional, leer lassen bei anonymem Zugang)");
        table.Controls.Add(_mqttUserBox, 1, 3);

        // Password
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_password")), 0, 4);
        _mqttPassBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, Height = 28 };
        AddTooltip(_mqttPassBox, "MQTT-Passwort (optional)");
        table.Controls.Add(_mqttPassBox, 1, 4);

        // SSL
        _mqttSslCheck = new CheckBox { Text = Localization.Get("mqtt_use_ssl"), AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        AddTooltip(_mqttSslCheck, "SSL/TLS für MQTT-Verbindung aktivieren");
        table.Controls.Add(_mqttSslCheck, 0, 5);
        table.SetColumnSpan(_mqttSslCheck, 2);

        // Fallback-Adresse
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_fallback_address", "Fallback-Adresse")), 0, 6);
        _mqttFallbackBox = new TextBox { Dock = DockStyle.Fill, Height = 28 };
        AddTooltip(_mqttFallbackBox, "Alternative MQTT-Broker-Adresse (z.B. lokale IP), falls die Hauptadresse nicht erreichbar ist. Leer lassen für keinen Fallback.");
        table.Controls.Add(_mqttFallbackBox, 1, 6);

        // Verbindung testen Button
        table.Controls.Add(MakeLabel(Localization.Get("mqtt_test_connection", "Verbindung testen")), 0, 7);
        var mqttTestBtn = MakeButton("🔌 " + Localization.Get("mqtt_test_connection", "Verbindung testen"), SuccessGreen, OnMqttTestConnection);
        AddTooltip(mqttTestBtn, "Verbindung zum MQTT-Broker testen, bevor gespeichert wird");
        table.Controls.Add(mqttTestBtn, 1, 7);

        // Status Label
        _mqttStatusLabel = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.Gray,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        table.Controls.Add(_mqttStatusLabel, 0, 8);
        table.SetColumnSpan(_mqttStatusLabel, 2);

        card.Controls.Add(table);
        _mqttEnabledCheck.CheckedChanged += (s, e) => UpdateMqttStatusLabel();

        return card;
    }

    // ─── Section 7: ⚡ Quick Actions ───
    private Panel BuildQuickActionsSection()
    {
        var card = MakeCardPanel();
        card.Controls.Add(MakeSectionHeader("⚡ " + Localization.Get("settings_quickactions")));
        card.Controls.Add(new Label
        {
            Text = Localization.Get("settings_quickactions_desc"),
            AutoSize = true,
            ForeColor = Color.Gray,
            Margin = new Padding(0, 4, 0, 8),
        });

        // Load Entities Button
        var qaLoadPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 8),
        };
        var loadBtn = MakeButton("📥 " + Localization.Get("settings_load_entities", "Entities laden"), SuccessGreen, OnLoadEntities);
        AddTooltip(loadBtn, Localization.Get("tooltip_load_entities"));
        qaLoadPanel.Controls.Add(loadBtn);
        card.Controls.Add(qaLoadPanel);

        // Entity ListBox
        _qaList = new ListBox
        {
            Dock = DockStyle.Top,
            Height = 180,
            MinimumSize = new Size(0, 120),
            Margin = new Padding(0, 0, 0, 8),
        };
        card.Controls.Add(_qaList);

        // Add / Edit / Remove Buttons
        var qaEditPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 0),
        };

        var addBtn = MakeButton("➕ " + Localization.Get("settings_qa_add", "Hinzufügen"), SuccessGreen, OnAddQuickAction);
        AddTooltip(addBtn, Localization.Get("tooltip_add_qa"));

        var editBtn = MakeButton("✏️ " + Localization.Get("settings_qa_edit", "Bearbeiten"), Color.FromArgb(100, 100, 100), OnEditQuickAction);
        AddTooltip(editBtn, Localization.Get("tooltip_edit_qa"));

        var removeBtn = MakeButton("🗑️ " + Localization.Get("settings_qa_remove", "Entfernen"), WarningOrange, OnRemoveQuickAction);
        AddTooltip(removeBtn, Localization.Get("tooltip_remove_qa"));

        qaEditPanel.Controls.Add(addBtn);
        qaEditPanel.Controls.Add(editBtn);
        qaEditPanel.Controls.Add(removeBtn);
        card.Controls.Add(qaEditPanel);

        return card;
    }

    // ═══════════════════════════════════════════════════════
    // BOTTOM BAR — Save, Reconnect, Status (immer sichtbar)
    // ═══════════════════════════════════════════════════════

    private void BuildBottomBar()
    {
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            Padding = new Padding(24, 12, 24, 12),
            BackColor = DarkSectionBg,
        };

        var buttonFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0),
            Margin = new Padding(0),
            WrapContents = false,
        };

        var saveBtn = MakeButton("💾 " + Localization.Get("settings_save"), AccentBlue, OnSave);
        AddTooltip(saveBtn, Localization.Get("tooltip_save"));

        var reconnectBtn = MakeButton("🔄 " + Localization.Get("settings_reconnect", "Neu verbinden"), Color.FromArgb(0, 100, 180), OnReconnectClicked);
        AddTooltip(reconnectBtn, Localization.Get("tooltip_reconnect"));

        buttonFlow.Controls.Add(saveBtn);
        buttonFlow.Controls.Add(reconnectBtn);
        bottomPanel.Controls.Add(buttonFlow);

        // Status-Label rechtsbündig in der Bottom Bar
        _statusLabel = new Label
        {
            Text = "",
            ForeColor = Color.Gray,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(8, 0, 0, 0),
            Font = new Font("Segoe UI", 9f),
        };
        bottomPanel.Controls.Add(_statusLabel);

        Controls.Add(bottomPanel);
    }

    // ═══════════════════════════════════════════════════════
    // HELPER METHODEN
    // ═══════════════════════════════════════════════════════

    // ─── Helper: Hotkey-Row (Modifier Dropdown + "+" + Key Dropdown) ───
    private static FlowLayoutPanel CreateHotkeyRow(out ComboBox modBox, out ComboBox keyBox)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        modBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Height = 28 };
        modBox.Items.AddRange(new object[] { "Ctrl+Shift", "Ctrl+Alt", "Ctrl", "Alt", "Shift", Localization.Get("settings_hotkey_none") });
        panel.Controls.Add(modBox);
        panel.Controls.Add(new Label { Text = "+", AutoSize = true, Margin = new Padding(4, 6, 4, 0) });
        keyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Height = 28 };
        keyBox.Items.AddRange(new object[] { "H", "Q", "A", "S", "D", "F", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Space" });
        panel.Controls.Add(keyBox);
        return panel;
    }

    // ─── Helper: Section Header (größer, fett, mit Abstand) ───
    private static Label MakeSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
            Dock = DockStyle.Top,
        };
    }

    // ─── Helper: Label (rechtsbündig für linke Spalte) ───
    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 6, 8, 0),
            Anchor = AnchorStyles.Right,
        };
    }

    // ─── Helper: Button mit AutoSize und FlatStyle ───
    private static Button MakeButton(string text, Color color, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(140, 36),
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(2),
            Padding = new Padding(12, 0, 12, 0),
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += onClick;
        return btn;
    }

    // ─── Helper: Tooltips ───
    private static void AddTooltip(Control control, string text)
    {
        var tip = new ToolTip { IsBalloon = true, InitialDelay = 300, ReshowDelay = 100, AutoPopDelay = 8000 };
        tip.SetToolTip(control, text);
    }

    // ═══════════════════════════════════════════════════════
    // QUICK ACTIONS LOGIK
    // ═══════════════════════════════════════════════════════

    private List<QuickAction> GetCurrentQuickActions()
    {
        var actions = new List<QuickAction>();
        try
        {
            var arr = JsonDocument.Parse(_config.QuickActions).RootElement;
            foreach (var item in arr.EnumerateArray())
            {
                var entityId = item.TryGetProperty("entityId", out var eid) ? eid.GetString() ?? "" : "";
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? entityId : entityId;
                if (!string.IsNullOrEmpty(entityId))
                    actions.Add(new QuickAction(entityId, name));
            }
        }
        catch { }
        return actions;
    }

    private void LoadQuickActionsList()
    {
        _qaList.Items.Clear();
        var actions = GetCurrentQuickActions();
        foreach (var a in actions)
            _qaList.Items.Add($"{a.Name} ({a.EntityId})");
    }

    // ═══════════════════════════════════════════════════════
    // BUTTON HANDLERS
    // ═══════════════════════════════════════════════════════

    private void OnSave(object? sender, EventArgs e)
    {
        // URL validieren
        var url = _urlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(Localization.Get("validation_url_empty"), Localization.Get("validation_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _urlBox.Focus();
            return;
        }
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            MessageBox.Show(Localization.Get("validation_url_invalid"), Localization.Get("validation_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _urlBox.Focus();
            return;
        }

        // Token validieren
        if (string.IsNullOrWhiteSpace(_tokenBox.Text.Trim()))
        {
            MessageBox.Show(Localization.Get("validation_token_empty"), Localization.Get("validation_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _tokenBox.Focus();
            return;
        }

        _config.HaUrl = _urlBox.Text.Trim();
        _config.HaToken = _tokenBox.Text.Trim();
        _config.VerifySsl = _sslCheck.Checked;
        _config.Autostart = _autostartCheck.Checked;
        _config.SensorInterval = Math.Max(10, (int)_intervalBox.Value);  // Minimum 10s erzwingen
        _config.UpdateChannel = _updateChannelBox.SelectedIndex == 1 ? "prerelease" : "stable";

        if (_languageBox.SelectedIndex >= 0 && _languageBox.SelectedIndex < Localization.AvailableLanguages.Count)
            _config.Language = Localization.AvailableLanguages[_languageBox.SelectedIndex];

        _config.Theme = _themeBox.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };

        // Benachrichtigungs-Position
        _config.NotificationPosition = _notifPosBox.SelectedIndex switch
        {
            1 => "bottom_right", 2 => "top_left", 3 => "top_right", _ => "bottom_left"
        };
        _config.NotificationMonitor = Math.Max(0, _notifMonitorBox.SelectedIndex);

        // Hotkeys
        _config.HotkeyModifiers = _hotkeyModBox.SelectedIndex switch
        {
            0 => "ctrl_shift", 1 => "ctrl_alt", 2 => "ctrl", 3 => "alt", 4 => "shift", 5 => "none", _ => "ctrl_shift"
        };
        _config.HotkeyKey = _hotkeyKeyBox.SelectedItem?.ToString() ?? "H";

        _config.HotkeyDashboardModifiers = _hotkeyDashModBox.SelectedIndex switch
        {
            0 => "ctrl_shift", 1 => "ctrl_alt", 2 => "ctrl", 3 => "alt", 4 => "shift", 5 => "none", _ => "ctrl_shift"
        };
        _config.HotkeyDashboardKey = _hotkeyDashKeyBox.SelectedItem?.ToString() ?? "D";

        _config.HotkeySettingsModifiers = _hotkeySettingsModBox.SelectedIndex switch
        {
            0 => "ctrl_shift", 1 => "ctrl_alt", 2 => "ctrl", 3 => "alt", 4 => "shift", 5 => "none", _ => "ctrl_shift"
        };
        _config.HotkeySettingsKey = _hotkeySettingsKeyBox.SelectedItem?.ToString() ?? "S";

        // MQTT-Einstellungen
        _config.MqttEnabled = _mqttEnabledCheck.Checked;
        _config.MqttBroker = _mqttBrokerBox.Text.Trim();
        if (int.TryParse(_mqttPortBox.Text.Trim(), out var mqttPort))
            _config.MqttPort = mqttPort;
        _config.MqttUsername = _mqttUserBox.Text.Trim();
        _config.MqttPassword = _mqttPassBox.Text;
        _config.MqttUseSsl = _mqttSslCheck.Checked;
        _config.MqttAutoConfigured = false; // manuelles Speichern
        _config.MqttBrokerFallback = _mqttFallbackBox.Text.Trim();

        _config.Save();
        if (_config.Autostart) Autostart.Enable(); else Autostart.Disable();
        ApplyTheme(_config.Theme);
        // Sprache neu laden, falls sie geändert wurde
        Localization.LoadLanguage(_config.Language);
        _statusLabel.Text = $"✓ {Localization.Get("settings_saved")}";
    }

    /// <summary>
    /// Neu verbinden mit HA — setzt auch Login-Block zurück falls blockiert.
    /// Das ist der EINZIGE Reconnect-Button — keine Duplikate.
    /// </summary>
    private void OnReconnectClicked(object? sender, EventArgs e)
    {
        // WebSocket-Login-Block zurücksetzen (z.B. nach 3 fehlgeschlagenen Token-Versuchen)
        var app = DeskLinkApp.Instance;
        if (app?._wsClient != null && app._wsClient.LoginBlocked)
        {
            app._wsClient.ResetLoginBlock();
            _statusLabel.Text = Localization.Get("status_login_reset");
        }
        else
        {
            _statusLabel.Text = Localization.Get("status_reconnecting");
        }

        // Reconnect asynchron ausführen, um den UI-Thread nicht zu blockieren
        System.Threading.Tasks.Task.Run(() =>
        {
            try { _onReconnect.Invoke(); }
            catch { }
        });

        _statusLabel.Text = Localization.Get("status_reconnect_done");
    }

    private void OnResetDeviceId(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            Localization.Get("settings_reset_device_confirm") + Localization.Get("settings_extra_note_device_reset"),
            Localization.Get("settings_reset_device"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            _api?.ResetDeviceId();
            _statusLabel.Text = $"✓ {Localization.Get("settings_reset_device_done")}";
        }
    }

    private void OnReRegisterSensors(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            Localization.Get("settings_reregister_confirm") + Localization.Get("settings_extra_note_reregister"),
            Localization.Get("settings_reregister_sensors"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try { DeskLinkApp.ReRegisterSensors(); }
                catch { }
            });
            _statusLabel.Text = $"✓ {Localization.Get("settings_reregister_done")}";
        }
    }

    private async void OnLoadEntities(object? sender, EventArgs e)
    {
        if (_api == null)
        {
            _statusLabel.Text = Localization.Get("status_no_ha_connection");
            return;
        }

        _statusLabel.Text = Localization.Get("status_loading_entities");
        try
        {
            _entities = await _api.GetEntitiesAsync();
            _entities = _entities.OrderBy(x => x.entityId).ToList();
            _statusLabel.Text = Localization.Get("status_entities_loaded", _entities.Count);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = Localization.Get("status_error", ex.Message);
        }
    }

    private void OnAddQuickAction(object? sender, EventArgs e)
    {
        if (_entities.Count == 0)
        {
            MessageBox.Show(Localization.Get("settings_load_entities_first"), "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new Form
        {
            Text = Localization.Get("settings_qa_add", "Quick Action hinzufügen"),
            Size = new Size(450, 200),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
        };

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(16) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var entityCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var (entityId, friendlyName) in _entities)
            entityCombo.Items.Add(new EntityItem(entityId, friendlyName));
        if (entityCombo.Items.Count > 0) entityCombo.SelectedIndex = 0;

        var nameBox = new TextBox { Dock = DockStyle.Fill };

        entityCombo.SelectedIndexChanged += (s, args) =>
        {
            if (entityCombo.SelectedItem is EntityItem item)
                nameBox.Text = item.FriendlyName;
        };
        if (entityCombo.Items.Count > 0) entityCombo.SelectedIndex = 0;

        var okBtn = new Button { Text = Localization.Get("settings_qa_add", "Hinzufügen"), Dock = DockStyle.Fill, BackColor = AccentBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        okBtn.FlatAppearance.BorderSize = 0;

        table.Controls.Add(MakeLabel(Localization.Get("settings_qa_entity", "Entity:")), 0, 0);
        table.Controls.Add(entityCombo, 1, 0);
        table.Controls.Add(MakeLabel(Localization.Get("settings_qa_name", "Name:")), 0, 1);
        table.Controls.Add(nameBox, 1, 1);
        table.Controls.Add(new Label(), 0, 2);
        table.Controls.Add(okBtn, 1, 2);

        okBtn.Click += (s, args) =>
        {
            if (entityCombo.SelectedItem is EntityItem item)
            {
                var actions = GetCurrentQuickActions();
                actions.Add(new QuickAction(item.EntityId, string.IsNullOrEmpty(nameBox.Text) ? item.FriendlyName : nameBox.Text));
                _config.QuickActions = JsonSerializer.Serialize(actions, _jsonOpts);
                _config.Save();
                LoadQuickActionsList();
                dialog.Close();
            }
        };

        dialog.Controls.Add(table);
        ApplyThemeToControls(dialog, _config.Theme);
        dialog.ShowDialog(this);
    }

    private void OnEditQuickAction(object? sender, EventArgs e)
    {
        if (_qaList.SelectedIndex < 0)
        {
            MessageBox.Show(Localization.Get("settings_qa_select_first"), "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var actions = GetCurrentQuickActions();
        var idx = _qaList.SelectedIndex;
        if (idx >= actions.Count) return;

        var action = actions[idx];

        using var dialog = new Form
        {
            Text = Localization.Get("settings_qa_edit", "Quick Action bearbeiten"),
            Size = new Size(450, 250),
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
        };

        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(16) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var entityCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var (entityId, friendlyName) in _entities)
            entityCombo.Items.Add(new EntityItem(entityId, friendlyName));

        for (int i = 0; i < entityCombo.Items.Count; i++)
        {
            if (entityCombo.Items[i] is EntityItem ei && ei.EntityId == action.EntityId)
            {
                entityCombo.SelectedIndex = i;
                break;
            }
        }
        if (entityCombo.SelectedIndex < 0 && entityCombo.Items.Count > 0) entityCombo.SelectedIndex = 0;

        var nameBox = new TextBox { Dock = DockStyle.Fill, Text = action.Name };

        entityCombo.SelectedIndexChanged += (s, args) =>
        {
            if (entityCombo.SelectedItem is EntityItem item)
                nameBox.Text = item.FriendlyName;
        };

        var deleteBtn = new Button { Text = "🗑️ " + Localization.Get("settings_qa_remove", "Entfernen"), BackColor = WarningOrange, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        deleteBtn.FlatAppearance.BorderSize = 0;
        var saveBtn = new Button { Text = "💾 " + Localization.Get("settings_save", "Speichern"), BackColor = AccentBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        saveBtn.FlatAppearance.BorderSize = 0;

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        btnPanel.Controls.Add(saveBtn);
        btnPanel.Controls.Add(deleteBtn);

        table.Controls.Add(MakeLabel(Localization.Get("settings_qa_entity", "Entity:")), 0, 0);
        table.Controls.Add(entityCombo, 1, 0);
        table.Controls.Add(MakeLabel(Localization.Get("settings_qa_name", "Name:")), 0, 1);
        table.Controls.Add(nameBox, 1, 1);
        table.Controls.Add(new Label(), 0, 2);
        table.Controls.Add(btnPanel, 1, 2);

        saveBtn.Click += (s, args) =>
        {
            if (entityCombo.SelectedItem is EntityItem item)
            {
                actions[idx] = new QuickAction(item.EntityId, string.IsNullOrEmpty(nameBox.Text) ? item.FriendlyName : nameBox.Text);
                _config.QuickActions = JsonSerializer.Serialize(actions, _jsonOpts);
                _config.Save();
                LoadQuickActionsList();
                dialog.Close();
            }
        };

        deleteBtn.Click += (s, args) =>
        {
            actions.RemoveAt(idx);
            _config.QuickActions = JsonSerializer.Serialize(actions, _jsonOpts);
            _config.Save();
            LoadQuickActionsList();
            dialog.Close();
        };

        dialog.Controls.Add(table);
        ApplyThemeToControls(dialog, _config.Theme);
        dialog.ShowDialog(this);
    }

    private void OnRemoveQuickAction(object? sender, EventArgs e)
    {
        if (_qaList.SelectedIndex < 0)
        {
            MessageBox.Show(Localization.Get("settings_qa_select_first"), "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var actions = GetCurrentQuickActions();
        var idx = _qaList.SelectedIndex;
        if (idx < actions.Count)
        {
            actions.RemoveAt(idx);
            _config.QuickActions = JsonSerializer.Serialize(actions, _jsonOpts);
            _config.Save();
            LoadQuickActionsList();
        }
    }

    // ═══════════════════════════════════════════════════════
    // THEME
    // ═══════════════════════════════════════════════════════

    private void ApplyThemeToControls(Form dialog, string theme)
    {
        bool dark = theme == "dark" || (theme == "system" && IsSystemDark());
        if (dark)
        {
            dialog.BackColor = DarkBg;
            dialog.ForeColor = DarkFg;
        }
        foreach (Control c in GetAllControls(dialog))
        {
            if (dark)
            {
                if (c is TextBox || c is ComboBox || c is NumericUpDown || c is ListBox)
                {
                    c.BackColor = DarkInput;
                    c.ForeColor = DarkFg;
                }
            }
            else
            {
                if (c is TextBox || c is ComboBox || c is NumericUpDown || c is ListBox)
                {
                    c.BackColor = SystemColors.Window;
                    c.ForeColor = SystemColors.WindowText;
                }
            }
        }
    }

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

    private class EntityItem
    {
        public string EntityId { get; }
        public string FriendlyName { get; }
        public EntityItem(string entityId, string friendlyName) { EntityId = entityId; FriendlyName = friendlyName; }
        public override string ToString() => $"{FriendlyName} ({EntityId})";
    }

    private void ApplyTheme(string theme)
    {
        bool dark = theme == "dark" || (theme == "system" && IsSystemDark());
        Color bg = dark ? DarkBg : SystemColors.Window;
        Color fg = dark ? DarkFg : SystemColors.WindowText;
        Color inputBg = dark ? DarkInput : SystemColors.Window;
        Color sectionBg = dark ? DarkSectionBg : Color.White;

        BackColor = bg;
        ForeColor = fg;

        foreach (Control c in GetAllControls(this))
        {
            // Section-Cards: eigenes Background-Color Handling
            if (c is Panel panel && panel.BorderStyle == BorderStyle.FixedSingle)
            {
                panel.BackColor = sectionBg;
                panel.ForeColor = fg;
                continue;
            }

            if (c is TextBox || c is ComboBox || c is NumericUpDown || c is ListBox)
            {
                c.BackColor = inputBg;
                c.ForeColor = fg;
            }
            else if (c is Button btn)
            {
                if (btn.ForeColor != Color.White)
                    btn.ForeColor = fg;
            }
            else if (c is CheckBox cb)
            {
                cb.ForeColor = fg;
            }
            else if (c is Label lbl)
            {
                if (lbl.ForeColor != Color.Gray && lbl.ForeColor != AccentBlue)
                    lbl.ForeColor = fg;
            }
        }
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                var value = key.GetValue("AppsUseLightTheme");
                if (value is int v) return v == 0;
            }
        }
        catch { }
        return false;
    }

    private static IEnumerable<Control> GetAllControls(Control container)
    {
        foreach (Control c in container.Controls)
        {
            yield return c;
            foreach (var child in GetAllControls(c))
                yield return child;
        }
    }

    private void LoadSettings()
    {
        _urlBox.Text = _config.HaUrl;
        _tokenBox.Text = _config.HaToken;
        _sslCheck.Checked = _config.VerifySsl;
        _autostartCheck.Checked = _config.Autostart;
        _intervalBox.Value = Math.Max(10, _config.SensorInterval);  // Minimum 10s erzwingen
        _updateChannelBox.SelectedIndex = _config.UpdateChannel == "prerelease" ? 1 : 0;

        var currentLangIndex = Localization.AvailableLanguages.IndexOf(_config.Language);
        if (currentLangIndex < 0) currentLangIndex = 0;
        _languageBox.SelectedIndex = currentLangIndex;

        _themeBox.SelectedIndex = _config.Theme switch { "light" => 1, "dark" => 2, _ => 0 };

        // Benachrichtigungs-Position
        _notifPosBox.SelectedIndex = _config.NotificationPosition switch
        {
            "bottom_right" => 1, "top_left" => 2, "top_right" => 3, _ => 0
        };

        // Benachrichtigungs-Monitor
        if (_notifMonitorBox.Items.Count > 0)
            _notifMonitorBox.SelectedIndex = Math.Min(_config.NotificationMonitor, _notifMonitorBox.Items.Count - 1);

        // Hotkeys laden
        _hotkeyModBox.SelectedIndex = _config.HotkeyModifiers switch
        {
            "ctrl_shift" => 0, "ctrl_alt" => 1, "ctrl" => 2, "alt" => 3, "shift" => 4, "none" => 5, _ => 0
        };
        var keyIndex = _hotkeyKeyBox.Items.IndexOf(_config.HotkeyKey.ToUpper());
        _hotkeyKeyBox.SelectedIndex = keyIndex >= 0 ? keyIndex : 0;

        _hotkeyDashModBox.SelectedIndex = _config.HotkeyDashboardModifiers switch
        {
            "ctrl_shift" => 0, "ctrl_alt" => 1, "ctrl" => 2, "alt" => 3, "shift" => 4, "none" => 5, _ => 0
        };
        var dashKeyIndex = _hotkeyDashKeyBox.Items.IndexOf(_config.HotkeyDashboardKey.ToUpper());
        _hotkeyDashKeyBox.SelectedIndex = dashKeyIndex >= 0 ? dashKeyIndex : 0;

        _hotkeySettingsModBox.SelectedIndex = _config.HotkeySettingsModifiers switch
        {
            "ctrl_shift" => 0, "ctrl_alt" => 1, "ctrl" => 2, "alt" => 3, "shift" => 4, "none" => 5, _ => 0
        };
        var settingsKeyIndex = _hotkeySettingsKeyBox.Items.IndexOf(_config.HotkeySettingsKey.ToUpper());
        _hotkeySettingsKeyBox.SelectedIndex = settingsKeyIndex >= 0 ? settingsKeyIndex : 0;

        // MQTT-Einstellungen laden
        _mqttEnabledCheck.Checked = _config.MqttEnabled;
        _mqttBrokerBox.Text = _config.MqttBroker;
        _mqttPortBox.Text = _config.MqttPort.ToString();
        _mqttUserBox.Text = _config.MqttUsername;
        _mqttPassBox.Text = _config.MqttPassword;
        _mqttSslCheck.Checked = _config.MqttUseSsl;
        _mqttFallbackBox.Text = _config.MqttBrokerFallback ?? "";
        UpdateMqttStatusLabel();
    }

    private void UpdateMqttStatusLabel()
    {
        if (!_mqttEnabledCheck.Checked)
        {
            _mqttStatusLabel.Text = "○ " + Localization.Get("mqtt_disabled");
            _mqttStatusLabel.ForeColor = Color.Gray;
        }
        else if (_config.MqttBroker.Length > 0)
        {
            _mqttStatusLabel.Text = "● " + Localization.Get("mqtt_connected") + $" ({_config.MqttBroker}:{_config.MqttPort})";
            _mqttStatusLabel.ForeColor = SuccessGreen;
        }
        else
        {
            _mqttStatusLabel.Text = "● " + Localization.Get("mqtt_disconnected");
            _mqttStatusLabel.ForeColor = WarningOrange;
        }
    }

    private async void OnMqttTestConnection(object? sender, EventArgs e)
    {
        var broker = _mqttBrokerBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(broker))
        {
            MessageBox.Show(Localization.Get("validation_mqtt_broker_empty"), Localization.Get("validation_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!int.TryParse(_mqttPortBox.Text.Trim(), out var port) || port < 1 || port > 65535)
        {
            MessageBox.Show(Localization.Get("validation_port_range"), Localization.Get("validation_port_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var btn = (Button)sender!;
        btn.Enabled = false;
        btn.Text = "🔌 " + Localization.Get("mqtt_testing");
        _mqttStatusLabel.Text = "⏳ " + Localization.Get("mqtt_testing_status");
        _mqttStatusLabel.ForeColor = Color.Gray;

        try
        {
            var result = await System.Threading.Tasks.Task.Run(() =>
                MqttSetupHelper.TestConnectionAsync(broker, port, _mqttUserBox.Text.Trim(), _mqttPassBox.Text, _mqttSslCheck.Checked));

            if (result)
            {
                _mqttStatusLabel.Text = $"✓ {Localization.Get("mqtt_test_success")} ({broker}:{port})";
                _mqttStatusLabel.ForeColor = SuccessGreen;
            }
            else
            {
                _mqttStatusLabel.Text = $"✗ {Localization.Get("mqtt_test_failed")} ({broker}:{port})";
                _mqttStatusLabel.ForeColor = DangerRed;
            }
        }
        catch (Exception ex)
        {
            _mqttStatusLabel.Text = $"✗ {Localization.Get("status_error", ex.Message)}";
            _mqttStatusLabel.ForeColor = DangerRed;
        }
        finally
        {
            btn.Enabled = true;
            btn.Text = "🔌 " + Localization.Get("mqtt_test_connection", "Verbindung testen");
        }
    }

    private static SettingsWindow? _instance;

    public static void Open(Config config, Action onReconnect, HaApiClient? api = null)
    {
        if (_instance != null && !_instance.IsDisposed)
        {
            _instance.Activate();
            return;
        }
        _instance = new SettingsWindow(config, onReconnect, api);
        _instance.Show();
    }
}
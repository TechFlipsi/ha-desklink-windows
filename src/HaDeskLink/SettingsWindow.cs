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
    private ComboBox _hotkeyModBox = null!;
    private ComboBox _hotkeyKeyBox = null!;
    private ComboBox _hotkeyDashModBox = null!;
    private ComboBox _hotkeyDashKeyBox = null!;
    private ComboBox _hotkeySettingsModBox = null!;
    private ComboBox _hotkeySettingsKeyBox = null!;
    private Label _statusLabel = null!;
    private ListBox _qaList = null!;
    private List<(string entityId, string friendlyName)> _entities = new();

    // Dark theme colors
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
        Size = new Size(640, 960);
        MinimumSize = new Size(520, 700);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        InitializeComponents();
        LoadSettings();
        LoadQuickActionsList();
        ApplyTheme(_config.Theme);
    }

    private void InitializeComponents()
    {
        var mainPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24) };

        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Width = mainPanel.Width - 60,
        };
        mainPanel.Resize += (s, e) => { content.Width = mainPanel.Width - 60; };

        // ═══════════════════════════════════════════
        // 🔌 VERBINDUNG
        // ═══════════════════════════════════════════
        content.Controls.Add(MakeSectionHeader("🔌 " + Localization.Get("settings_connection", "Verbindung")));

        var connTable = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 5, 0, 15) };
        connTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        connTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _urlBox = new TextBox { Dock = DockStyle.Fill, Text = "https://homeassistant.local:8123" };
        AddTooltip(_urlBox, "Die URL deiner Home Assistant Instanz, z.B. http://192.168.1.100:8123");
        connTable.Controls.Add(MakeLabel(Localization.Get("settings_ha_url")), 0, 0);
        connTable.Controls.Add(_urlBox, 1, 0);

        _tokenBox = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        AddTooltip(_tokenBox, "Long-Lived Access Token aus HA: Profil → Sicherheit → Token erstellen");
        connTable.Controls.Add(MakeLabel(Localization.Get("settings_token")), 0, 1);
        connTable.Controls.Add(_tokenBox, 1, 1);

        _sslCheck = new CheckBox { Text = Localization.Get("settings_verify_ssl"), AutoSize = true };
        AddTooltip(_sslCheck, "SSL-Zertifikat überprüfen (deaktivieren bei Self-Signed)");
        connTable.Controls.Add(_sslCheck, 0, 2);
        connTable.SetColumnSpan(_sslCheck, 2);
        content.Controls.Add(connTable);

        // ═══════════════════════════════════════════
        // ⚙️ ALLGEMEIN
        // ═══════════════════════════════════════════
        content.Controls.Add(MakeSectionHeader("⚙️ " + Localization.Get("settings_general", "Allgemein")));

        var genTable = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 8, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(0, 5, 0, 15) };
        genTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        genTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _autostartCheck = new CheckBox { Text = Localization.Get("settings_autostart"), AutoSize = true };
        AddTooltip(_autostartCheck, "HA DeskLink automatisch beim Windows-Start starten");
        genTable.Controls.Add(_autostartCheck, 0, 0);
        genTable.SetColumnSpan(_autostartCheck, 2);

        genTable.Controls.Add(MakeLabel(Localization.Get("settings_sensor_interval")), 0, 1);
        _intervalBox = new NumericUpDown { Minimum = 10, Maximum = 300, Value = 30, Dock = DockStyle.Fill };
        AddTooltip(_intervalBox, "Wie oft Sensordaten an HA gesendet werden (10-300 Sekunden). 30s ist der Standard.");

        var intervalHint = new Label { Text = "(10 – 300 Sekunden, Standard: 30)", AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f) };
        genTable.Controls.Add(_intervalBox, 1, 1);
        genTable.Controls.Add(intervalHint, 0, 2);
        genTable.SetColumnSpan(intervalHint, 2);

        genTable.Controls.Add(MakeLabel(Localization.Get("settings_update_channel")), 0, 3);
        _updateChannelBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        AddTooltip(_updateChannelBox, "Stabil: Nur getestete Versionen. Pre-Release: Auch Beta-Versionen.");
        _updateChannelBox.Items.AddRange(new object[] { Localization.Get("settings_channel_stable"), Localization.Get("settings_channel_prerelease") });
        genTable.Controls.Add(_updateChannelBox, 1, 3);

        genTable.Controls.Add(MakeLabel(Localization.Get("settings_language")), 0, 4);
        _languageBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var lang in Localization.AvailableLanguages)
            _languageBox.Items.Add($"{Localization.GetLanguageName(lang)} ({lang})");
        genTable.Controls.Add(_languageBox, 1, 4);

        genTable.Controls.Add(MakeLabel(Localization.Get("settings_theme")), 0, 5);
        _themeBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        AddTooltip(_themeBox, "System: Folgt Windows-Einstellung. Hell/Dunkel: Feste Wahl.");
        _themeBox.Items.AddRange(new object[] { Localization.Get("settings_theme_system"), Localization.Get("settings_theme_light"), Localization.Get("settings_theme_dark") });
        genTable.Controls.Add(_themeBox, 1, 5);

        // Hotkey: Quick Actions
        genTable.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_qa")), 0, 6);
        var hotkeyPanel = CreateHotkeyRow(out _hotkeyModBox, out _hotkeyKeyBox);
        AddTooltip(_hotkeyModBox, "Tastenkombination für Quick Actions öffnen");
        genTable.Controls.Add(hotkeyPanel, 1, 6);

        // Hotkey: Dashboard
        genTable.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_dashboard")), 0, 7);
        var dashPanel = CreateHotkeyRow(out _hotkeyDashModBox, out _hotkeyDashKeyBox);
        AddTooltip(dashPanel, "Tastenkombination für HA Dashboard öffnen");
        genTable.Controls.Add(dashPanel, 1, 7);

        // Hotkey: Settings (row 8 — need to add row)
        genTable.RowCount = 9;
        genTable.Controls.Add(MakeLabel(Localization.Get("settings_hotkey_settings")), 0, 8);
        var settingsPanel = CreateHotkeyRow(out _hotkeySettingsModBox, out _hotkeySettingsKeyBox);
        AddTooltip(settingsPanel, "Tastenkombination für Einstellungen öffnen");
        genTable.Controls.Add(settingsPanel, 1, 8);

        content.Controls.Add(genTable);

        // ═══════════════════════════════════════════
        // 🔧 AKTIONEN — klar beschriftet, kein Duplikat
        // ═══════════════════════════════════════════
        content.Controls.Add(MakeSectionHeader("🔧 " + Localization.Get("settings_actions", "Aktionen")));

        var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, Padding = new Padding(0, 5, 0, 15) };

        var saveBtn = MakeButton("💾 " + Localization.Get("settings_save"), AccentBlue, OnSave);
        AddTooltip(saveBtn, "Alle Einstellungen speichern (URL, Token, Intervall, Sprache, usw.)");

        var reconnectBtn = MakeButton("🔄 " + Localization.Get("settings_reconnect", "Neu verbinden"), Color.FromArgb(0, 100, 180), OnReconnectClicked);
        AddTooltip(reconnectBtn, "Verbindung zu Home Assistant neu aufbauen.\nSetzt auch Login-Blockierung zurück falls blockiert.");

        var resetBtn = MakeButton("🔑 " + Localization.Get("settings_reset_device", "Geräte-ID zurücksetzen"), WarningOrange, OnResetDeviceId);
        AddTooltip(resetBtn, "Erstellt eine neue Geräte-ID. Das alte Gerät bleibt in HA.\nNötig wenn du das Gerät in HA löschen willst und neu anmelden musst.");

        var reregisterBtn = MakeButton("📊 " + Localization.Get("settings_reregister_sensors", "Sensoren neu registrieren"), SuccessGreen, OnReRegisterSensors);
        AddTooltip(reregisterBtn, "Alle Sensoren in Home Assistant erneut registrieren.\nHilft wenn Sensoren fehlen oder falsche Werte zeigen.");

        actionPanel.Controls.Add(saveBtn);
        actionPanel.Controls.Add(reconnectBtn);
        actionPanel.Controls.Add(resetBtn);
        actionPanel.Controls.Add(reregisterBtn);
        content.Controls.Add(actionPanel);

        // ═══════════════════════════════════════════
        // ⚡ QUICK ACTIONS
        // ═══════════════════════════════════════════
        content.Controls.Add(MakeSectionHeader("⚡ " + Localization.Get("settings_quickactions")));
        content.Controls.Add(new Label { Text = Localization.Get("settings_quickactions_desc"), AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 0, 0, 8) });

        var qaLoadPanel = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        var loadBtn = MakeButton("📥 " + Localization.Get("settings_load_entities", "Entities laden"), SuccessGreen, OnLoadEntities);
        AddTooltip(loadBtn, "Lädt alle Entities aus Home Assistant für die Entity-Auswahl.\nErforderlich bevor du eine Quick Action hinzufügen kannst.");
        qaLoadPanel.Controls.Add(loadBtn);
        content.Controls.Add(qaLoadPanel);

        _qaList = new ListBox { Dock = DockStyle.Top, Height = 180, MinimumSize = new Size(0, 120) };
        content.Controls.Add(_qaList);

        var qaEditPanel = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Padding = new Padding(0, 4, 0, 15) };

        var addBtn = MakeButton("➕ " + Localization.Get("settings_qa_add", "Hinzufügen"), SuccessGreen, OnAddQuickAction);
        AddTooltip(addBtn, "Neue Quick Action hinzufügen (Entity aus HA auswählen)");

        var editBtn = MakeButton("✏️ " + Localization.Get("settings_qa_edit", "Bearbeiten"), Color.FromArgb(100, 100, 100), OnEditQuickAction);
        AddTooltip(editBtn, "Ausgewählte Quick Action bearbeiten oder löschen");

        var removeBtn = MakeButton("🗑️ " + Localization.Get("settings_qa_remove", "Entfernen"), WarningOrange, OnRemoveQuickAction);
        AddTooltip(removeBtn, "Ausgewählte Quick Action entfernen");

        qaEditPanel.Controls.Add(addBtn);
        qaEditPanel.Controls.Add(editBtn);
        qaEditPanel.Controls.Add(removeBtn);
        content.Controls.Add(qaEditPanel);

        // Status
        _statusLabel = new Label { Text = "", ForeColor = Color.Gray, AutoSize = true, Margin = new Padding(0, 4, 0, 8) };
        content.Controls.Add(_statusLabel);

        mainPanel.Controls.Add(content);
        Controls.Add(mainPanel);
    }

    // ─── Helper: Hotkey row ───
    private static FlowLayoutPanel CreateHotkeyRow(out ComboBox modBox, out ComboBox keyBox)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
        modBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        modBox.Items.AddRange(new object[] { "Ctrl+Shift", "Ctrl+Alt", "Ctrl", "Alt", "Shift", Localization.Get("settings_hotkey_none") });
        panel.Controls.Add(modBox);
        panel.Controls.Add(new Label { Text = "+", AutoSize = true, Margin = new Padding(4, 6, 4, 0) });
        keyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        keyBox.Items.AddRange(new object[] { "H", "Q", "A", "S", "D", "F", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "Space" });
        panel.Controls.Add(keyBox);
        return panel;
    }

    // ─── Helper: Section header ───
    private static Label MakeSectionHeader(string text)
    {
        return new Label { Text = text, Font = new Font("Segoe UI", 11, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 12, 0, 2) };
    }

    private static Label MakeLabel(string text)
    {
        return new Label { Text = text, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
    }

    // ─── Helper: Button with AutoSize ───
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

    // ─── Quick Actions logic ───
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
    // BUTTON HANDLERS — alle klar benannt und dokumentiert
    // ═══════════════════════════════════════════════════════

    private void OnSave(object? sender, EventArgs e)
    {
        // Validate URL
        var url = _urlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("Die HA-URL darf nicht leer sein!", "Ungültige Eingabe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _urlBox.Focus();
            return;
        }
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            MessageBox.Show("Die URL muss mit http:// oder https:// beginnen!", "Ungültige Eingabe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _urlBox.Focus();
            return;
        }

        // Validate Token
        if (string.IsNullOrWhiteSpace(_tokenBox.Text.Trim()))
        {
            MessageBox.Show("Der Long-Lived Access Token darf nicht leer sein!\n\nErstelle einen in HA: Profil → Sicherheit → Token erstellen.", "Ungültige Eingabe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _tokenBox.Focus();
            return;
        }

        _config.HaUrl = _urlBox.Text.Trim();
        _config.HaToken = _tokenBox.Text.Trim();
        _config.VerifySsl = _sslCheck.Checked;
        _config.Autostart = _autostartCheck.Checked;
        _config.SensorInterval = Math.Max(10, (int)_intervalBox.Value);  // Enforce minimum 10s
        _config.UpdateChannel = _updateChannelBox.SelectedIndex == 1 ? "prerelease" : "stable";

        if (_languageBox.SelectedIndex >= 0 && _languageBox.SelectedIndex < Localization.AvailableLanguages.Count)
            _config.Language = Localization.AvailableLanguages[_languageBox.SelectedIndex];

        _config.Theme = _themeBox.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };

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

        _config.Save();
        if (_config.Autostart) Autostart.Enable(); else Autostart.Disable();
        ApplyTheme(_config.Theme);
        _statusLabel.Text = $"✓ {Localization.Get("settings_saved")}";
    }

    /// <summary>
    /// Reconnect to HA — also resets LoginBlock if blocked.
    /// This is the ONLY reconnect button — no duplicates.
    /// </summary>
    private void OnReconnectClicked(object? sender, EventArgs e)
    {
        // Reset WebSocket login block if it was set (e.g., after 3 failed token attempts)
        var app = DeskLinkApp.Instance;
        if (app?._wsClient != null && app._wsClient.LoginBlocked)
        {
            app._wsClient.ResetLoginBlock();
            _statusLabel.Text = "✓ Login-Block zurückgesetzt, verbinde neu…";
        }
        else
        {
            _statusLabel.Text = "🔄 Verbinde neu mit Home Assistant…";
        }

        // Run reconnect async to not block UI thread
        System.Threading.Tasks.Task.Run(() =>
        {
            try { _onReconnect.Invoke(); }
            catch { }
        });

        _statusLabel.Text = "✓ Verbindung wird neu aufgebaut";
    }

    private void OnResetDeviceId(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            Localization.Get("settings_reset_device_confirm") + "\n\nDas alte Gerät bleibt in HA bestehen — du musst es dort manuell löschen.",
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
            Localization.Get("settings_reregister_confirm") + "\n\nDas registriert alle Sensoren neu in Home Assistant.",
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
        if (_api == null) { _statusLabel.Text = "⚠️ Keine Verbindung zu HA"; return; }

        _statusLabel.Text = "⏳ Lade Entities…";
        try
        {
            _entities = await _api.GetEntitiesAsync();
            _entities = _entities.OrderBy(x => x.entityId).ToList();
            _statusLabel.Text = $"✓ {_entities.Count} Entities geladen";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"⚠️ Fehler: {ex.Message}";
        }
    }

    private void OnAddQuickAction(object? sender, EventArgs e)
    {
        if (_entities.Count == 0)
        {
            MessageBox.Show(Localization.Get("settings_load_entities_first", "Bitte zuerst '📥 Entities laden' klicken!"), "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        table.Controls.Add(MakeLabel("Entity:"), 0, 0);
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
            MessageBox.Show(Localization.Get("settings_qa_select_first", "Bitte zuerst eine Quick Action auswählen!"), "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        table.Controls.Add(MakeLabel("Entity:"), 0, 0);
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
            MessageBox.Show(Localization.Get("settings_qa_select_first", "Bitte zuerst eine Quick Action auswählen!"), "HA DeskLink", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        BackColor = bg;
        ForeColor = fg;

        foreach (Control c in GetAllControls(this))
        {
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
        _intervalBox.Value = Math.Max(10, _config.SensorInterval);  // Enforce minimum 10s
        _updateChannelBox.SelectedIndex = _config.UpdateChannel == "prerelease" ? 1 : 0;

        var currentLangIndex = Localization.AvailableLanguages.IndexOf(_config.Language);
        if (currentLangIndex < 0) currentLangIndex = 0;
        _languageBox.SelectedIndex = currentLangIndex;

        _themeBox.SelectedIndex = _config.Theme switch { "light" => 1, "dark" => 2, _ => 0 };

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
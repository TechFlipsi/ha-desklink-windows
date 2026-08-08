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
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HaDeskLink;

/// <summary>
/// JSON-basiertes Lokalisierungssystem.
/// Benutzer können eigene Sprachen hinzufügen, indem sie eine JSON-Datei im Lang/ Ordner ablegen.
/// Die Sprachnamen (Code → Nativer Name) werden aus languages.json gelesen.
/// </summary>
public static class Localization
{
    private static Dictionary<string, string> _strings = new();
    private static Dictionary<string, string> _languageNames = new();
    private static string _currentLanguage = "de";

    /// <summary>
    /// Verfügbare Sprachen (automatisch aus dem Lang/ Ordner erkannt, ohne languages.json)
    /// </summary>
    public static List<string> AvailableLanguages { get; private set; } = new() { "de" };

    /// <summary>
    /// Liest den Anzeigenamen für einen Sprachcode aus languages.json.
    /// Fallback: der Code selbst in Großbuchstaben, wenn languages.json fehlt oder der Code nicht gefunden wird.
    /// </summary>
    public static string GetLanguageName(string code) =>
        _languageNames.TryGetValue(code, out var name) ? name : code.ToUpper();

    /// <summary>
    /// Lädt eine Sprache. Fällt auf Deutsch zurück, wenn die Datei nicht existiert.
    /// Lädt auch languages.json für die Sprachnamen und scannt den Lang/ Ordner nach verfügbaren Sprachen.
    /// </summary>
    public static void LoadLanguage(string languageCode)
    {
        _currentLanguage = languageCode;
        var langDir = Path.Combine(AppContext.BaseDirectory, "Lang");
        if (!Directory.Exists(langDir))
            langDir = Path.Combine(Path.GetDirectoryName(typeof(Localization).Assembly.Location)!, "Lang");

        // Sprachnamen aus languages.json laden (Code → Nativer Name)
        LoadLanguageNames(langDir);

        // Verfügbare Sprachen scannen (alle *.json außer languages.json)
        AvailableLanguages = new List<string>();
        if (Directory.Exists(langDir))
        {
            foreach (var file in Directory.GetFiles(langDir, "*.json"))
            {
                var code = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (code != "languages")
                    AvailableLanguages.Add(code);
            }
        }
        if (AvailableLanguages.Count == 0)
            AvailableLanguages.Add("de");

        // Angeforderte Sprache laden, Fallback auf Deutsch
        var langFile = Path.Combine(langDir, $"{languageCode}.json");
        if (!File.Exists(langFile))
            langFile = Path.Combine(langDir, "de.json");

        if (File.Exists(langFile))
        {
            try
            {
                var json = File.ReadAllText(langFile);
                _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch
            {
                _strings = new();
            }
        }
    }

    /// <summary>
    /// Lädt die Sprachnamen aus languages.json (Code → Nativer Name).
    /// Fallback: leeres Dictionary, GetLanguageName() zeigt dann nur den Code in Großbuchstaben.
    /// </summary>
    private static void LoadLanguageNames(string langDir)
    {
        _languageNames = new Dictionary<string, string>();
        var namesFile = Path.Combine(langDir, "languages.json");
        if (File.Exists(namesFile))
        {
            try
            {
                var json = File.ReadAllText(namesFile);
                _languageNames = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch
            {
                _languageNames = new();
            }
        }
    }

    /// <summary>
    /// Holt einen lokalisierten String anhand des Keys. Fällt auf den Key selbst zurück, wenn nicht gefunden.
    /// </summary>
    public static string Get(string key)
    {
        return _strings.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>
    /// Holt einen lokalisierten String mit Format-Argumenten. Z.B. Get("update_failed", ex.Message)
    /// </summary>
    public static string Get(string key, params object[] args)
    {
        var template = Get(key);
        try { return string.Format(template, args); }
        catch { return template; }
    }

    /// <summary>
    /// Aktueller Sprachcode
    /// </summary>
    public static string CurrentLanguage => _currentLanguage;
}
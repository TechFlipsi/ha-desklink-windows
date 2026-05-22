// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
using System;

namespace HaDeskLink;

/// <summary>
/// Protects against HA IP-bans by rate-limiting authentication attempts.
/// Rules: Max 3 soft, Max 5 hard, exponential backoff 5s→30s→120s→300s.
/// </summary>
public class AuthGuard
{
    private int _failedAttempts;
    private DateTime _blockedUntil = DateTime.MinValue;
    private bool _hardBlocked;
    private string _lastError = "";

    public const int MaxSoftAttempts = 3;
    public const int MaxHardAttempts = 5;

    public int CurrentBackoffSeconds => _failedAttempts switch
    {
        0 => 0, 1 => 5, 2 => 30, 3 => 120, _ => 300
    };

    public bool IsBlocked
    {
        get
        {
            if (_hardBlocked) return true;
            if (_failedAttempts >= MaxSoftAttempts && DateTime.UtcNow < _blockedUntil) return true;
            return false;
        }
    }

    public bool IsHardBlocked => _hardBlocked;

    public string BlockMessage
    {
        get
        {
            if (_hardBlocked)
                return "⚠️ Authentifizierung blockiert — zu viele fehlgeschlagene Versuche.\n\n" +
                       $"Letzter Fehler: {_lastError}\n\n" +
                       "Aus Sicherheitsgründen (HA IP-Ban-Schutz) wurden die Login-Versuche gestoppt.\n" +
                       "Bitte überprüfe deinen Token und starte HA DeskLink neu.";

            if (_failedAttempts >= MaxSoftAttempts)
            {
                var remaining = (_blockedUntil - DateTime.UtcNow);
                if (remaining > TimeSpan.Zero)
                    return $"⚠️ Zu viele Login-Versuche — warte vor erneutem Versuch.\n\n" +
                           $"Letzter Fehler: {_lastError}\n\nDies schützt vor HA IP-Bans.";
            }

            return $"⚠️ Authentifizierung fehlgeschlagen ({_failedAttempts}/{MaxHardAttempts}).\n{_lastError}";
        }
    }

    public void RecordFailure(string error)
    {
        _failedAttempts++;
        _lastError = error;
        if (_failedAttempts >= MaxHardAttempts) { _hardBlocked = true; _blockedUntil = DateTime.MaxValue; }
        else if (_failedAttempts >= MaxSoftAttempts) { _blockedUntil = DateTime.UtcNow.AddSeconds(CurrentBackoffSeconds); }
    }

    public void RecordSuccess() { _failedAttempts = 0; _hardBlocked = false; _blockedUntil = DateTime.MinValue; _lastError = ""; }
    public void Reset() { _failedAttempts = 0; _hardBlocked = false; _blockedUntil = DateTime.MinValue; _lastError = ""; }

    public static bool ValidateTokenFormat(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        if (token.Length < 20) return false;
        if (token.Contains(' ') && !token.StartsWith("ey")) return false;
        return true;
    }
}

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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HaDeskLink;

/// <summary>
/// Execute system commands received from Home Assistant notifications.
/// </summary>
public static class CommandHandler
{
    public static void Execute(string command)
    {
        System.Diagnostics.Debug.WriteLine($"[HA DeskLink] Command received: {command}");
        switch (command.ToLowerInvariant())
        {
            case "shutdown":
                Process.Start("shutdown", "/s /t 30 /c \"HA DeskLink: PC wird heruntergefahren\"");
                break;
            case "restart":
            case "reboot":
                Process.Start("shutdown", "/r /t 30 /c \"HA DeskLink: PC wird neu gestartet\"");
                break;
            case "hibernate":
                SetSuspendState(true, false, false);
                break;
            case "sleep":
                SetSuspendState(false, false, false);
                break;
            case "lock":
            case "lock_screen":
                LockWorkStation();
                break;
            case "mute":
            case "volume_mute":
                ToggleMute();
                break;
            case "volume_up":
                VolumeUp();
                break;
            case "volume_down":
                VolumeDown();
                break;
            case "media_play_pause":
                MediaPlayPause();
                break;
            case "media_next":
                MediaNext();
                break;
            case "media_previous":
                MediaPrevious();
                break;
            case "monitor_off":
                MonitorOff();
                break;
            case "monitor_on":
                MonitorOn();
                break;
            case "screenshot":
                TakeScreenshot();
                break;
            case "screenshot_save":
                TakeAndSaveScreenshot();
                break;
            case "snipping_tool":
                OpenSnippingTool();
                break;
            case "brightness_up":
                BrightnessUp();
                break;
            case "brightness_down":
                BrightnessDown();
                break;
            default:
                // TTS (Text-to-Speech): "tts:Hallo Welt"
                if (command.StartsWith("tts:", StringComparison.OrdinalIgnoreCase))
                {
                    var text = command.Substring(4);
                    SpeakText(text);
                }
                // App Launcher: "launch:spotify"
                else if (command.StartsWith("launch:", StringComparison.OrdinalIgnoreCase))
                {
                    var appCommand = command.Substring(7).Trim();
                    LaunchApp(appCommand);
                }
                // Check for brightness value command: "brightness:50"
                else if (command.StartsWith("brightness:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(command.Substring("brightness:".Length), out int value))
                        SensorManager.SetBrightness(Math.Clamp(value, 0, 100));
                    else
                        throw new NotSupportedException($"Invalid brightness value: {command}");
                }
                // Custom Commands: prüfe ob der Command in der CustomCommands-Liste ist
                else if (TryExecuteCustomCommand(command))
                {
                    // Wurde bereits in TryExecuteCustomCommand ausgeführt
                }
                else
                    throw new NotSupportedException($"{Localization.Get("command_unknown", command)}");
                break;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    // Volume control via key simulation
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    private const byte VK_VOLUME_MUTE = 0xAD;
    private const byte VK_VOLUME_UP = 0xAF;
    private const byte VK_VOLUME_DOWN = 0xAE;
    private const byte VK_BRIGHTNESS_UP = 0x6F;   // Monitor brightness up
    private const byte VK_BRIGHTNESS_DOWN = 0x6E;  // Monitor brightness down
    private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    private const byte VK_MEDIA_PREV_TRACK = 0xB1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private static void ToggleMute()
    {
        keybd_event(VK_VOLUME_MUTE, 0, 0, 0);
        keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, 0);
    }

    private static void VolumeUp()
    {
        for (int i = 0; i < 5; i++) // 5 presses = ~10% increase
        {
            keybd_event(VK_VOLUME_UP, 0, 0, 0);
            keybd_event(VK_VOLUME_UP, 0, KEYEVENTF_KEYUP, 0);
        }
    }

    private static void VolumeDown()
    {
        for (int i = 0; i < 5; i++)
        {
            keybd_event(VK_VOLUME_DOWN, 0, 0, 0);
            keybd_event(VK_VOLUME_DOWN, 0, KEYEVENTF_KEYUP, 0);
        }
    }

    private static void MediaPlayPause()
    {
        keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 0, 0);
        keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_KEYUP, 0);
    }

    private static void MediaNext()
    {
        keybd_event(VK_MEDIA_NEXT_TRACK, 0, 0, 0);
        keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_KEYUP, 0);
    }

    private static void MediaPrevious()
    {
        keybd_event(VK_MEDIA_PREV_TRACK, 0, 0, 0);
        keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_KEYUP, 0);
    }

    private static void BrightnessUp()
    {
        for (int i = 0; i < 5; i++)
        {
            keybd_event(VK_BRIGHTNESS_UP, 0, 0, 0);
            keybd_event(VK_BRIGHTNESS_UP, 0, KEYEVENTF_KEYUP, 0);
        }
    }

    private static void BrightnessDown()
    {
        for (int i = 0; i < 5; i++)
        {
            keybd_event(VK_BRIGHTNESS_DOWN, 0, 0, 0);
            keybd_event(VK_BRIGHTNESS_DOWN, 0, KEYEVENTF_KEYUP, 0);
        }
    }

    // Monitor control
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_SYSCOMMAND = 0x0112;
    private readonly static IntPtr SC_MONITORPOWER = (IntPtr)0xF170;
    private readonly static IntPtr HWND_BROADCAST = (IntPtr)0xFFFF;

    private static void MonitorOff()
    {
        SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, (IntPtr)2);
    }

    private static void MonitorOn()
    {
        SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, (IntPtr)(-1));
        // Also move mouse to wake monitor
        keybd_event(0, 0, 0, 0);
    }

    private static void TakeScreenshot()
    {
        // Save screenshot to temp and upload to HA
        TakeAndSaveScreenshot();
    }

    /// <summary>
    /// Take a real screenshot using Graphics.CopyFromScreen,
    /// save as PNG, and fire HA event with base64 image data.
    /// </summary>
    private static void TakeAndSaveScreenshot()
    {
        try
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            if (screen == null) return;

            var bounds = screen.Bounds;
            using var bmp = new System.Drawing.Bitmap(bounds.Width, bounds.Height);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, System.Drawing.CopyPixelOperation.SourceCopy);

            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HA_DeskLink");
            System.IO.Directory.CreateDirectory(tempPath);
            var filePath = System.IO.Path.Combine(tempPath, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

            // Fire event to upload to HA asynchronously, then clean up temp file
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var app = DeskLinkApp.Instance;
                    if (app != null)
                    {
                        await app.UploadScreenshotAsync(filePath);
                    }
                }
                catch { }
                finally
                {
                    try { if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath); } catch { }
                }
            });
        }
        catch { }
    }

    private static void OpenSnippingTool()
    {
        // Use built-in Windows screenshot (Win+Shift+S)
        keybd_event(0x5B, 0, 0, 0); // Win down
        keybd_event(0x10, 0, 0, 0); // Shift down
        keybd_event(0x53, 0, 0, 0); // S down
        keybd_event(0x53, 0, KEYEVENTF_KEYUP, 0); // S up
        keybd_event(0x10, 0, KEYEVENTF_KEYUP, 0); // Shift up
        keybd_event(0x5B, 0, KEYEVENTF_KEYUP, 0); // Win up
    }

    // ─────────────────────────────────────────────────────────────────
    //  TTS (Text-to-Speech) — Windows
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spricht Text über Windows SAPI (System.Speech) via PowerShell.
    /// Der Text wird sicher escaped um Command-Injection zu verhindern.
    /// </summary>
    private static void SpeakText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Sichere Escaping: nur single quotes escapen, dann in single quotes wickeln
        // Verhindert Command-Injection durch den TTS-Text
        var safeText = text.Replace("'", "''");

        try
        {
            // PowerShell mit System.Speech Assembly — Verfügbar ab Windows 10
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"Add-Type -AssemblyName System.Speech; (New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak('{safeText}')\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Fehler: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  App Launcher — Windows
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Startet eine App anhand des konfigurierten AppLauncher-Commands.
    /// Sucht in AppLaunchers Config nach dem command und startet den Pfad.
    /// </summary>
    private static void LaunchApp(string appCommand)
    {
        if (string.IsNullOrWhiteSpace(appCommand)) return;

        try
        {
            var config = Config.Load();
            var launchers = JsonSerializer.Deserialize<List<AppLauncherEntry>>(config.AppLaunchers);
            if (launchers == null) return;

            var entry = launchers.Find(l =>
                string.Equals(l.Command, appCommand, StringComparison.OrdinalIgnoreCase));
            if (entry == null || string.IsNullOrEmpty(entry.Path))
                throw new NotSupportedException($"App Launcher '{appCommand}' nicht gefunden");

            // Windows: direkter Process.Start mit UseShellExecute für Apps
            Process.Start(new ProcessStartInfo
            {
                FileName = entry.Path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppLauncher] Fehler: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Custom Commands — Windows
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Prüft ob der Command in der CustomCommands-Liste der Config ist.
    /// Wenn ja, wird das konfigurierte Skript ausgeführt.
    /// </summary>
    /// <returns>true wenn der Command gefunden und ausgeführt wurde</returns>
    private static bool TryExecuteCustomCommand(string command)
    {
        try
        {
            var config = Config.Load();
            var customCommands = JsonSerializer.Deserialize<List<CustomCommandEntry>>(config.CustomCommands);
            if (customCommands == null || customCommands.Count == 0) return false;

            var entry = customCommands.Find(c =>
                string.Equals(c.Command, command, StringComparison.OrdinalIgnoreCase));
            if (entry == null || string.IsNullOrEmpty(entry.Script)) return false;

            // Windows: cmd /c script
            Process.Start("cmd", "/c " + entry.Script);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  JSON Modelle für Custom Commands und App Launchers
    // ─────────────────────────────────────────────────────────────────

    private class CustomCommandEntry
    {
        public string Command { get; set; } = "";
        public string Script { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private class AppLauncherEntry
    {
        public string Command { get; set; } = "";
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
    }
}
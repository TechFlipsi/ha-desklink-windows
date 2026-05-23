
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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HaDeskLink;

/// <summary>
/// Shared model for now-playing media state across all platforms.
/// </summary>
public class MediaState
{
    public string State { get; set; } = "idle";  // idle, playing, paused
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Source { get; set; }           // App name (Spotify, Chrome, etc.)
    public int? Volume { get; set; }              // 0-100
    public bool? Muted { get; set; }
}

/// <summary>
/// Detects now-playing media on Windows using the
/// GlobalSystemMediaTransportControlsSessionManager API.
/// Falls back to subprocess calls if the COM API is unavailable.
/// </summary>
public class MediaPlayer : IDisposable
{
    private bool _disposed;

    public MediaPlayer()
    {
    }

    /// <summary>
    /// Get the current media playback state from Windows.
    /// Uses GSMT Session Manager when available (Windows 10+ build 18362+),
    /// with PowerShell fallback.
    /// </summary>
    public MediaState GetCurrentMediaState()
    {
        try
        {
            var state = GetMediaStateViaComApi();
            if (state != null)
                return state;
        }
        catch
        {
            // COM API unavailable — try fallbacks
        }

        try
        {
            var state = GetMediaStateViaProcess();
            if (state != null)
                return state;
        }
        catch
        {
            // All methods failed
        }

        return new MediaState { State = "idle" };
    }

    /// <summary>
    /// Primary method: use Windows GSMT session manager COM API.
    /// </summary>
    private static MediaState? GetMediaStateViaComApi()
    {
        try
        {
            var manager = GetSessionManager();
            if (manager == null)
                return null;

            var sessionPtr = IntPtr.Zero;
            try
            {
                var hr = manager.GetCurrentSession(out sessionPtr);
                if (hr != 0 || sessionPtr == IntPtr.Zero)
                    return null;

                var state = new MediaState();

                // Get source app name
                try
                {
                    var sourceAppPtr = IntPtr.Zero;
                    var getSourceHr = manager.GetSessionSourceAppUserModelId(sessionPtr, out sourceAppPtr);
                    if (getSourceHr == 0 && sourceAppPtr != IntPtr.Zero)
                    {
                        var appId = Marshal.PtrToStringUni(sourceAppPtr);
                        Marshal.FreeCoTaskMem(sourceAppPtr);
                        if (!string.IsNullOrWhiteSpace(appId))
                        {
                            // Convert UWP-style AUMID to friendly name
                            state.Source = FriendlyAppName(appId);
                        }
                    }
                }
                catch { }

                // Get playback info
                try
                {
                    var playbackInfoPtr = IntPtr.Zero;
                    hr = manager.GetMediaPlaybackInfo(sessionPtr, out playbackInfoPtr);
                    if (hr == 0 && playbackInfoPtr != IntPtr.Zero)
                    {
                        var info = Marshal.PtrToStructure<GSMTCPlaybackInfo>(playbackInfoPtr);
                        Marshal.FreeCoTaskMem(playbackInfoPtr);

                        state.State = info.PlaybackStatus switch
                        {
                            0 => "idle",    // Closed
                            1 => "idle",    // Changed
                            2 => "paused",   // Paused
                            3 => "playing",  // Playing
                            4 => "paused",  // Paused (opened)
                            _ => "idle"
                        };
                    }
                }
                catch { }

                // Get media properties (title, artist, album)
                try
                {
                    var mediaInfoPtr = IntPtr.Zero;
                    hr = manager.TryGetMediaPropertiesAsync(sessionPtr, out mediaInfoPtr);
                    if (hr == 0 && mediaInfoPtr != IntPtr.Zero)
                    {
                        var info = Marshal.PtrToStructure<GSMTCMediaPropertiesInfo>(mediaInfoPtr);
                        Marshal.FreeCoTaskMem(mediaInfoPtr);

                        // Read strings from pointers
                        state.Title = ReadStringFromIntPtr(info.TitlePtr);
                        state.Artist = ReadStringFromIntPtr(info.ArtistPtr);
                        state.Album = ReadStringFromIntPtr(info.AlbumTitlePtr);
                    }
                }
                catch { }

                return state;
            }
            finally
            {
                if (sessionPtr != IntPtr.Zero)
                    Marshal.Release(sessionPtr);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fallback: PowerShell query for now-playing info.
    /// Uses the Windows.Media.Control API class via PowerShell.
    /// </summary>
    private static MediaState? GetMediaStateViaProcess()
    {
        try
        {
            // Try using a PowerShell script that accesses the Windows Runtime
            var psScript = @"
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | ? { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($WinRtTask, $ResultType) {
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}
[Windows.System.User,Windows.System,ContentType=WindowsRuntime] | Out-Null
[Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager,Windows.Media.Control,ContentType=WindowsRuntime] | Out-Null
$mgr = Await ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]::RequestAsync()) ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager])
if ($mgr) {
    $session = $mgr.GetCurrentSession()
    if ($session) {
        $info = Await ($session.TryGetMediaPropertiesAsync()) ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties])
        $playback = $session.GetPlaybackInfo()
        $result = @{
            Title = $info.Title
            Artist = $info.Artist
            Album = $info.AlbumTitle
            Status = $playback.PlaybackStatus.ToString()
            Source = $session.SourceAppUserModelId
        }
        ConvertTo-Json $result
    }
}
";
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "\\\"")}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();
            proc.WaitForExit(5000);

            if (string.IsNullOrWhiteSpace(output))
                return null;

            // Parse JSON output
            return ParsePowerShellJson(output);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse the PowerShell JSON output into a MediaState object.
    /// </summary>
    private static MediaState? ParsePowerShellJson(string json)
    {
        try
        {
            // Simple manual JSON parsing to avoid dependency on System.Text.Json in all scenarios
            var state = new MediaState();

            state.Title = ExtractJsonString(json, "Title");
            state.Artist = ExtractJsonString(json, "Artist");
            state.Album = ExtractJsonString(json, "Album");
            state.Source = ExtractJsonString(json, "Source");

            var statusStr = ExtractJsonString(json, "Status");
            if (!string.IsNullOrEmpty(statusStr))
            {
                state.State = statusStr.ToLowerInvariant() switch
                {
                    "playing" => "playing",
                    "paused" => "paused",
                    "stopped" => "idle",
                    "closed" => "idle",
                    _ => "idle"
                };
            }

            // If we got a title, set state to at least idle (not null)
            if (!string.IsNullOrEmpty(state.Title) && state.State == "idle")
                state.State = "playing";

            return state;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract a string value from a simple JSON object.
    /// </summary>
    private static string? ExtractJsonString(string json, string key)
    {
        try
        {
            var search = $"\"{key}\"";
            var keyIdx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (keyIdx < 0) return null;

            var colonIdx = json.IndexOf(':', keyIdx + search.Length);
            if (colonIdx < 0) return null;

            var valueStart = json.IndexOf('"', colonIdx);
            if (valueStart < 0) return null;

            var valueEnd = json.IndexOf('"', valueStart + 1);
            if (valueEnd < 0) return null;

            var value = json.Substring(valueStart + 1, valueEnd - valueStart - 1);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Convert AUMID to a friendly app name.
    /// </summary>
    private static string FriendlyAppName(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return "Unknown";

        // Known app mappings
        if (appId.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        if (appId.Contains("ZuneMusic", StringComparison.OrdinalIgnoreCase)) return "Media Player";
        if (appId.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (appId.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        if (appId.Contains("Edge", StringComparison.OrdinalIgnoreCase)) return "Edge";
        if (appId.Contains("VLC", StringComparison.OrdinalIgnoreCase)) return "VLC";
        if (appId.Contains("Winamp", StringComparison.OrdinalIgnoreCase)) return "Winamp";
        if (appId.Contains("iTunes", StringComparison.OrdinalIgnoreCase)) return "iTunes";
        if (appId.Contains("Foobar", StringComparison.OrdinalIgnoreCase)) return "foobar2000";

        // Extract the last part of the AUMID (e.g., "Microsoft.ZuneMusic_..." -> "Microsoft.ZuneMusic")
        var dotIdx = appId.IndexOf('.');
        if (dotIdx >= 0)
        {
            var endIdx = appId.IndexOf('_', dotIdx);
            if (endIdx < 0) endIdx = appId.Length;
            return appId.Substring(0, endIdx);
        }

        return appId;
    }

    /// <summary>
    /// Safely read a string from an IntPtr pointer.
    /// </summary>
    private static string? ReadStringFromIntPtr(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        try
        {
            var str = Marshal.PtrToStringUni(ptr);
            return string.IsNullOrWhiteSpace(str) ? null : str;
        }
        catch
        {
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  COM interop definitions for GSMTC session manager
    // ─────────────────────────────────────────────────────────────────

    [ComImport, Guid("8E1B1EE1-A5FA-4D27-A8A6-7C39DD9B3E60")]
    private class GlobalSystemMediaTransportControlsSessionManager { }

    [Guid("0F6C8A71-7DB8-49BE-BE9E-27F9A3C0F9C0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    private interface IGlobalSystemMediaTransportControlsSessionManager
    {
        [PreserveSig]
        int GetCurrentSession(out IntPtr session);

        [PreserveSig]
        int GetSessions(out IntPtr sessions);

        [PreserveSig]
        int GetSessionSourceAppUserModelId(
            [In] IntPtr session,
            out IntPtr sourceAppUserModelId);

        [PreserveSig]
        int GetMediaPlaybackInfo(
            [In] IntPtr session,
            out IntPtr playbackInfo);

        [PreserveSig]
        int TryGetMediaPropertiesAsync(
            [In] IntPtr session,
            out IntPtr mediaPropertiesOp);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GSMTCPlaybackInfo
    {
        public int PlaybackStatus;          // 0=Closed, 1=Changed, 2=Paused, 3=Playing, 4=Opened
        public int PlaybackType;            // 0=Unknown, 1=Music, 2=Video, 3=Image
        public IntPtr ControlsPtr;
        public IntPtr TimelinePropertiesPtr;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GSMTCMediaPropertiesInfo
    {
        public IntPtr TitlePtr;
        public IntPtr SubtitlePtr;
        public IntPtr ArtistPtr;
        public IntPtr AlbumArtistPtr;
        public IntPtr AlbumTitlePtr;
        public IntPtr TrackNumberPtr;
        public IntPtr GenresPtr;
        public IntPtr PlaybackTypePtr;
        public IntPtr AlbumTrackCountPtr;
        public IntPtr ThumbnailPtr;
    }

    /// <summary>
    /// Helper to get GSMTC session manager via COM.
    /// </summary>
    private static IGlobalSystemMediaTransportControlsSessionManager? GetSessionManager()
    {
        try
        {
            var obj = (IGlobalSystemMediaTransportControlsSessionManager)
                new GlobalSystemMediaTransportControlsSessionManager();
            return obj;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

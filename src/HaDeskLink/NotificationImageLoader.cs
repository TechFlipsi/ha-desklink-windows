// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
// Downloads notification images (HA companion `image` key) with auth for relative URLs.
#nullable enable
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace HaDeskLink;

/// <summary>
/// Resolves and downloads images referenced in HA notifications (companion-style
/// `image` key). Supports absolute URLs and HA-relative paths
/// (/media/local/..., /api/camera_proxy/...) which are resolved against the
/// configured HA URL and authenticated with the long-lived access token.
/// Downloads are cached in %LocalAppData%\HaDeskLink\cache\notifications\.
/// </summary>
public static class NotificationImageLoader
{
    private static readonly HttpClient _plainHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const long MaxImageBytes = 10 * 1024 * 1024; // 10 MB (HA companion limit for images)

    /// <summary>Result of an image load attempt.</summary>
    public sealed class ImageResult
    {
        public string? LocalPath { get; init; }
        public string? Error { get; init; }
        public bool FromCache { get; init; }
    }

    /// <summary>
    /// Loads the image referenced by a notification. Relative paths are resolved
    /// against HA and requested with the long-lived token; absolute http(s) URLs
    /// are fetched without auth (external camera URLs etc.).
    /// </summary>
    public static ImageResult Load(string imageRef, string haUrl, string haToken)
    {
        if (string.IsNullOrWhiteSpace(imageRef))
            return new ImageResult { Error = "empty reference" };

        try
        {
            bool isAbsolute = imageRef.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                           || imageRef.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            string url;
            bool needsAuth;
            if (isAbsolute)
            {
                url = imageRef;
                needsAuth = false;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(haUrl))
                    return new ImageResult { Error = "relative path but no HA URL configured" };
                var baseUri = haUrl.TrimEnd('/');
                url = imageRef.StartsWith('/') ? baseUri + imageRef : baseUri + "/" + imageRef;
                needsAuth = true;
            }

            // Extension check (HA companion rule: filetype from extension)
            var ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
            if (!string.IsNullOrEmpty(ext) && Array.IndexOf(AllowedExtensions, ext) < 0)
                return new ImageResult { Error = $"unsupported image type: {ext}" };

            // Cache lookup (keyed by URL hash so camera proxies update correctly)
            var cacheDir = GetCacheDir();
            var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..24];
            var cached = Directory.GetFiles(cacheDir, cacheKey + ".*");
            if (cached.Length > 0)
            {
                var fi = new FileInfo(cached[0]);
                // Cache entry valid for 60s (camera snapshots should refresh)
                if (fi.LastWriteTimeUtc > DateTime.UtcNow - TimeSpan.FromSeconds(60))
                    return new ImageResult { LocalPath = cached[0], FromCache = true };
            }

            // Download
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (needsAuth && !string.IsNullOrWhiteSpace(haToken))
                request.Headers.Add("Authorization", $"Bearer {haToken}");
            // Browser-ish UA: some reverse proxies block empty UAs
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; HaDeskLink)");

            HttpResponseMessage resp;
            if (needsAuth)
            {
                // Auth via a client that tolerates self-signed certs on LAN (same as HaApiClient)
                using var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };
                using var authClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                using var authResp = authClient.SendAsync(request).GetAwaiter().GetResult();
                if (!authResp.IsSuccessStatusCode)
                    return new ImageResult { Error = $"HTTP {(int)authResp.StatusCode}" };
                using var authStream = authResp.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                return SaveToCache(authStream, cacheDir, cacheKey);
            }
            else
            {
                using var plainResp = _plainHttp.SendAsync(request).GetAwaiter().GetResult();
                if (!plainResp.IsSuccessStatusCode)
                    return new ImageResult { Error = $"HTTP {(int)plainResp.StatusCode}" };
                using var plainStream = plainResp.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                return SaveToCache(plainStream, cacheDir, cacheKey);
            }
        }
        catch (Exception ex)
        {
            return new ImageResult { Error = ex.Message };
        }
    }

    private static ImageResult SaveToCache(Stream stream, string cacheDir, string cacheKey)
    {
        Directory.CreateDirectory(cacheDir);
        var tmpPath = Path.Combine(cacheDir, cacheKey + ".tmp");
        long total = 0;
        using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > MaxImageBytes)
                {
                    TryDelete(tmpPath);
                    return new ImageResult { Error = $"image too large (> {MaxImageBytes / 1024 / 1024} MB)" };
                }
                fs.Write(buffer, 0, read);
            }
        }

        // Sniff actual extension from magic bytes (camera proxies often lack extensions)
        var sniffed = SniffExtension(tmpPath);
        if (sniffed == null)
        {
            TryDelete(tmpPath);
            return new ImageResult { Error = "unsupported image format (no JPEG/PNG/GIF/WebP magic)" };
        }

        var finalPath = Path.Combine(cacheDir, cacheKey + sniffed);
        if (File.Exists(finalPath))
            File.Delete(finalPath);
        File.Move(tmpPath, finalPath);
        return new ImageResult { LocalPath = finalPath };
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception) { /* non-fatal */ }
    }

    private static string? SniffExtension(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            Span<byte> magic = stackalloc byte[12];
            var n = fs.Read(magic);
            if (n >= 3 && magic[0] == 0xFF && magic[1] == 0xD8 && magic[2] == 0xFF) return ".jpg";
            if (n >= 4 && magic[0] == 0x89 && magic[1] == 0x50 && magic[2] == 0x4E && magic[3] == 0x47) return ".png";
            if (n >= 3 && magic[0] == 'G' && magic[1] == 'I' && magic[2] == 'F') return ".gif";
            if (n >= 12 && magic[0] == 'R' && magic[1] == 'I' && magic[2] == 'F' && magic[3] == 'F'
                       && magic[8] == 'W' && magic[9] == 'E' && magic[10] == 'B' && magic[11] == 'P') return ".webp";
        }
        catch (Exception) { }
        return null;
    }

    private static string GetCacheDir()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "HaDeskLink", "cache", "notifications");
    }

    /// <summary>Removes cached images older than 7 days. Called once per app start.</summary>
    public static void CleanupCache()
    {
        try
        {
            var dir = GetCacheDir();
            if (!Directory.Exists(dir))
                return;
            var cutoff = DateTime.UtcNow - TimeSpan.FromDays(7);
            foreach (var f in Directory.GetFiles(dir))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(f) < cutoff)
                        File.Delete(f);
                }
                catch (Exception) { /* individual file cleanup failure is non-fatal */ }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[NotificationImage] Cache cleanup failed: {ex.Message}"); }
    }
}
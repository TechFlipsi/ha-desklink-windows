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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;

namespace HaDeskLink;

/// <summary>
/// Result of MQTT auto-configuration attempt.
/// </summary>
public class MqttSetupResult
{
    public bool Success { get; set; }
    public string? BrokerHost { get; set; }
    public int BrokerPort { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; }
    public bool MosquittoNotInstalled { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Auto-configures MQTT by querying the Home Assistant REST API for
/// the MQTT broker details, then testing the connection.
/// 
/// Strategy (fixed):
/// 1. Check if MQTT integration exists in HA (via /api/config -> components).
///    This is the AUTHORITY — "mqtt" in components means MQTT IS installed.
/// 2. Try to connect with multiple strategies (anonymous, HA token).
/// 3. Multi-address fallback for domain names and local addresses.
/// 4. Never reports MQTT as "not installed" when /api/config shows it.
/// </summary>
public static class MqttSetupHelper
{
    /// <summary>
    /// Query the HA REST API for MQTT integration details and try to
    /// establish a working MQTT connection. Uses the configured HA URL
    /// (works with IP addresses AND domain names like home.kirchweger.de).
    /// </summary>
    public static async Task<MqttSetupResult> AutoConfigureAsync(string haUrl, string haToken, string? fallbackHost = null)
    {
        var result = new MqttSetupResult();

        try
        {
            // ── Step 1: extract the host from the configured HA URL ──
            var haUri = new Uri(haUrl.TrimEnd('/'));
            var haHost = haUri.Host;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {haToken}");

            // ── Step 2: check if MQTT integration exists in HA ──
            // THIS IS THE AUTHORITATIVE CHECK. "mqtt" in components = installed.
            bool mqttInstalled = false;
            bool apiReachable = false;

            try
            {
                var configResp = await http.GetStringAsync($"{haUrl.TrimEnd('/')}/api/config");
                apiReachable = true;
                using var configDoc = JsonDocument.Parse(configResp);

                if (configDoc.RootElement.TryGetProperty("components", out var components)
                    && components.ValueKind == JsonValueKind.Array)
                {
                    foreach (var comp in components.EnumerateArray())
                    {
                        if (comp.GetString() == "mqtt")
                        {
                            mqttInstalled = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // If we can't reach the API, we can't determine if MQTT is installed.
                // We'll still try to connect and give useful feedback.
            }

            // ── Step 3: if MQTT integration is NOT in HA components, stop here ──
            if (apiReachable && !mqttInstalled)
            {
                result.MosquittoNotInstalled = true;
                result.ErrorMessage = "MQTT Integration nicht in Home Assistant gefunden. Bitte MQTT in HA aktivieren.";
                return result;
            }

            // ── Step 4: try to extract broker host from config entries (best-effort) ──
            string? apiBrokerHost = null;
            int brokerPort = 1883;
            bool brokerSsl = false;

            try
            {
                var entriesResp = await http.GetStringAsync($"{haUrl.TrimEnd('/')}/api/config/config_entries/entry");
                using var entriesDoc = JsonDocument.Parse(entriesResp);

                if (entriesDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in entriesDoc.RootElement.EnumerateArray())
                    {
                        var domain = entry.TryGetProperty("domain", out var d) ? d.GetString() : null;
                        if (domain != "mqtt") continue;

                        if (entry.TryGetProperty("data", out var data))
                        {
                            if (data.TryGetProperty("broker", out var b))
                                apiBrokerHost = b.GetString();
                            if (data.TryGetProperty("port", out var p) && p.TryGetInt32(out var portVal))
                                brokerPort = portVal;
                            if (data.TryGetProperty("ssl", out var ssl) && ssl.ValueKind == JsonValueKind.True)
                                brokerSsl = true;
                        }
                        break;
                    }
                }
            }
            catch
            {
                // Config entries API may fail — that's fine, we fall back to HA host
            }

            // If API didn't reveal host, use HA host
            var primaryHost = apiBrokerHost ?? haHost;
            if (brokerPort <= 0)
                brokerPort = brokerSsl ? 8883 : 1883;

            // ── Step 5: build list of broker addresses to try ──
            var brokerAddresses = BuildAddressList(primaryHost, haHost, fallbackHost);

            result.BrokerPort = brokerPort;
            result.UseSsl = brokerSsl;

            // ── Step 6: try to connect with multiple strategies across all addresses ──
            // Strategy A: Anonymous connection (most Mosquitto add-on setups allow this)
            foreach (var address in brokerAddresses)
            {
                if (await TestConnectionAsync(address, brokerPort, null, null, brokerSsl))
                {
                    result.Success = true;
                    result.BrokerHost = address;
                    result.Username = null;
                    result.Password = null;
                    return result;
                }
            }

            // Strategy B: HA long-lived access token as MQTT password
            if (!string.IsNullOrEmpty(haToken))
            {
                foreach (var address in brokerAddresses)
                {
                    if (await TestConnectionAsync(address, brokerPort, "homeassistant", haToken, brokerSsl))
                    {
                        result.Success = true;
                        result.BrokerHost = address;
                        result.Username = "homeassistant";
                        result.Password = haToken;
                        return result;
                    }
                }
            }

            // ── Step 7: all connection attempts failed ──
            // MQTT is installed (we confirmed via /api/config), but we can't connect.
            // This is an AUTHENTICATION / REACHABILITY problem, NOT an installation problem.
            result.MosquittoNotInstalled = false; // MQTT IS installed — we confirmed it
            result.BrokerHost = primaryHost;
            result.ErrorMessage = $"MQTT Broker gefunden unter {primaryHost}:{brokerPort}, aber Authentifizierung fehlgeschlagen. Bitte manuell konfigurieren.";
        }
        catch (Exception ex)
        {
            result.MosquittoNotInstalled = true;
            result.ErrorMessage = $"Auto-configuration failed: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Build an ordered list of broker addresses to attempt.
    /// For domain names, tries the domain first then resolved IPs.
    /// For IPs, uses them directly. Also adds localhost as last resort.
    /// </summary>
    private static List<string> BuildAddressList(string primaryHost, string haHost, string? fallbackHost)
    {
        var addresses = new List<string>();

        // Primary host always first
        addresses.Add(primaryHost);

        // If primary is a domain and different from haHost, add haHost too
        if (primaryHost != haHost && !string.IsNullOrEmpty(haHost))
            addresses.Add(haHost);

        // If the host is a domain name, try to resolve it to IPs
        if (!IsIpAddress(primaryHost))
        {
            try
            {
                var ips = Dns.GetHostAddresses(primaryHost);
                foreach (var ip in ips)
                {
                    var ipStr = ip.ToString();
                    if (!addresses.Contains(ipStr))
                        addresses.Add(ipStr);
                }
            }
            catch
            {
                // DNS resolution failed — just continue with the domain
            }
        }

        // Add fallback host if provided and different
        if (!string.IsNullOrEmpty(fallbackHost) && !addresses.Contains(fallbackHost))
            addresses.Add(fallbackHost);

        // Add localhost/127.0.0.1 as last resort (only if not already localhost)
        if (!addresses.Contains("localhost") && !addresses.Contains("127.0.0.1"))
        {
            addresses.Add("localhost");
            addresses.Add("127.0.0.1");
        }

        return addresses;
    }

    /// <summary>
    /// Check if a host string is an IPv4 or IPv6 address.
    /// </summary>
    private static bool IsIpAddress(string host)
    {
        return IPAddress.TryParse(host, out _);
    }

    /// <summary>
    /// Test whether we can establish a TCP/TLS MQTT connection to the
    /// given broker. Returns true on success, false on any failure.
    /// </summary>
    public static async Task<bool> TestConnectionAsync(
        string broker, int port, string? username, string? password, bool useSsl)
    {
        try
        {
            var factory = new MqttFactory();
            using var mqtt = factory.CreateMqttClient();

            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(broker, port)
                .WithTimeout(TimeSpan.FromSeconds(5))
                .WithCleanSession();

            if (!string.IsNullOrEmpty(username))
                optionsBuilder = optionsBuilder.WithCredentials(username, password ?? "");

            if (useSsl)
                optionsBuilder = optionsBuilder.WithTlsOptions(o => o
                    .WithSslProtocols(System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13)
                    .WithCertificateValidationHandler(_ => true)); // accept self-signed certs

            var options = optionsBuilder.Build();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var connectResult = await mqtt.ConnectAsync(options, cts.Token);

            if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
            {
                // Clean disconnect.
                using var disconnectCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await mqtt.DisconnectAsync(new MqttClientDisconnectOptions(), disconnectCts.Token);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}

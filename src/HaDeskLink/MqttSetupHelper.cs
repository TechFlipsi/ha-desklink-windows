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
using System.Linq;
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
/// Strategy:
/// 1. Check if MQTT integration exists in HA (via /api/config -> components)
/// 2. Try to extract broker details from config entries API (credentials may be hidden)
/// 3. Use the HA host as broker host (Mosquitto add-on runs on same host)
/// 4. Try multiple connection strategies: discovered creds, anonymous, HA token
/// </summary>
public static class MqttSetupHelper
{
    /// <summary>
    /// Query the HA REST API for MQTT integration details and try to
    /// establish a working MQTT connection. Uses the configured HA URL
    /// (works with IP addresses AND domain names like home.kirchweger.de).
    /// </summary>
    public static async Task<MqttSetupResult> AutoConfigureAsync(string haUrl, string haToken)
    {
        var result = new MqttSetupResult();

        try
        {
            // ── Step 1: extract the host from the configured HA URL ──
            // This works with IPs (192.168.178.33) AND domains (home.kirchweger.de)
            var haUri = new Uri(haUrl.TrimEnd('/'));
            var haHost = haUri.Host;
            var haPort = haUri.Port; // -1 if not specified, else the actual port

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {haToken}");

            // ── Step 2: check if MQTT integration exists in HA ──
            bool mqttInstalled = false;
            string? brokerHost = null;
            int brokerPort = 0;
            string? brokerUser = null;
            string? brokerPass = null;
            bool brokerSsl = false;

            try
            {
                // Check /api/config -> components list for "mqtt"
                var configResp = await http.GetStringAsync($"{haUrl.TrimEnd('/')}/api/config");
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
                // If we can't reach the API, assume MQTT might be there and try anyway
                mqttInstalled = true; // give it a shot
            }

            // ── Step 3: try to extract broker details from config entries ──
            // HA protects the actual credentials, so we may get empty data here.
            // But sometimes the broker host is visible.
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

                        // Found the MQTT config entry
                        if (entry.TryGetProperty("title", out var title))
                        {
                            // "core-mosquitto" title means Mosquitto add-on
                            // This confirms the broker is on the same host
                        }

                        if (entry.TryGetProperty("data", out var data))
                        {
                            // Broker host might be available
                            if (data.TryGetProperty("broker", out var b))
                                brokerHost = b.GetString();
                            if (data.TryGetProperty("port", out var p) && p.TryGetInt32(out var portVal))
                                brokerPort = portVal;
                            if (data.TryGetProperty("username", out var u))
                                brokerUser = u.GetString();
                            if (data.TryGetProperty("password", out var pw))
                                brokerPass = pw.GetString();
                            if (data.TryGetProperty("ssl", out var ssl) && ssl.ValueKind == JsonValueKind.True)
                                brokerSsl = true;
                        }
                        break;
                    }
                }
            }
            catch
            {
                // Config entries API may not return credentials, that's fine
            }

            // ── Step 4: determine broker host ──
            // If the API didn't reveal the broker host, use the HA host.
            // For domain names (home.kirchweger.de), this means the MQTT broker
            // is accessible at the same domain. For IPs, same thing.
            if (string.IsNullOrEmpty(brokerHost))
            {
                brokerHost = haHost;
            }

            // Default port unless we found one from the API
            if (brokerPort <= 0)
            {
                // Check if HA is running on a custom port — Mosquitto is
                // always on 1883 (or 8883 for SSL) regardless of HA's port
                brokerPort = brokerSsl ? 8883 : 1883;
            }

            result.BrokerHost = brokerHost;
            result.BrokerPort = brokerPort;
            result.UseSsl = brokerSsl;

            // ── Step 5: check if MQTT is installed at all ──
            if (!mqttInstalled)
            {
                // Double-check by trying to connect anyway — the components
                // list might not include all loaded integrations
            }

            // ── Step 6: attempt to connect with multiple strategies ──

            // Strategy A: with discovered credentials (or anonymous if none found)
            if (await TestConnectionAsync(brokerHost, brokerPort, brokerUser, brokerPass, brokerSsl))
            {
                result.Success = true;
                result.Username = brokerUser;
                result.Password = brokerPass;
                return result;
            }

            // Strategy B: try anonymous access (Mosquitto add-on default allows
            // anonymous from local network)
            if (brokerUser != null || brokerPass != null)
            {
                if (await TestConnectionAsync(brokerHost, brokerPort, null, null, brokerSsl))
                {
                    result.Success = true;
                    result.Username = null;
                    result.Password = null;
                    return result;
                }
            }

            // Strategy C: try with the HA access token as MQTT password
            // Some HA setups accept this
            if (!string.IsNullOrEmpty(haToken))
            {
                if (await TestConnectionAsync(brokerHost, brokerPort, "homeassistant", haToken, brokerSsl))
                {
                    result.Success = true;
                    result.Username = "homeassistant";
                    result.Password = haToken;
                    return result;
                }
            }

            // Strategy D: if HA runs on a non-standard port, try localhost
            // (Mosquitto might only listen on localhost in some setups)
            if (brokerHost != "localhost" && brokerHost != "127.0.0.1")
            {
                if (await TestConnectionAsync("localhost", brokerPort, null, null, brokerSsl))
                {
                    result.Success = true;
                    result.BrokerHost = "localhost";
                    result.Username = null;
                    result.Password = null;
                    return result;
                }

                if (await TestConnectionAsync("127.0.0.1", brokerPort, null, null, brokerSsl))
                {
                    result.Success = true;
                    result.BrokerHost = "127.0.0.1";
                    result.Username = null;
                    result.Password = null;
                    return result;
                }
            }

            // ── Step 7: all attempts failed ──
            result.MosquittoNotInstalled = true;
            result.ErrorMessage = "Could not connect to the MQTT broker. " +
                "Please make sure the Mosquitto broker add-on is installed and running in Home Assistant " +
                "(Settings → Add-ons → Mosquitto Broker).";
        }
        catch (Exception ex)
        {
            result.MosquittoNotInstalled = true;
            result.ErrorMessage = $"Auto-configuration failed: {ex.Message}";
        }

        return result;
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
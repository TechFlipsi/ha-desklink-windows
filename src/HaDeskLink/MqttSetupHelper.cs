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
/// </summary>
public static class MqttSetupHelper
{
    /// <summary>
    /// Query the HA REST API for MQTT integration details and try to
    /// establish a working MQTT connection. Returns a result that can
    /// be used directly for persistent MQTT client setup.
    /// </summary>
    public static async Task<MqttSetupResult> AutoConfigureAsync(string haUrl, string haToken)
    {
        var result = new MqttSetupResult();

        try
        {
            // ── Step 1: extract the HA host from the URL ──
            var haUri = new Uri(haUrl.TrimEnd('/'));
            var haHost = haUri.Host;

            // Build an HttpClient with the Bearer token pre-set.
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {haToken}");

            // ── Step 2: fetch config entries, look for the MQTT integration ──
            string? brokerHost = null;
            int brokerPort = 0;
            string? brokerUser = null;
            string? brokerPass = null;
            bool brokerSsl = false;

            try
            {
                var configUrl = $"{haUrl}/api/config/config_entries/entry";
                var resp = await http.GetStringAsync(configUrl);
                using var doc = JsonDocument.Parse(resp);

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in doc.RootElement.EnumerateArray())
                    {
                        var domain = entry.TryGetProperty("domain", out var d) ? d.GetString() : null;
                        if (domain != "mqtt") continue;

                        // We found the MQTT integration – extract the connection details.
                        if (entry.TryGetProperty("data", out var data))
                        {
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
                // Fallback: the /api/config/config_entries/entry endpoint might not
                // exist on older HA versions — carry on with the heuristic below.
            }

            // ── Step 3: heuristic fallback ──
            // The MQTT broker (Mosquitto add-on) nearly always runs on the
            // same host as Home Assistant itself.
            if (string.IsNullOrEmpty(brokerHost))
            {
                brokerHost = haHost;
            }

            // Default port unless we found one from the API.
            if (brokerPort <= 0)
            {
                brokerPort = brokerSsl ? 8883 : 1883;
            }

            result.BrokerHost = brokerHost;
            result.BrokerPort = brokerPort;
            result.UseSsl = brokerSsl;

            // ── Step 4: attempt to connect ──

            // Strategy A: with discovered credentials (or anonymous if none)
            if (await TestConnectionAsync(brokerHost, brokerPort, brokerUser, brokerPass, brokerSsl))
            {
                result.Success = true;
                result.Username = brokerUser;
                result.Password = brokerPass;
                return result;
            }

            // Strategy B: try the HA access token as the MQTT password.
            // Some HA setups (especially supervised/core installs) accept this.
            if (!string.IsNullOrEmpty(haToken) && brokerUser != haToken)
            {
                if (await TestConnectionAsync(brokerHost, brokerPort, brokerUser, haToken, brokerSsl))
                {
                    result.Success = true;
                    result.Username = brokerUser;
                    result.Password = haToken;
                    return result;
                }
            }

            // Strategy C: anonymous fallback (in case credentials are wrong, but
            // the Mosquitto add-on allows anonymous connections from the local LAN).
            if (!string.IsNullOrEmpty(brokerUser) || !string.IsNullOrEmpty(brokerPass))
            {
                if (await TestConnectionAsync(brokerHost, brokerPort, null, null, brokerSsl))
                {
                    result.Success = true;
                    result.Username = null;
                    result.Password = null;
                    return result;
                }
            }

            // ── Step 5: all attempts failed ──
            result.MosquittoNotInstalled = true;
            result.ErrorMessage = "Could not connect to the MQTT broker. " +
                "Please make sure the Mosquitto broker add-on is installed and running in Home Assistant.";
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

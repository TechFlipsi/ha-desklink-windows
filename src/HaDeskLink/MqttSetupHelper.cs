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
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;

namespace HaDeskLink;

public static class MqttSetupHelper
{
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
                .WithClientId($"ha_desklink_test_{Guid.NewGuid():N}".Substring(0, 23))
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

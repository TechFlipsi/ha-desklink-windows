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
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace HaDeskLink;

/// <summary>
/// MQTT client for Home Assistant with auto-discovery, LWT,
/// sensor publishing, media player control, and command routing.
/// Follows the HA MQTT Discovery specification exactly.
/// </summary>
public class MqttClient : IDisposable
{
    #region Constants

    private const string DiscoveryPrefix = "homeassistant";
    private const string TopicPrefix = "ha_desklink";
    private const string AppManufacturer = "HA DeskLink";
    private const string AppDisplayName = "HA DeskLink";
    private const string AppModel = "PC";
    private const string PayloadOnline = "online";
    private const string PayloadOffline = "offline";
    private const int MaxFailures = 10;

    #endregion

    #region Fields

    private readonly string _broker;
    private readonly int _port;
    private readonly string? _username;
    private readonly string? _password;
    private readonly bool _useSsl;
    private readonly string _deviceId;
    private readonly string _nodeId;
    private readonly string _version;
    private readonly Action<string>? _onCommandReceived;
    private readonly Action? _onConnectedCallback;
    private readonly string _configDir;

    private IMqttClient? _mqtt;
    private CancellationTokenSource? _cts;
    private int _consecutiveFailures;
    private int _reconnectAttempt;
    private bool _disposed;
    private List<SensorData>? _lastDiscoverySensors;

    #endregion

    #region Properties

    /// <summary>
    /// True when the MQTT client is connected to the broker.
    /// Used for smart routing between mobile_app and MQTT.
    /// </summary>
    public bool IsConnected => _mqtt?.IsConnected == true;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new MQTT client.
    /// </summary>
    /// <param name="broker">MQTT broker hostname or IP.</param>
    /// <param name="port">MQTT broker port.</param>
    /// <param name="username">Optional username for authentication.</param>
    /// <param name="password">Optional password for authentication.</param>
    /// <param name="useSsl">Whether to use TLS/SSL.</param>
    /// <param name="configDir">Config directory for reading registration.json.</param>
    /// <param name="version">App version string for discovery configs.</param>
    /// <param name="onCommandReceived">Callback invoked when a command is received via MQTT.</param>
    /// <param name="onConnectedCallback">Optional callback invoked when connection is established.</param>
    public MqttClient(string broker, int port, string? username, string? password,
        bool useSsl, string configDir, string version,
        Action<string>? onCommandReceived = null,
        Action? onConnectedCallback = null)
    {
        _broker = broker;
        _port = port;
        _username = username;
        _password = password;
        _useSsl = useSsl;
        _configDir = configDir;
        _version = version;
        _onCommandReceived = onCommandReceived;
        _onConnectedCallback = onConnectedCallback;
        _deviceId = LoadDeviceId();
        _nodeId = SanitizeNodeId(_deviceId);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Connect to the MQTT broker with LWT and auto-reconnect.
    /// Blocks until cancelled via Dispose or DisconnectAsync.
    /// </summary>
    public async Task ConnectAsync()
    {
        _cts = new CancellationTokenSource();
        await ConnectWithRetryAsync(_cts.Token);
    }

    /// <summary>
    /// Gracefully disconnect: publish offline LWT, then disconnect.
    /// </summary>
    public async Task DisconnectAsync()
    {
        try
        {
            if (_mqtt?.IsConnected == true)
            {
                // Publish offline availability (LWT)
                var availTopic = GetAvailabilityTopic();
                var offlineMsg = new MqttApplicationMessageBuilder()
                    .WithTopic(availTopic)
                    .WithPayload(PayloadOffline)
                    .WithRetainFlag()
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();
                await _mqtt.PublishAsync(offlineMsg, CancellationToken.None);

                await _mqtt.DisconnectAsync(new MqttClientDisconnectOptions
                {
                    ReasonString = "Client disconnecting"
                }, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT] Disconnect error: {ex.Message}");
        }
        finally
        {
            _cts?.Cancel();
        }
    }

    /// <summary>
    /// Publish HA MQTT Discovery configs for all sensors and the media player.
    /// All messages are sent with retain=true so HA remembers them.
    /// Must be called after ConnectAsync when IsConnected is true.
    /// </summary>
    /// <param name="sensors">List of SensorData to publish discovery for.</param>
    public async Task PublishDiscoveryAsync(List<SensorData> sensors)
    {
        if (_mqtt?.IsConnected != true)
        {
            Console.WriteLine("[MQTT] Cannot publish discovery: not connected");
            return;
        }

        try
        {
            _lastDiscoverySensors = sensors;

            var device = GetDeviceBlock();

            foreach (var sensor in sensors)
            {
                var config = BuildSensorDiscoveryConfig(sensor, device);
                var topic = $"{DiscoveryPrefix}/{sensor.Type}/{_nodeId}/{sensor.UniqueId}/config";
                await PublishRetainedAsync(topic, config);
                await Task.Delay(100); // 100ms delay to avoid overwhelming HA
            }

            // Publish media_player discovery
            var mediaConfig = BuildMediaPlayerDiscoveryConfig(device);
            var mediaTopic = $"{DiscoveryPrefix}/media_player/{_nodeId}/media_player/config";
            await PublishRetainedAsync(mediaTopic, mediaConfig);

            Console.WriteLine($"[MQTT] Published discovery for {sensors.Count} sensors + media_player");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT] Discovery publish error: {ex.Message}");
        }
    }

    /// <summary>
    /// Publish state for a single sensor to its state topic.
    /// </summary>
    public async Task PublishSensorStateAsync(SensorData sensor)
    {
        if (_mqtt?.IsConnected != true) return;

        try
        {
            var stateTopic = GetStateTopic(sensor.Type, sensor.UniqueId);
            var payload = FormatSensorPayload(sensor);
            await PublishAsync(stateTopic, payload);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT] Sensor state publish error: {ex.Message}");
        }
    }

    /// <summary>
    /// Publish state for multiple sensors to their respective state topics.
    /// </summary>
    public async Task PublishSensorStatesAsync(List<SensorData> sensors)
    {
        if (_mqtt?.IsConnected != true) return;

        foreach (var sensor in sensors)
        {
            await PublishSensorStateAsync(sensor);
        }
    }

    /// <summary>
    /// Publish media player state and optional JSON attributes.
    /// </summary>
    /// <param name="state">Media player state (e.g. "playing", "paused", "stopped").</param>
    /// <param name="attributes">Optional JSON string with media attributes.</param>
    public async Task PublishMediaStateAsync(string state, string? attributes = null)
    {
        if (_mqtt?.IsConnected != true) return;

        try
        {
            var stateTopic = GetMediaStateTopic();
            await PublishAsync(stateTopic, state);

            if (!string.IsNullOrEmpty(attributes))
            {
                var attrTopic = GetMediaAttributesTopic();
                await PublishAsync(attrTopic, attributes);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT] Media state publish error: {ex.Message}");
        }
    }

    #endregion

    #region Private: Connection Management

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_consecutiveFailures >= MaxFailures)
            {
                Console.WriteLine($"[MQTT] Too many consecutive failures ({_consecutiveFailures}), pausing 60s...");
                try { await Task.Delay(TimeSpan.FromSeconds(60), ct); }
                catch (OperationCanceledException) { break; }
                _consecutiveFailures = 0;
            }

            try
            {
                var factory = new MqttFactory();
                _mqtt = factory.CreateMqttClient();

                // Wire up event handlers
                _mqtt.ConnectedAsync += OnConnectedAsync;
                _mqtt.DisconnectedAsync += OnDisconnectedAsync;
                _mqtt.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

                var options = BuildClientOptions();

                Console.WriteLine($"[MQTT] Connecting to {_broker}:{_port} (TLS={_useSsl})...");
                var connectResult = await _mqtt.ConnectAsync(options, ct);

                if (connectResult.ResultCode == MqttClientConnectResultCode.Success)
                {
                    // Reset counters on successful connection
                    _consecutiveFailures = 0;
                    _reconnectAttempt = 0;
                    Console.WriteLine($"[MQTT] Connected successfully to {_broker}");

                    // OnConnectedAsync handles online LWT + subscriptions

                    // Keep alive until disconnected or cancelled
                    while (_mqtt.IsConnected && !ct.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), ct);
                    }

                    if (ct.IsCancellationRequested) break;
                }
                else
                {
                    _consecutiveFailures++;
                    Console.WriteLine($"[MQTT] Connection rejected: {connectResult.ResultCode} ({_consecutiveFailures}/{MaxFailures})");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                Console.WriteLine($"[MQTT] Connection error ({_consecutiveFailures}/{MaxFailures}): {ex.Message}");
            }

            if (!ct.IsCancellationRequested)
            {
                var delay = GetBackoffDelay();
                Console.WriteLine($"[MQTT] Reconnecting in {delay.TotalSeconds:F0}s...");
                try { await Task.Delay(delay, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        // Cleanup
        try { _mqtt?.Dispose(); } catch { }
    }

    private MqttClientOptions BuildClientOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_broker, _port)
            .WithClientId($"{TopicPrefix}_{_deviceId}")
            .WithCleanSession(false)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
            .WithSessionExpiryInterval(600); // 10 minutes

        // Last Will Testament: publish "offline" on availability topic
        var availTopic = GetAvailabilityTopic();
        builder.WithWillTopic(availTopic)
               .WithWillPayload(PayloadOffline)
               .WithWillRetain(true)
               .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);

        // TLS/SSL
        if (_useSsl)
        {
            builder.WithTlsOptions(o => o
                .WithSslProtocols(SslProtocols.Tls12 | SslProtocols.Tls13)
                .WithCertificateValidationHandler(_ => true));
        }

        // Authentication
        if (!string.IsNullOrEmpty(_username))
        {
            builder.WithCredentials(_username, _password ?? "");
        }

        return builder.Build();
    }

    private TimeSpan GetBackoffDelay()
    {
        _reconnectAttempt++;
        // Exponential backoff: 1s, 2s, 4s, 8s, 16s, 30s (capped)
        var seconds = Math.Min(30, Math.Pow(2, _reconnectAttempt - 1));
        return TimeSpan.FromSeconds(seconds);
    }

    #endregion

    #region Private: MQTT Event Handlers

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        Console.WriteLine("[MQTT] Connected event fired — publishing online status");

        try
        {
            // Publish online availability (LWT counterpart)
            var availTopic = GetAvailabilityTopic();
            var onlineMsg = new MqttApplicationMessageBuilder()
                .WithTopic(availTopic)
                .WithPayload(PayloadOnline)
                .WithRetainFlag()
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await _mqtt!.PublishAsync(onlineMsg, CancellationToken.None);

            // Subscribe to command topics
            await SubscribeToCommandsAsync();

            // Subscribe to Home Assistant status for birth message support
            await SubscribeToHomeAssistantStatusAsync();

            // Notify application that connection is established
            _onConnectedCallback?.Invoke();

            // Publish discovery + states on (re)connect if we have cached sensor data
            if (_lastDiscoverySensors != null)
            {
                try
                {
                    await PublishDiscoveryAsync(_lastDiscoverySensors);
                    await PublishSensorStatesAsync(_lastDiscoverySensors);
                }
                catch (Exception discoveryEx)
                {
                    Console.WriteLine($"[MQTT] Discovery on connect error: {discoveryEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT] OnConnected handler error: {ex.Message}");
        }
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        Console.WriteLine($"[MQTT] Disconnected: {e.Reason} (clientWasConnected={e.ClientWasConnected})");
        await Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            Console.WriteLine($"[MQTT] Received: {topic} => {payload}");

            // Handle Home Assistant birth message
            if (topic == "homeassistant/status" && payload == "online")
            {
                Console.WriteLine("[MQTT] Home Assistant restarted — re-publishing birth message");
                await PublishBirthMessageAsync();
                return;
            }

            // Route the command payload to the callback
            _onCommandReceived?.Invoke(payload);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT] Message handling error: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    #endregion

    #region Private: Subscription

    private async Task SubscribeToCommandsAsync()
    {
        if (_mqtt?.IsConnected != true) return;

        var mediaTopic = GetCommandTopic("media");
        var systemTopic = GetCommandTopic("system");

        var mqttFactory = new MqttFactory();
        var mediaFilter = mqttFactory.CreateTopicFilterBuilder()
            .WithTopic(mediaTopic)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        var systemFilter = mqttFactory.CreateTopicFilterBuilder()
            .WithTopic(systemTopic)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqtt.SubscribeAsync(mediaFilter, CancellationToken.None);
        await _mqtt.SubscribeAsync(systemFilter, CancellationToken.None);

        Console.WriteLine($"[MQTT] Subscribed to command topics: {mediaTopic}, {systemTopic}");
    }

    /// <summary>
    /// Subscribe to homeassistant/status so we can re-publish discovery
    /// when Home Assistant restarts (birth message).
    /// </summary>
    private async Task SubscribeToHomeAssistantStatusAsync()
    {
        if (_mqtt?.IsConnected != true) return;

        var mqttFactory = new MqttFactory();
        var statusFilter = mqttFactory.CreateTopicFilterBuilder()
            .WithTopic("homeassistant/status")
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqtt.SubscribeAsync(statusFilter, CancellationToken.None);

        Console.WriteLine("[MQTT] Subscribed to homeassistant/status for birth message support");
    }

    #endregion

    #region Private: Publishing Helpers

    private async Task PublishAsync(string topic, string payload)
    {
        if (_mqtt?.IsConnected != true) return;

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await _mqtt.PublishAsync(msg, CancellationToken.None);
    }

    private async Task PublishRetainedAsync(string topic, string payload)
    {
        if (_mqtt?.IsConnected != true) return;

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithRetainFlag()
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await _mqtt.PublishAsync(msg, CancellationToken.None);
    }

    #endregion

    #region Private: Birth Message

    /// <summary>
    /// Re-publish availability online + all discovery configs.
    /// Called on initial connect and when HA restarts (homeassistant/status = "online").
    /// </summary>
    private async Task PublishBirthMessageAsync()
    {
        if (_mqtt?.IsConnected != true) return;

        try
        {
            // 1. Publish online availability (retained)
            var availTopic = GetAvailabilityTopic();
            var onlineMsg = new MqttApplicationMessageBuilder()
                .WithTopic(availTopic)
                .WithPayload(PayloadOnline)
                .WithRetainFlag()
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await _mqtt.PublishAsync(onlineMsg, CancellationToken.None);
            Console.WriteLine("[MQTT] Birth: published online availability");

            // 2. Re-publish all discovery messages with 100ms delay
            if (_lastDiscoverySensors != null && _lastDiscoverySensors.Count > 0)
            {
                var device = GetDeviceBlock();

                foreach (var sensor in _lastDiscoverySensors)
                {
                    var config = BuildSensorDiscoveryConfig(sensor, device);
                    var topic = $"{DiscoveryPrefix}/{sensor.Type}/{_nodeId}/{sensor.UniqueId}/config";
                    await PublishRetainedAsync(topic, config);
                    await Task.Delay(100);
                }

                // Media player
                var mediaConfig = BuildMediaPlayerDiscoveryConfig(device);
                var mediaTopic = $"{DiscoveryPrefix}/media_player/{_nodeId}/media_player/config";
                await PublishRetainedAsync(mediaTopic, mediaConfig);

                Console.WriteLine($"[MQTT] Birth: re-published discovery for {_lastDiscoverySensors.Count} sensors + media_player");
            }
            else
            {
                Console.WriteLine("[MQTT] Birth: no discovery data cached, skipping discovery re-publish");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT] Birth message error: {ex.Message}");
        }
    }

    #endregion

    #region Private: Topic Builders

    /// <summary>
    /// LWT/availability topic: ha_desklink/{deviceId}/availability
    /// </summary>
    private string GetAvailabilityTopic()
        => $"{TopicPrefix}/{_deviceId}/availability";

    /// <summary>
    /// Sensor state topic: ha_desklink/{deviceId}/{component}/{objectId}/state
    /// </summary>
    private string GetStateTopic(string component, string objectId)
        => $"{TopicPrefix}/{_deviceId}/{component}/{objectId}/state";

    /// <summary>
    /// Command topic: ha_desklink/{deviceId}/command/{component}
    /// </summary>
    private string GetCommandTopic(string component)
        => $"{TopicPrefix}/{_deviceId}/command/{component}";

    /// <summary>
    /// Media player state topic: ha_desklink/{deviceId}/media_player/state
    /// </summary>
    private string GetMediaStateTopic()
        => $"{TopicPrefix}/{_deviceId}/media_player/state";

    /// <summary>
    /// Media player attributes topic: ha_desklink/{deviceId}/media_player/attributes
    /// </summary>
    private string GetMediaAttributesTopic()
        => $"{TopicPrefix}/{_deviceId}/media_player/attributes";

    #endregion

    #region Private: Discovery Config Builders

    /// <summary>
    /// Build the shared "device" block for all discovery configs.
    /// Identifiers tie all entities to one device in HA.
    /// </summary>
    private Dictionary<string, object> GetDeviceBlock()
    {
        return new Dictionary<string, object>
        {
            ["identifiers"] = new[] { $"{TopicPrefix}_{_deviceId}" },
            ["name"] = AppDisplayName,
            ["manufacturer"] = AppManufacturer,
            ["model"] = AppModel,
            ["sw_version"] = _version
        };
    }

    /// <summary>
    /// Build the HA MQTT Discovery config JSON for a sensor or binary_sensor.
    /// Follows the spec: includes unique_id, state_topic, availability, device block.
    /// </summary>
    private string BuildSensorDiscoveryConfig(SensorData sensor, Dictionary<string, object> device)
    {
        var config = new Dictionary<string, object>
        {
            ["unique_id"] = $"{TopicPrefix}_{_deviceId}_{sensor.UniqueId}",
            ["name"] = sensor.Name,
            ["state_topic"] = GetStateTopic(sensor.Type, sensor.UniqueId),
            ["availability_topic"] = GetAvailabilityTopic(),
            ["payload_available"] = PayloadOnline,
            ["payload_not_available"] = PayloadOffline,
            ["device"] = device
        };

        // Optional sensor properties
        if (!string.IsNullOrEmpty(sensor.UnitOfMeasurement))
            config["unit_of_measurement"] = sensor.UnitOfMeasurement;
        if (!string.IsNullOrEmpty(sensor.DeviceClass))
            config["device_class"] = sensor.DeviceClass;
        if (!string.IsNullOrEmpty(sensor.Icon))
            config["icon"] = sensor.Icon;
        if (!string.IsNullOrEmpty(sensor.StateClass))
            config["state_class"] = sensor.StateClass;
        if (!string.IsNullOrEmpty(sensor.EntityCategory))
            config["entity_category"] = sensor.EntityCategory;

        // Binary sensor specific: payload_on / payload_off
        if (sensor.SensorKind == SensorType.BinarySensor)
        {
            config["payload_on"] = sensor.PayloadOn ?? "on";
            config["payload_off"] = sensor.PayloadOff ?? "off";
        }

        return JsonSerializer.Serialize(config);
    }

    /// <summary>
    /// Build the HA MQTT Discovery config JSON for the media_player entity.
    /// Includes command_topic for media control via MQTT.
    /// </summary>
    private string BuildMediaPlayerDiscoveryConfig(Dictionary<string, object> device)
    {
        var config = new Dictionary<string, object>
        {
            ["unique_id"] = $"{TopicPrefix}_{_deviceId}_media_player",
            ["name"] = "Media Player",
            ["command_topic"] = GetCommandTopic("media"),
            ["state_topic"] = GetMediaStateTopic(),
            ["json_attributes_topic"] = GetMediaAttributesTopic(),
            ["availability_topic"] = GetAvailabilityTopic(),
            ["payload_available"] = PayloadOnline,
            ["payload_not_available"] = PayloadOffline,
            ["device"] = device
        };

        return JsonSerializer.Serialize(config);
    }

    #endregion

    #region Private: Helpers

    /// <summary>
    /// Read device_id from registration.json — same ID used for mobile_app.
    /// </summary>
    private string LoadDeviceId()
    {
        try
        {
            var path = Path.Combine(_configDir, "registration.json");
            if (File.Exists(path))
            {
                var data = JsonDocument.Parse(File.ReadAllText(path));
                if (data.RootElement.TryGetProperty("device_id", out var di))
                    return di.GetString() ?? "unknown";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MQTT] Failed to load device ID from registration.json: {ex.Message}");
        }

        // Fallback: generate a consistent ID from the machine name
        return Environment.MachineName.ToLowerInvariant();
    }

    /// <summary>
    /// Sanitize device ID for use in MQTT topics (lowercase, alphanumeric + underscore only).
    /// Creates the node_id used in discovery topics: ha_desklink_{sanitizedDeviceId}
    /// </summary>
    private static string SanitizeNodeId(string deviceId)
    {
        var sanitized = Regex.Replace(deviceId.ToLowerInvariant(), "[^a-z0-9_]", "_");
        return $"{TopicPrefix}_{sanitized}";
    }

    /// <summary>
    /// Format the sensor state value for the MQTT state payload.
    /// Binary sensors get "on"/"off", numeric sensors get their string representation.
    /// </summary>
    private static string FormatSensorPayload(SensorData sensor)
    {
        return sensor.State?.ToString() ?? "";
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _mqtt?.Dispose();
    }

    #endregion
}

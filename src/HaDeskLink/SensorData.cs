
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
namespace HaDeskLink;

/// <summary>
/// Sensor data model for HA mobile_app protocol.
/// </summary>
public class SensorData
{
    /// <summary>
    /// Sensor type: Sensor (measurement) or BinarySensor (on/off).
    /// </summary>
    public SensorType SensorKind { get; set; } = SensorType.Sensor;

    /// <summary>
    /// Computed sensor type string: "sensor" or "binary_sensor".
    /// </summary>
    public string Type => SensorKind switch
    {
        SensorType.BinarySensor => "binary_sensor",
        _ => "sensor"
    };

    public string UniqueId { get; set; } = "";
    public string Name { get; set; } = "";
    public object State { get; set; } = "";
    public string? UnitOfMeasurement { get; set; }
    public string? DeviceClass { get; set; }
    public string? Icon { get; set; }
    public string? StateClass { get; set; }
    public string? EntityCategory { get; set; } = "diagnostic";

    /// <summary>
    /// MQTT binary_sensor payload for ON state.
    /// </summary>
    public string? PayloadOn { get; set; } = "on";

    /// <summary>
    /// MQTT binary_sensor payload for OFF state.
    /// </summary>
    public string? PayloadOff { get; set; } = "off";

    public SensorData(string uniqueId, string name, object state, string unit = "",
        string deviceClass = "", string icon = "", string stateClass = "")
    {
        UniqueId = uniqueId;
        Name = name;
        State = state;
        if (!string.IsNullOrEmpty(unit)) UnitOfMeasurement = unit;
        if (!string.IsNullOrEmpty(deviceClass)) DeviceClass = deviceClass;
        if (!string.IsNullOrEmpty(icon)) Icon = icon;
        if (!string.IsNullOrEmpty(stateClass)) StateClass = stateClass;
    }
}

/// <summary>
/// Enumerates sensor type variants.
/// </summary>
public enum SensorType
{
    Sensor = 0,
    BinarySensor = 1
}
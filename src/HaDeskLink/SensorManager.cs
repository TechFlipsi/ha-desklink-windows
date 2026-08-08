
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
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HaDeskLink;

/// <summary>
/// Collects system sensor data using WMI, PerformanceCounter, and subprocess
/// calls — no driver or LibreHardwareMonitor dependency.
/// </summary>
public class SensorManager : IDisposable
{
    private bool _disposed;

    public SensorManager()
    {
        // No hardware initialisation needed — everything is queried on demand.
    }

    public List<SensorData> CollectAll()
    {
        var sensors = new List<SensorData>();

        sensors.AddRange(GetCpuSensors());
        sensors.AddRange(GetCpuClockSensors());
        sensors.AddRange(GetGpuSensors());
        sensors.AddRange(GetMemorySensors());
        sensors.AddRange(GetDiskSensors());
        sensors.Add(GetUptime());

        var lastAct = GetLastActivity();
        if (lastAct != null) sensors.Add(lastAct);

        var battery = GetBattery();
        if (battery != null) sensors.Add(battery);

        sensors.Add(GetIpAddress());
        sensors.Add(GetConnectivity());
        sensors.Add(GetProcessCount());
        sensors.Add(GetPageFile());
        sensors.Add(GetActiveWindow());

        var wifi = GetWifiSsid();
        if (wifi != null) sensors.Add(wifi);
        var wifiSignal = GetWifiSignal();
        if (wifiSignal != null) sensors.Add(wifiSignal);

        // Fan sensors (WMI Win32_Fan + GPU fan via nvidia-smi)
        sensors.AddRange(GetFanSensors());

        // Fullscreen sensor
        var fullscreen = GetFullscreenInfo();
        if (fullscreen != null) sensors.Add(fullscreen);

        // Monitor layout
        sensors.Add(GetMonitorLayout());

        // Brightness
        var brightness = GetBrightness();
        if (brightness != null) sensors.Add(brightness);

        // Idle time (seconds)
        var idleTime = GetIdleTimeSensor();
        if (idleTime != null) sensors.Add(idleTime);

        // Presence Detection (binary_sensor: on wenn idle_time < 300s UND connectivity = on)
        var presence = GetPresence();
        if (presence != null) sensors.Add(presence);

        // Audio sensors (volume + mute)
        sensors.AddRange(GetAudioSensors());

        // Mic active
        var micActive = GetMicActive();
        if (micActive != null) sensors.Add(micActive);

        // Webcam active
        var webcamActive = GetWebcamActive();
        if (webcamActive != null) sensors.Add(webcamActive);

        // GPU memory
        sensors.AddRange(GetGpuMemorySensors());

        // Network throughput
        sensors.AddRange(GetNetworkSensors());

        // Bluetooth devices connected (Anzahl verbundener Geräte)
        var bluetooth = GetBluetoothDevices();
        if (bluetooth != null) sensors.Add(bluetooth);

       // App version
       sensors.Add(GetAppVersion());
        // PC status (binary_sensor: "on" while app is running)
        var pcStatus = new SensorData("pc_status", "PC Status", "on",
            deviceClass: "connectivity", icon: "mdi:desktop-classic")
        {
            SensorKind = SensorType.BinarySensor,
            EntityCategory = null
        };
        sensors.Add(pcStatus);

        return sensors;
    }

    // ─────────────────────────────────────────────────────────────────
    //  CPU sensors (WMI + PerformanceCounter — no LHM)
    // ─────────────────────────────────────────────────────────────────

    private static List<SensorData> GetCpuSensors()
    {
        var result = new List<SensorData>();

        // --- CPU temperature via WMI MSAcpi_ThermalZoneTemperature ---
        // Requires elevation on some systems; fails gracefully.
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (ManagementObject obj in searcher.Get())
            {
                var raw = Convert.ToDouble(obj["CurrentTemperature"]);
                // Raw value is tenths of Kelvin → Celsius
                var celsius = Math.Round((raw / 10.0) - 273.15, 1);
                result.Add(new SensorData("cpu_temperature", "CPU Temperature",
                    celsius, "\u00b0C",
                    icon: "mdi:thermometer", stateClass: "measurement"));
                break; // First thermal zone only
            }
        }
        catch { /* not available or not elevated */ }

        // --- CPU load via WMI Win32_Processor.LoadPercentage ---
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT LoadPercentage FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var load = Convert.ToDouble(obj["LoadPercentage"]);
                result.Add(new SensorData("cpu_percent", "CPU Usage",
                    Math.Round(load, 1), "%",
                    icon: "mdi:cpu-64-bit", stateClass: "measurement"));
                break; // Aggregate across all CPUs via first result
            }
        }
        catch { }

        // Fallback: if WMI returned no load, try PerformanceCounter
        if (!result.Any(s => s.UniqueId == "cpu_percent"))
        {
            try
            {
                using var pc = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                pc.NextValue(); // First read = 0
                System.Threading.Thread.Sleep(100);
                var load = Math.Round(pc.NextValue(), 1);
                if (load >= 0)
                {
                    result.Add(new SensorData("cpu_percent", "CPU Usage",
                        load, "%",
                        icon: "mdi:cpu-64-bit", stateClass: "measurement"));
                }
            }
            catch { }
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  CPU clock (WMI Win32_Processor)
    // ─────────────────────────────────────────────────────────────────

    private static List<SensorData> GetCpuClockSensors()
    {
        var result = new List<SensorData>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CurrentClockSpeed, MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                // Prefer CurrentClockSpeed (dynamic); fall back to MaxClockSpeed
                uint mhz = 0;
                try { mhz = Convert.ToUInt32(obj["CurrentClockSpeed"]); }
                catch { }
                if (mhz == 0)
                {
                    try { mhz = Convert.ToUInt32(obj["MaxClockSpeed"]); }
                    catch { }
                }
                if (mhz > 0)
                {
                    result.Add(new SensorData("cpu_clock", "CPU Clock", (double)mhz,
                        "MHz", icon: "mdi:speedometer", stateClass: "measurement"));
                }
                break; // First processor
            }

            // Fallback: PerformanceCounter "% Processor Performance" × MaxClockSpeed
            if (!result.Any(s => s.UniqueId == "cpu_clock"))
            {
                try
                {
                    uint maxClock = 0;
                    using (var searcher2 = new ManagementObjectSearcher(
                        "SELECT MaxClockSpeed FROM Win32_Processor"))
                    {
                        foreach (ManagementObject obj in searcher2.Get())
                        {
                            try { maxClock = Convert.ToUInt32(obj["MaxClockSpeed"]); }
                            catch { }
                            break;
                        }
                    }
                    if (maxClock > 0)
                    {
                        using var pc = new PerformanceCounter(
                            "Processor Information", "% Processor Performance", "_Total");
                        pc.NextValue();
                        System.Threading.Thread.Sleep(50);
                        var perf = pc.NextValue();
                        var mhz = Math.Round(maxClock * perf / 100.0, 0);
                        result.Add(new SensorData("cpu_clock", "CPU Clock", mhz,
                            "MHz", icon: "mdi:speedometer", stateClass: "measurement"));
                    }
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  GPU sensors (PerformanceCounter + nvidia-smi / AMD subprocess)
    // ─────────────────────────────────────────────────────────────────

    private static List<SensorData> GetGpuSensors()
    {
        var result = new List<SensorData>();
        var gpuVendor = GetGpuVendor();

        // --- GPU temperature ---
        // Strategy: nvidia-smi for NVIDIA, WMI thermal zone for AMD/Intel, or ADLX CLI
        if (gpuVendor == "NVIDIA")
        {
            // NVIDIA: nvidia-smi is the gold standard
            try
            {
                var psi = new ProcessStartInfo("nvidia-smi",
                    "--query-gpu=temperature.gpu --format=csv,noheader,nounits")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd()?.Trim();
                proc?.WaitForExit(3000);
                if (!string.IsNullOrEmpty(output) && double.TryParse(output, out var gpuTemp))
                {
                    result.Add(new SensorData("gpu_temperature", "GPU Temperature",
                        Math.Round(gpuTemp, 1), "\u00b0C",
                        icon: "mdi:gpu", stateClass: "measurement"));
                }
            }
            catch { /* nvidia-smi not available */ }
        }
        else
        {
            // AMD / Intel: try WMI thermal zones, then ADL command line tool
            var amdTemp = GetAmdGpuTemperature();
            if (amdTemp.HasValue)
            {
                result.Add(new SensorData("gpu_temperature", "GPU Temperature",
                    Math.Round(amdTemp.Value, 1), "\u00b0C",
                    icon: "mdi:gpu", stateClass: "measurement"));
            }
        }

        // --- GPU load via PerformanceCounter "GPU Engine" ---
        // Windows 10+ exposes per-engine utilization for ALL GPU vendors.
        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            var instances = category.GetInstanceNames();
            double maxUtil = 0;

            // Single-pass: read once (NextValue returns 0 on first call),
            // sleep, then read again for real values — always do both passes
            // regardless of first-pass result, since first NextValue() always = 0
            var gpuCounters = new List<PerformanceCounter>();
            foreach (var inst in instances)
            {
                if (!inst.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase) &&
                    !inst.Contains("engtype_Graphics", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    gpuCounters.Add(new PerformanceCounter("GPU Engine",
                        "Utilization Percentage", inst));
                }
                catch { }
            }

            // First read (always returns 0, but primes the counter)
            foreach (var pc in gpuCounters)
            { try { pc.NextValue(); } catch { } }

            // Brief pause for accurate second read
            System.Threading.Thread.Sleep(200);

            // Second read — this is the real value
            foreach (var pc in gpuCounters)
            {
                try
                {
                    var val = pc.NextValue();
                    if (val > maxUtil) maxUtil = val;
                }
                catch { }
            }

            // Dispose all counters
            foreach (var pc in gpuCounters)
            { try { pc.Dispose(); } catch { } }

            if (maxUtil > 0)
            {
                result.Add(new SensorData("gpu_load", "GPU Load",
                    Math.Round(maxUtil, 1), "%",
                    icon: "mdi:gpu", stateClass: "measurement"));
            }
        }
        catch { /* GPU Engine counters not available */ }

        return result;
    }

    /// <summary>Detects GPU vendor from WMI (NVIDIA, AMD, or Intel).</summary>
    private static string GetGpuVendor()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT AdapterCompatibility FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var compat = obj["AdapterCompatibility"]?.ToString() ?? "";
                if (compat.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "NVIDIA";
                if (compat.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    compat.IndexOf("ATI", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "AMD";
                if (compat.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Intel";
            }
        }
        catch { }
        return "Unknown";
    }

    /// <summary>Gets AMD GPU temperature using multiple driverless strategies.</summary>
    private static double? GetAmdGpuTemperature()
    {
        // Strategy 1: AMD ADLX CLI tool (installed with Radeon Software)
        // Location: %ProgramFiles%\\AMD\\CIM\\adl.exe or %ProgramFiles%\\AMD\\OverDrive\\od.exe
        var adlPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "AMD", "CIM", "adl.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AMD", "CIM", "adl.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AMD", "OverDrive", "od.exe"),
        };
        foreach (var adlPath in adlPaths)
        {
            if (!File.Exists(adlPath)) continue;
            try
            {
                var psi = new ProcessStartInfo(adlPath)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd() ?? "";
                proc?.WaitForExit(3000);
                // Parse temperature from ADL output (format varies)
                foreach (var line in output.Split('\n'))
                {
                    if (line.Contains("temp", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Temp", StringComparison.OrdinalIgnoreCase))
                    {
                        // Try to extract a number near "temp" keyword
                        var parts = line.Split(new[] { ':', '=', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var part in parts)
                        {
                            if (double.TryParse(part, out var temp) && temp > 20 && temp < 120)
                                return temp;
                        }
                    }
                }
            }
            catch { }
        }

        // Strategy 2: WMI thermal zones — look for GPU-related thermal zone
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (ManagementObject obj in searcher.Get())
            {
                var instanceName = obj["InstanceName"]?.ToString() ?? "";
                var raw = Convert.ToDouble(obj["CurrentTemperature"]);
                // Look for GPU-related thermal zone names (AMD GPUs often appear as "GPUZ" or similar)
                if (instanceName.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
                    instanceName.Contains("Graphics", StringComparison.OrdinalIgnoreCase) ||
                    instanceName.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                {
                    return Math.Round((raw / 10.0) - 273.15, 1);
                }
            }
        }
        catch { /* WMI thermal zones not accessible */ }

        // Strategy 3: PerformanceCounter GPU adapter memory (indirect indicator)
        // If GPU is under load, temperature is likely elevated — but can't get exact number
        // Return null — sensor simply won't appear in HA
        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Fan sensors (WMI Win32_Fan + GPU fan via nvidia-smi / AMD ADL)
    // ─────────────────────────────────────────────────────────────────

    private static List<SensorData> GetFanSensors()
    {
        var result = new List<SensorData>();
        var gpuVendor = GetGpuVendor();

        // GPU fan speed — vendor-specific
        if (gpuVendor == "NVIDIA")
        {
            try
            {
                var psi = new ProcessStartInfo("nvidia-smi",
                    "--query-gpu=fan.speed --format=csv,noheader,nounits")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd()?.Trim();
                proc?.WaitForExit(3000);
                if (!string.IsNullOrEmpty(output) && double.TryParse(output, out var fanPct))
                {
                    result.Add(new SensorData("gpu_fan_speed", "GPU Fan Speed",
                        Math.Round(fanPct, 0), "%",
                        icon: "mdi:fan", stateClass: "measurement"));
                }
            }
            catch { }
        }
        // AMD GPU fan — WMI thermal zone fan data or ADL CLI (limited availability)
        // Note: AMD doesn't expose fan percentage via simple CLI like nvidia-smi

        // System fans via WMI Win32_Fan (rarely populated on consumer boards)
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DesiredSpeed FROM Win32_Fan");
            foreach (ManagementObject obj in searcher.Get())
            {
                try
                {
                    var name = obj["Name"]?.ToString() ?? "System Fan";
                    var rpm = Convert.ToDouble(obj["DesiredSpeed"]);
                    if (rpm > 0)
                    {
                        var uid = name.ToLowerInvariant()
                            .Replace(" ", "_").Replace("#", "");
                        result.Add(new SensorData($"fan_{uid}", $"Fan: {name}",
                            Math.Round(rpm, 0), "RPM",
                            icon: "mdi:fan", stateClass: "measurement"));
                    }
                }
                catch { }
            }
        }
        catch { /* Win32_Fan not available */ }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Memory sensors (WMI — unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static List<SensorData> GetMemorySensors()
    {
        var result = new List<SensorData>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                var totalKB = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                var freeKB = Convert.ToDouble(obj["FreePhysicalMemory"]);
                var usedGB = Math.Round((totalKB - freeKB) / 1048576.0, 2);
                var totalGB = Math.Round(totalKB / 1048576.0, 2);
                var freeGB = Math.Round(freeKB / 1048576.0, 2);
                var percent = Math.Round((1 - freeKB / totalKB) * 100, 1);

                result.Add(new SensorData("memory_percent", "Memory Usage", percent, "%",
                    icon: "mdi:memory", stateClass: "measurement"));
                result.Add(new SensorData("memory_used", "Memory Used", usedGB, "GB",
                    icon: "mdi:memory", stateClass: "measurement"));
                result.Add(new SensorData("memory_free", "Memory Free", freeGB, "GB",
                    icon: "mdi:memory", stateClass: "measurement"));
                result.Add(new SensorData("memory_total", "Memory Total", totalGB, "GB",
                    icon: "mdi:memory"));
            }
        }
        catch { }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Disk sensors (DriveInfo — unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static List<SensorData> GetDiskSensors()
    {
        var result = new List<SensorData>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var label = drive.Name.TrimEnd('\\');
                var driveKey = label.Replace(":", "").ToLower();

                var total = (double)drive.TotalSize / (1024 * 1024 * 1024);
                var free = (double)drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                var used = total - free;
                var percent = Math.Round(used / total * 100, 1);

                result.Add(new SensorData($"disk_{driveKey}_percent", $"Disk {label} Usage",
                    percent, "%", icon: "mdi:harddisk", stateClass: "measurement"));
                result.Add(new SensorData($"disk_{driveKey}_free", $"Disk {label} Free",
                    Math.Round(free, 2), "GB", icon: "mdi:harddisk", stateClass: "measurement"));
                result.Add(new SensorData($"disk_{driveKey}_used", $"Disk {label} Used",
                    Math.Round(used, 2), "GB", icon: "mdi:harddisk", stateClass: "measurement"));
                result.Add(new SensorData($"disk_{driveKey}_total", $"Disk {label} Total",
                    Math.Round(total, 2), "GB", icon: "mdi:harddisk"));
            }
        }
        catch { }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Uptime (unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData GetUptime()
    {
        var uptime = Environment.TickCount64 / 1000;
        var hours = Math.Round(uptime / 3600.0, 1);
        return new SensorData("uptime", "Uptime", hours, "h",
            icon: "mdi:clock-outline", stateClass: "measurement");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Last activity (unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData? GetLastActivity()
    {
        try
        {
            var idle = GetIdleTimeMs();
            var minutes = Math.Round(idle / 60000.0, 1);
            return new SensorData("last_activity", "Last Activity", minutes, "min",
                icon: "mdi:account-clock", stateClass: "measurement");
        }
        catch { return null; }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Battery (WMI — unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData? GetBattery()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT EstimatedChargeRemaining FROM Win32_Battery");
            foreach (var obj in searcher.Get())
            {
                var pct = Math.Round(Convert.ToDouble(obj["EstimatedChargeRemaining"]), 0);
                return new SensorData("battery", "Battery", pct, "%",
                    deviceClass: "battery", icon: "mdi:battery", stateClass: "measurement");
            }
        }
        catch { }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    //  User32 interop (unchanged)
    // ─────────────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    private static uint GetIdleTimeMs()
    {
        var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        GetLastInputInfo(ref lii);
        return (uint)Environment.TickCount - lii.dwTime;
    }

    // ─────────────────────────────────────────────────────────────────
    //  IP address (WMI — unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData GetIpAddress()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT IPAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");
            foreach (var obj in searcher.Get())
            {
                var ips = obj["IPAddress"] as string[];
                if (ips != null)
                {
                    foreach (var ip in ips)
                    {
                        if (ip.Contains("."))
                        {
                            return new SensorData("ip_address", "IP Address", ip,
                                icon: "mdi:ip-network");
                        }
                    }
                }
            }
        }
        catch { }
        return new SensorData("ip_address", "IP Address", "unavailable",
            icon: "mdi:ip-network-off");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Connectivity (ping — unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData GetConnectivity()
    {
        try
        {
            // Ping HA URL host instead of hardcoded 8.8.8.8 — works in isolated networks
            var pingHost = "8.8.8.8";
            try
            {
                var config = Config.Load();
                if (!string.IsNullOrEmpty(config.HaUrl) && Uri.TryCreate(config.HaUrl, UriKind.Absolute, out var haUri))
                {
                    pingHost = haUri.Host;
                }
            }
            catch { }

            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = ping.Send(pingHost, 2000);
            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                return new SensorData("connectivity", "Connectivity", "on",
                    deviceClass: "connectivity", icon: "mdi:check-network");
        }
        catch { }
        return new SensorData("connectivity", "Connectivity", "off",
            deviceClass: "connectivity", icon: "mdi:close-network");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Process count (unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData GetProcessCount()
    {
        try
        {
            var count = System.Diagnostics.Process.GetProcesses().Length;
            return new SensorData("process_count", "Running Processes", count, "",
                icon: "mdi:cog", stateClass: "measurement");
        }
        catch { return new SensorData("process_count", "Running Processes", 0, icon: "mdi:cog"); }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Page file (WMI — unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData GetPageFile()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CurrentUsage, AllocatedBaseSize FROM Win32_PageFileUsage");
            foreach (var obj in searcher.Get())
            {
                var usedMB = Convert.ToDouble(obj["CurrentUsage"]);
                var totalMB = Convert.ToDouble(obj["AllocatedBaseSize"]);
                var usedGB = Math.Round(usedMB / 1024.0, 2);
                var totalGB = Math.Round(totalMB / 1024.0, 2);
                var percent = Math.Round(usedMB / totalMB * 100, 1);
                return new SensorData("page_file_percent", "Page File Usage", percent, "%",
                    icon: "mdi:harddisk", stateClass: "measurement");
            }
        }
        catch { }
        return new SensorData("page_file_percent", "Page File Usage", 0, "%",
            icon: "mdi:harddisk", stateClass: "measurement");
    }

    // ─────────────────────────────────────────────────────────────────
    //  WiFi (WMI + netsh — unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData? GetWifiSsid()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT SSID FROM Win32_NetworkConnection WHERE ConnectionState = 'Connected'");
            foreach (var obj in searcher.Get())
            {
                var ssid = obj["SSID"]?.ToString();
                if (!string.IsNullOrEmpty(ssid))
                    return new SensorData("wifi_ssid", "WiFi Network", ssid,
                        icon: "mdi:wifi");
            }
        }
        catch { }
        return null;
    }

    private static SensorData? GetWifiSignal()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Description FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2");
            var psi = new System.Diagnostics.ProcessStartInfo("netsh", "wlan show interfaces")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit(3000);

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains("Signal") && line.Contains("%"))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1)
                    {
                        var pctStr = parts[1].Trim().Replace("%", "").Trim();
                        if (int.TryParse(pctStr, out var pct))
                        {
                            return new SensorData("wifi_signal", "WiFi Signal", pct, "%",
                                icon: "mdi:wifi-strength-" + (pct > 75 ? "4" : pct > 50 ? "3" : pct > 25 ? "2" : "1"),
                                stateClass: "measurement");
                        }
                    }
                }
            }
        }
        catch { }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Active window (unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData GetActiveWindow()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            var title = new System.Text.StringBuilder(256);
            GetWindowText(hwnd, title, 256);
            var name = title.ToString();
            if (!string.IsNullOrEmpty(name))
                return new SensorData("active_window", "Active Window", name,
                    icon: "mdi:window-maximize");
        }
        catch { }
        return new SensorData("active_window", "Active Window", "unknown",
            icon: "mdi:window-maximize");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Network throughput (PerformanceCounter — unchanged)
    // ─────────────────────────────────────────────────────────────────

    private List<SensorData> GetNetworkSensors()
    {
        var result = new List<SensorData>();
        try
        {
            var category = new System.Diagnostics.PerformanceCounterCategory("Network Interface");
            var instances = category.GetInstanceNames();
            foreach (var instance in instances)
            {
                if (instance.ToLowerInvariant().Contains("loopback") ||
                    instance.ToLowerInvariant().Contains("isatap") ||
                    instance.ToLowerInvariant().Contains("teredo") ||
                    instance.ToLowerInvariant().Contains("bluetooth"))
                    continue;

                try
                {
                    using var sent = new System.Diagnostics.PerformanceCounter("Network Interface",
                        "Bytes Sent/sec", instance);
                    using var recv = new System.Diagnostics.PerformanceCounter("Network Interface",
                        "Bytes Received/sec", instance);
                    sent.NextValue(); recv.NextValue();
                    System.Threading.Thread.Sleep(100);
                    var uploadKbps = Math.Round(sent.NextValue() / 1024.0, 1);
                    var downloadKbps = Math.Round(recv.NextValue() / 1024.0, 1);

                    result.Add(new SensorData("network_upload", "Upload Speed", uploadKbps, "KB/s",
                        icon: "mdi:upload", stateClass: "measurement"));
                    result.Add(new SensorData("network_download", "Download Speed", downloadKbps, "KB/s",
                        icon: "mdi:download", stateClass: "measurement"));
                    break;
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Fullscreen detection (User32 — unchanged)
    // ─────────────────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int Size;
        public RECT Monitor;
        public RECT WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    private const int GWL_STYLE = -16;
    private const uint WS_CAPTION = 0x00C00000;
    private const uint WS_THICKFRAME = 0x00040000;

    private SensorData? GetFullscreenInfo()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return new SensorData("fullscreen", "Fullscreen", "off", icon: "mdi:fullscreen", stateClass: "measurement");

            var titleBuilder = new System.Text.StringBuilder(256);
            GetWindowText(hwnd, titleBuilder, 256);
            var title = titleBuilder.ToString();

            if (string.IsNullOrWhiteSpace(title))
                return new SensorData("fullscreen", "Fullscreen", "off", icon: "mdi:fullscreen", stateClass: "measurement");

            GetWindowRect(hwnd, out var windowRect);

            var monitor = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
            var monitorInfo = new MONITORINFO();
            monitorInfo.Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(monitor, ref monitorInfo);

            var workArea = monitorInfo.WorkArea;
            var screen = monitorInfo.Monitor;

            var style = GetWindowLong(hwnd, GWL_STYLE);
            var isBorderless = (style & (WS_CAPTION | WS_THICKFRAME)) == 0;

            var windowWidth = windowRect.Right - windowRect.Left;
            var windowHeight = windowRect.Bottom - windowRect.Top;
            var workWidth = workArea.Right - workArea.Left;
            var workHeight = workArea.Bottom - workArea.Top;
            var screenW = screen.Right - screen.Left;
            var screenH = screen.Bottom - screen.Top;

            bool coversWorkArea = windowRect.Left <= workArea.Left + 5 &&
                                 windowRect.Top <= workArea.Top + 5 &&
                                 windowWidth >= workWidth - 10 &&
                                 windowHeight >= workHeight - 10;

            bool coversEntireScreen = windowRect.Left <= screen.Left + 2 &&
                                      windowRect.Top <= screen.Top + 2 &&
                                      windowWidth >= screenW - 5 &&
                                      windowHeight >= screenH - 5;

            var fullscreen = isBorderless || coversEntireScreen;

            if (!fullscreen && coversWorkArea)
                fullscreen = true;

            var state = fullscreen ? "on" : "off";

            return new SensorData("fullscreen", "Fullscreen", state, icon: "mdi:fullscreen", stateClass: "measurement");
        }
        catch
        {
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Monitor layout (unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData GetMonitorLayout()
    {
        try
        {
            var screens = Screen.AllScreens;
            var count = screens.Length;
            var layout = count <= 1 ? "1" : string.Join("+", System.Linq.Enumerable.Range(1, count));
            return new SensorData("monitor_layout", "Monitor Layout", layout, icon: "mdi:monitor-multiple");
        }
        catch
        {
            return new SensorData("monitor_layout", "Monitor Layout", "unknown", icon: "mdi:monitor-multiple");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Brightness (WMI + PowerShell fallback — unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData? GetBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE");
            foreach (var obj in searcher.Get())
            {
                var brightness = Convert.ToUInt32(obj["CurrentBrightness"]);
                return new SensorData("brightness", "Brightness", brightness, "%",
                    deviceClass: "illuminance", icon: "mdi:brightness-6", stateClass: "measurement");
            }
        }
        catch { }
        return null;
    }

    public static void SetBrightness(int targetBrightness)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM WmiMonitorBrightness WHERE Active=TRUE");
            var results = searcher.Get();
            if (results.Count > 0)
            {
                foreach (ManagementObject obj in results)
                {
                    obj.InvokeMethod("WmiSetBrightness", new object[] { (uint)targetBrightness, 0 });
                }
                return;
            }
        }
        catch { }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"(Get-WmiObject -Namespace root/WMI -Class WmiMonitorBrightnessMethods).WmiSetBrightness(1, {targetBrightness})\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(5000);
        }
        catch { }
    }

    public static int? GetCurrentBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE");
            foreach (var obj in searcher.Get())
            {
                return Convert.ToInt32(obj["CurrentBrightness"]);
            }
        }
        catch { }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Idle time sensor (seconds)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData? GetIdleTimeSensor()
    {
        try
        {
            var idleMs = GetIdleTimeMs();
            var seconds = Math.Round(idleMs / 1000.0, 1);
            return new SensorData("idle_time", "Idle Time", seconds, "s",
                icon: "mdi:timer-outline", stateClass: "measurement");
        }
        catch { return null; }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Audio sensors (volume, mute) via IAudioEndpointVolume
    // ─────────────────────────────────────────────────────────────────

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    private const uint COINIT_MULTITHREADED = 0x0;

    // COM interface GUIDs and IIDs for MMDevice API
    private static readonly Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IID_IMMDeviceEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid IID_IAudioEndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void _VtblGap0_1(); // Not needed
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr ppDevice);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        void _VtblGap0_2(); // RegisterControlChangeNotify, UnregisterControlChangeNotify
        [PreserveSig] int GetChannelCount(out uint channelCount);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, IntPtr pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, IntPtr pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, IntPtr pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
    private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

    private static List<SensorData> GetAudioSensors()
    {
        var result = new List<SensorData>();
        try
        {
            CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED);
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enumerator.GetDefaultAudioEndpoint((int)EDataFlow.eRender, (int)ERole.eMultimedia, out var devicePtr) == 0)
            {
                var volumeInterface = Marshal.GetObjectForIUnknown(devicePtr);
                var volume = (IAudioEndpointVolume)volumeInterface;

                // Volume level
                var hr = volume.GetMasterVolumeLevelScalar(out float level);
                if (hr == 0)
                {
                    var volPct = (int)Math.Round(level * 100);
                    result.Add(new SensorData("audio_volume", "Audio Volume", volPct, "%",
                        icon: "mdi:volume-high", stateClass: "measurement"));
                }

                // Mute
                hr = volume.GetMute(out bool mute);
                if (hr == 0)
                {
                    result.Add(new SensorData("audio_mute", "Audio Mute", mute ? "on" : "off",
                        deviceClass: "plug", icon: "mdi:volume-off"));
                }

                Marshal.ReleaseComObject(volume);
                Marshal.ReleaseComObject(devicePtr);
            }
            Marshal.ReleaseComObject(enumerator);
        }
        catch { }
        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Microphone active sensor (check capture audio sessions)
    // ─────────────────────────────────────────────────────────────────

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        void _VtblGap0_2(); // GetAudioSessionControl, GetSimpleAudioVolume
        [PreserveSig] int GetSessionEnumerator(out IntPtr sessionEnum);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IntPtr session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        void _VtblGap0_7(); // State, DisplayName, IconPath, GroupingParam, GetProcessId
        [PreserveSig] int GetProcessId(out uint processId);
        [PreserveSig] int GetState(out int state);
    }

    private static SensorData? GetMicActive()
    {
        try
        {
            CoInitializeEx(IntPtr.Zero, COINIT_MULTITHREADED);
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();

            var hr = enumerator.GetDefaultAudioEndpoint((int)EDataFlow.eCapture, (int)ERole.eConsole, out var devicePtr);
            if (hr != 0)
            {
                Marshal.ReleaseComObject(enumerator);
                return null;
            }

            IAudioSessionManager2? sessionManager = null;
            IAudioSessionEnumerator? sessionEnum = null;

            try
            {
                var iidIAudioSessionManager2 = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
                hr = Marshal.QueryInterface(devicePtr, ref iidIAudioSessionManager2, out var sessionManager2);
                if (hr != 0)
                {
                    return new SensorData("mic_active", "Microphone Active", "off",
                        deviceClass: "plug", icon: "mdi:microphone");
                }

                sessionManager = Marshal.GetObjectForIUnknown(sessionManager2) as IAudioSessionManager2;
                if (sessionManager == null)
                {
                    return new SensorData("mic_active", "Microphone Active", "off",
                        deviceClass: "plug", icon: "mdi:microphone");
                }

                if (sessionManager.GetSessionEnumerator(out var sessionEnumPtr) != 0)
                {
                    return new SensorData("mic_active", "Microphone Active", "off",
                        deviceClass: "plug", icon: "mdi:microphone");
                }

                sessionEnum = Marshal.GetObjectForIUnknown(sessionEnumPtr) as IAudioSessionEnumerator;
                if (sessionEnum == null)
                {
                    return new SensorData("mic_active", "Microphone Active", "off",
                        deviceClass: "plug", icon: "mdi:microphone");
                }

                sessionEnum.GetCount(out var count);
                for (int i = 0; i < count; i++)
                {
                    if (sessionEnum.GetSession(i, out var sessionPtr) == 0)
                    {
                        var session = Marshal.GetObjectForIUnknown(sessionPtr) as IAudioSessionControl2;
                        if (session != null && session.GetState(out var state) == 0)
                        {
                            // State: 0 = inactive, 1 = active, 2 = expired
                            if (state == 1)
                            {
                                Marshal.ReleaseComObject(session);
                                return new SensorData("mic_active", "Microphone Active", "on",
                                    deviceClass: "plug", icon: "mdi:microphone");
                            }
                        }
                        Marshal.ReleaseComObject(session);
                    }
                }
            }
            finally
            {
                if (sessionEnum != null) Marshal.ReleaseComObject(sessionEnum);
                if (sessionManager != null) Marshal.ReleaseComObject(sessionManager);
                Marshal.ReleaseComObject(devicePtr);
                Marshal.ReleaseComObject(enumerator);
            }
        }
        catch { }

        return new SensorData("mic_active", "Microphone Active", "off",
            deviceClass: "plug", icon: "mdi:microphone");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Webcam active sensor (WMI Win32_PnPEntity for imaging devices)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData? GetWebcamActive()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Status FROM Win32_PnPEntity WHERE PNPClass = 'Image' OR PNPClass = 'Camera'");
            foreach (ManagementObject obj in searcher.Get())
            {
                var status = obj["Status"]?.ToString() ?? "";
                // Device is present and working
                if (status.Equals("OK", StringComparison.OrdinalIgnoreCase))
                {
                    return new SensorData("webcam_active", "Webcam Active", "on",
                        deviceClass: "plug", icon: "mdi:webcam");
                }
            }
        }
        catch { }
        return new SensorData("webcam_active", "Webcam Active", "off",
            deviceClass: "plug", icon: "mdi:webcam");
    }

    // ─────────────────────────────────────────────────────────────────
    //  GPU memory sensors (nvidia-smi / rocm-smi)
    // ─────────────────────────────────────────────────────────────────

    private static List<SensorData> GetGpuMemorySensors()
    {
        var result = new List<SensorData>();
        var gpuVendor = GetGpuVendor();

        try
        {
            if (gpuVendor == "NVIDIA")
            {
                // nvidia-smi --query-gpu=memory.used,memory.total --format=csv,noheader,nounits
                var psi = new ProcessStartInfo("nvidia-smi",
                    "--query-gpu=memory.used,memory.total --format=csv,noheader,nounits")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd()?.Trim();
                proc?.WaitForExit(3000);

                if (!string.IsNullOrEmpty(output))
                {
                    // Parse "used, total" (both in MiB)
                    var parts = output.Split(',');
                    if (parts.Length >= 2 &&
                        double.TryParse(parts[0].Trim(), out var used) &&
                        double.TryParse(parts[1].Trim(), out var total))
                    {
                        result.Add(new SensorData("gpu_memory_used", "GPU Memory Used",
                            Math.Round(used, 0), "MB",
                            icon: "mdi:memory", stateClass: "measurement"));
                        result.Add(new SensorData("gpu_memory_total", "GPU Memory Total",
                            Math.Round(total, 0), "MB",
                            icon: "mdi:memory"));
                    }
                }
            }
            else if (gpuVendor == "AMD")
            {
                // Try rocm-smi
                var psi = new ProcessStartInfo("rocm-smi",
                    "--showmeminfo vram --csv")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd()?.Trim();
                proc?.WaitForExit(3000);

                if (!string.IsNullOrEmpty(output))
                {
                    // Parse CSV output looking for used/total VRAM
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("VRAM", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("memory", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(',');
                            // Try to find used and total values
                            double used = 0, total = 0;
                            for (int i = 0; i < parts.Length; i++)
                            {
                                var val = parts[i].Trim();
                                if (double.TryParse(val, out var num))
                                {
                                    if (used == 0) used = num;
                                    else if (total == 0) total = num;
                                }
                            }
                            if (used > 0 && total > 0)
                            {
                                result.Add(new SensorData("gpu_memory_used", "GPU Memory Used",
                                    Math.Round(used, 0), "MB",
                                    icon: "mdi:memory", stateClass: "measurement"));
                                result.Add(new SensorData("gpu_memory_total", "GPU Memory Total",
                                    Math.Round(total, 0), "MB",
                                    icon: "mdi:memory"));
                                break;
                            }
                        }
                    }
                }
            }
            // Intel: skip
        }
        catch { }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    //  App version (unchanged)
    // ─────────────────────────────────────────────────────────────────

    private static SensorData GetAppVersion()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        return new SensorData("ha_desklink_version", Localization.Get("ha_desklink_version", "HA DeskLink Version"),
            version, icon: "mdi:information-outline");
    }

    // ─────────────────────────────────────────────────────────────────
    //  Presence Detection (binary_sensor)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Presence Detection: Kombiniert idle_time und connectivity.
    /// Sensor ist "on" wenn idle_time &lt; 300 Sekunden UND connectivity = on.
    /// </summary>
    private static SensorData? GetPresence()
    {
        try
        {
            var idleMs = GetIdleTimeMs();
            var idleSeconds = idleMs / 1000.0;
            var isIdle = idleSeconds < 300;

            // Connectivity prüfen (Ping wie GetConnectivity)
            var isOnline = false;
            try
            {
                var pingHost = "8.8.8.8";
                try
                {
                    var config = Config.Load();
                    if (!string.IsNullOrEmpty(config.HaUrl) && Uri.TryCreate(config.HaUrl, UriKind.Absolute, out var haUri))
                        pingHost = haUri.Host;
                }
                catch { }

                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send(pingHost, 2000);
                isOnline = reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            }
            catch { }

            var isPresent = isIdle && isOnline ? "on" : "off";

            return new SensorData("presence", "Presence", isPresent,
                deviceClass: "presence", icon: "mdi:account-check")
            {
                SensorKind = SensorType.BinarySensor
            };
        }
        catch
        {
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Bluetooth Devices (Anzahl verbundener Geräte)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Zählt verbundene Bluetooth-Geräte über PowerShell Get-PnpDevice.
    /// Gibt null zurück wenn Bluetooth nicht verfügbar ist.
    /// </summary>
    private static SensorData? GetBluetoothDevices()
    {
        try
        {
            // PowerShell: Get-PnpDevice -Class Bluetooth | Where-Object Status -eq 'OK'
            // Zählt verbundene Bluetooth-Geräte
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command \"(Get-PnpDevice -Class Bluetooth -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'OK' } | Measure-Object).Count\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd()?.Trim();
            proc?.WaitForExit(5000);

            if (string.IsNullOrEmpty(output)) return null;

            if (int.TryParse(output, out int count))
            {
                return new SensorData("bluetooth_devices_connected", "Bluetooth Devices Connected",
                    count, "",
                    icon: "mdi:bluetooth-connect", stateClass: "measurement");
            }
        }
        catch { /* Bluetooth nicht verfügbar */ }

        return null;
    }

    // ─────────────────────────────────────────────────────────────────
    //  IDisposable
    // ─────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (!_disposed)
        {
            // No persistent native resources to clean up.
            // PerformanceCounters are created/disposed per-call.
            _disposed = true;
        }
    }
}

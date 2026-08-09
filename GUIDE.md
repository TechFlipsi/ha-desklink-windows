# HA DeskLink – Comprehensive User Manual

**Version 5.0.4** | Windows Companion App for Home Assistant

> 📖 **This manual explains every feature of HA DeskLink in detail** – from installation through all sensors, commands, and settings to troubleshooting. It is designed for new users who have never used HA DeskLink before.

---

## Table of Contents

1. [Installation & Initial Setup](#1-installation--initial-setup)
2. [Sensors](#2-sensors)
3. [PC Commands from Home Assistant](#3-pc-commands-from-home-assistant)
4. [Actionable Notifications](#4-actionable-notifications)
5. [Quick Actions](#5-quick-actions)
6. [Custom Commands](#6-custom-commands)
7. [App Launchers](#7-app-launchers)
8. [Notifications](#8-notifications)
9. [WebView2 Dashboard](#9-webview2-dashboard)
10. [MQTT (Optional Features)](#10-mqtt-optional-features)
11. [Auto-Update](#11-auto-update)
12. [Autostart](#12-autostart)
13. [Settings](#13-settings)
14. [Security](#14-security)
15. [Languages](#15-languages)
16. [Build & Development](#16-build--development)
17. [Troubleshooting](#17-troubleshooting)

---

## 1. Installation & Initial Setup

### System Requirements

- **Windows 10 or 11** (64-bit, x64)
- **No .NET Runtime required** – everything is included in the installer (self-contained)
- **No kernel driver required** – HA DeskLink uses WMI + PerformanceCounter (driverless)
- **Optional:** WebView2 Runtime (for the embedded dashboard – automatically downloaded if needed)

### Installation

1. Download the latest `HA_DeskLink_Setup_x.x.x.exe` from [GitHub Releases](https://github.com/TechFlipsi/ha-desklink-windows/releases/latest).
2. **Important:** **Right-click** the downloaded `.exe` file and select **"Run as Administrator"**.
   > ⚠️ A normal double-click or waiting for the UAC prompt will result in an error – please start via right-click as Administrator.
3. The installer (InnoSetup) installs HA DeskLink to `C:\Program Files\HA DeskLink\`.
   - The installer requires **Administrator privileges** (`PrivilegesRequired=admin` in `installer.iss`).
   - A Start Menu shortcut and Desktop shortcut are created.
4. After installation, HA DeskLink starts automatically.

### Initial Setup (Setup Wizard)

On first launch, the **Setup Wizard** appears (if no `registration.json` exists yet):

#### Step 1: Connect to Home Assistant

| Field | Description | Example |
|---|---|---|
| **HA URL** | The URL of your Home Assistant instance | `https://homeassistant.local:8123` |
| **Long-Lived Token** | A long-lived access token from HA | (see below) |
| **Verify SSL Certificate** | Checkbox whether SSL certificates are validated | Disable for self-signed certificates |

**Creating a token in Home Assistant:**
1. Open Home Assistant in your browser
2. Click your **Profile** at the bottom left
3. Go to **Security** → **Long-Lived Access Tokens**
4. Click **Create Token**, enter a name (e.g., "HA DeskLink")
5. Copy the token and paste it into the Setup Wizard

Click **"Connect"**. HA DeskLink registers with Home Assistant via the `mobile_app` protocol (same as the mobile app – no extra integration needed!).

#### Step 2: MQTT (optional)

After a successful HA connection, the MQTT step appears:

- **Continue without MQTT:** HA DeskLink works fully without MQTT (sensors, commands, notifications, Quick Actions).
- **With MQTT:** Enables Media Player, faster sensor updates, and PC status detection. Enter broker, port, username, password, and SSL option.

| Field | Description | Default |
|---|---|---|
| **Broker** | MQTT broker hostname/IP | (derived from HA URL) |
| **Port** | MQTT port | 1883 |
| **Username** | Optional, for authentication | (empty) |
| **Password** | Optional, for authentication | (empty) |
| **Use SSL/TLS** | TLS encryption | Off |

Click **"Test"** to verify the connection, then **"Apply & Continue"**.

> 💡 MQTT can also be configured later in the Settings.

### After Setup

After setup, HA DeskLink runs in the **System Tray** (bottom right in the taskbar). Sensors appear automatically in Home Assistant under **Settings → Devices & Services → mobile_app**.

---

## 2. Sensors

HA DeskLink collects comprehensive system sensor data and transmits it to Home Assistant. All sensors appear with the prefix `sensor.ha_desklink_` or `binary_sensor.ha_desklink_` in HA.

> 📌 **Entity name schema:** Entity IDs follow the pattern `sensor.ha_desklink_<sensor_id>` and `binary_sensor.ha_desklink_<sensor_id>`. Example: `sensor.ha_desklink_cpu_percent` for CPU usage.

### Sensors Overview

#### CPU Sensors

| Entity ID | Name | Unit | Description | Possible Values |
|---|---|---|---|---|
| `sensor.ha_desklink_cpu_percent` | CPU Usage | % | CPU load (WMI Win32_Processor.LoadPercentage, fallback: PerformanceCounter) | 0.0 – 100.0 |
| `sensor.ha_desklink_cpu_temperature` | CPU Temperature | °C | CPU temperature (WMI MSAcpi_ThermalZoneTemperature, **requires admin privileges**) | 20.0 – 100.0+ |
| `sensor.ha_desklink_cpu_clock` | CPU Clock | MHz | Current CPU clock speed (WMI CurrentClockSpeed, fallback: MaxClockSpeed × PerformanceCounter) | e.g. 3400.0 |

> ⚠️ **CPU temperature** requires Administrator privileges. Without admin rights, this sensor will not appear.

#### GPU Sensors

| Entity ID | Name | Unit | Description | Possible Values |
|---|---|---|---|---|
| `sensor.ha_desklink_gpu_load` | GPU Load | % | GPU utilization (PerformanceCounter "GPU Engine", all vendors) | 0.0 – 100.0 |
| `sensor.ha_desklink_gpu_temperature` | GPU Temperature | °C | GPU temperature (NVIDIA: nvidia-smi, AMD: WMI/ADLX, Intel: WMI) | 20.0 – 100.0+ |
| `sensor.ha_desklink_gpu_memory_used` | GPU Memory Used | MB | GPU VRAM used (NVIDIA: nvidia-smi, AMD: rocm-smi) | e.g. 2048.0 |
| `sensor.ha_desklink_gpu_memory_total` | GPU Memory Total | MB | Total GPU VRAM | e.g. 8192.0 |
| `sensor.ha_desklink_gpu_fan_speed` | GPU Fan Speed | % | GPU fan speed (nvidia-smi, NVIDIA only) | 0 – 100 |

> 💡 GPU sensors only appear if a GPU is present. With multiple GPUs, the first detected GPU is used.

#### Memory (RAM)

| Entity ID | Name | Unit | Description | Possible Values |
|---|---|---|---|---|
| `sensor.ha_desklink_memory_percent` | Memory Usage | % | RAM utilization (WMI Win32_OperatingSystem) | 0.0 – 100.0 |
| `sensor.ha_desklink_memory_used` | Memory Used | GB | RAM used | e.g. 12.50 |
| `sensor.ha_desklink_memory_free` | Memory Free | GB | RAM free | e.g. 3.50 |
| `sensor.ha_desklink_memory_total` | Memory Total | GB | Total RAM (static) | e.g. 16.00 |

#### Disk Drives

For **each** detected fixed disk drive (C:, D:, E:, etc.), four sensors are created:

| Entity ID (example C:) | Name | Unit | Description |
|---|---|---|---|
| `sensor.ha_desklink_disk_c_percent` | Disk C: Usage | % | Usage in % |
| `sensor.ha_desklink_disk_c_free` | Disk C: Free | GB | Free space |
| `sensor.ha_desklink_disk_c_used` | Disk C: Used | GB | Used space |
| `sensor.ha_desklink_disk_c_total` | Disk C: Total | GB | Total capacity |

> 💡 Additional drives (D:, E:, etc.) are detected automatically. The drive letter is lowercased and the colon removed: `disk_d_percent`, `disk_e_free`, etc.

#### System & Network

| Entity ID | Name | Unit | Description | Possible Values |
|---|---|---|---|---|
| `sensor.ha_desklink_uptime` | Uptime | h | PC uptime in hours (TickCount64) | e.g. 4.5 |
| `sensor.ha_desklink_last_activity` | Last Activity | min | Minutes since last mouse/keyboard input | e.g. 2.3 |
| `sensor.ha_desklink_idle_time` | Idle Time | s | Seconds since last input (GetLastInputInfo) | e.g. 138.5 |
| `sensor.ha_desklink_ip_address` | IP Address | – | Current IPv4 address (WMI) | e.g. 192.168.1.100 |
| `sensor.ha_desklink_process_count` | Running Processes | – | Number of running processes | e.g. 234 |
| `sensor.ha_desklink_page_file_percent` | Page File Usage | % | Page file utilization (WMI Win32_PageFileUsage) | e.g. 45.3 |
| `sensor.ha_desklink_network_upload` | Upload Speed | KB/s | Upload speed (PerformanceCounter, first non-loopback NIC) | e.g. 125.5 |
| `sensor.ha_desklink_network_download` | Download Speed | KB/s | Download speed | e.g. 2300.8 |
| `sensor.ha_desklink_bluetooth_devices_connected` | Bluetooth Devices Connected | – | Number of connected Bluetooth devices (PowerShell Get-PnpDevice) | e.g. 3 |

#### Binary Sensors (on/off)

| Entity ID | Name | Description | Possible Values |
|---|---|---|---|
| `binary_sensor.ha_desklink_connectivity` | Connectivity | Ping to HA host (fallback: 8.8.8.8) | `on` / `off` |
| `binary_sensor.ha_desklink_audio_mute` | Audio Mute | System audio muted (IAudioEndpointVolume COM) | `on` / `off` |
| `binary_sensor.ha_desklink_mic_active` | Microphone Active | Microphone in use (AudioSessionManager COM) | `on` / `off` |
| `binary_sensor.ha_desklink_webcam_active` | Webcam Active | Webcam present (WMI Win32_PnPEntity Image/Camera) | `on` / `off` |
| `binary_sensor.ha_desklink_presence` | Presence | on when idle_time < 300s AND connectivity = on | `on` / `off` |
| `binary_sensor.ha_desklink_pc_status` | PC Status | on while app is running (on exit: off) | `on` / `off` |

#### Audio & Brightness

| Entity ID | Name | Unit | Description | Possible Values |
|---|---|---|---|---|
| `sensor.ha_desklink_audio_volume` | Audio Volume | % | System volume (IAudioEndpointVolume COM) | 0 – 100 |
| `sensor.ha_desklink_brightness` | Brightness | % | Display brightness (WMI WmiMonitorBrightness, laptops only) | 0 – 100 |

#### WiFi

| Entity ID | Name | Unit | Description | Possible Values |
|---|---|---|---|---|
| `sensor.ha_desklink_wifi_ssid` | WiFi Network | – | Connected WiFi network (WMI Win32_NetworkConnection) | e.g. "MyWiFi" |
| `sensor.ha_desklink_wifi_signal` | WiFi Signal | % | WiFi signal strength (netsh wlan show interfaces) | 0 – 100 |

#### Display & Windows

| Entity ID | Name | Unit | Description | Possible Values |
|---|---|---|---|---|
| `sensor.ha_desklink_active_window` | Active Window | – | Title of the active window (GetForegroundWindow) | e.g. "Google Chrome" |
| `sensor.ha_desklink_fullscreen` | Fullscreen | – | Fullscreen mode detected (window size vs. monitor) | `on` / `off` |
| `sensor.ha_desklink_monitor_layout` | Monitor Layout | – | Monitor configuration | "1", "1+2", "1+2+3" |

#### Battery (laptops only)

| Entity ID | Name | Unit | Description | Possible Values |
|---|---|---|---|---|
| `sensor.ha_desklink_battery` | Battery | % | Battery level (WMI Win32_Battery) | 0 – 100 |

#### Fans

| Entity ID | Name | Unit | Description | Possible Values |
|---|---|---|---|---|
| `sensor.ha_desklink_gpu_fan_speed` | GPU Fan Speed | % | GPU fan (nvidia-smi, NVIDIA only) | 0 – 100 |
| `sensor.ha_desklink_fan_*` | Fan: * | RPM | System fans (WMI Win32_Fan, rarely available) | e.g. 1500 |

#### App Version

| Entity ID | Name | Description |
|---|---|---|
| `sensor.ha_desklink_ha_desklink_version` | HA DeskLink Version | Current app version (Assembly.Version) |

### Using Sensors in HA

#### Example: Dashboard card for CPU temperature

```yaml
type: gauge
entity: sensor.ha_desklink_cpu_temperature
name: CPU Temperature
unit: °C
min: 20
max: 100
severity:
  green: 0
  yellow: 70
  red: 85
```

#### Example: Automation for high CPU temperature

```yaml
automation:
  - alias: "CPU Temperature Warning"
    trigger:
      - platform: numeric_state
        entity_id: sensor.ha_desklink_cpu_temperature
        above: 85
        for:
          minutes: 5
    action:
      - service: notify.mobile_app_ha_desklink
        data:
          title: "⚠️ CPU too hot!"
          message: "CPU temperature is {{ states('sensor.ha_desklink_cpu_temperature') }}°C"
```

#### Example: Shut down PC only when no one is using it

```yaml
automation:
  - alias: "Shut down PC on inactivity"
    trigger:
      - platform: numeric_state
        entity_id: sensor.ha_desklink_idle_time
        above: 1800
        for:
          minutes: 5
    condition:
      - condition: state
        entity_id: binary_sensor.ha_desklink_connectivity
        state: "on"
    action:
      - service: notify.mobile_app_ha_desklink
        data:
          title: "Shut down PC"
          message: "PC has not been used for 30+ minutes."
          data:
            command: "shutdown"
```

---

## 3. PC Commands from Home Assistant

HA DeskLink receives commands via **notifications** – just like the mobile app. No extra integration in HA is needed. Commands are passed in the `data` field of the notification.

### All Available Commands

| Command | Spelling | Effect |
|---|---|---|
| Shut down | `shutdown` | Shuts down the PC in 30 seconds (`shutdown /s /t 30`) |
| Restart | `restart` or `reboot` | Restarts the PC in 30 seconds (`shutdown /r /t 30`) |
| Hibernate | `hibernate` | Puts the PC into hibernation (SetSuspendState) |
| Sleep | `sleep` | Puts the PC into sleep mode (SetSuspendState) |
| Lock | `lock_screen` or `lock` | Locks the Windows screen (LockWorkStation) |
| Mute volume | `volume_mute` or `mute` | Mutes/unmutes audio (ToggleMute) |
| Volume up | `volume_up` | Increases volume by ~10% (5× VK_VOLUME_UP) |
| Volume down | `volume_down` | Decreases volume by ~10% (5× VK_VOLUME_DOWN) |
| Media Play/Pause | `media_play_pause` | Play/Pause for media playback (VK_MEDIA_PLAY_PAUSE) |
| Media Next | `media_next` | Next track (VK_MEDIA_NEXT_TRACK) |
| Media Previous | `media_previous` | Previous track (VK_MEDIA_PREV_TRACK) |
| Brightness up | `brightness_up` | Increases brightness by ~10% (laptops only) |
| Brightness down | `brightness_down` | Decreases brightness by ~10% (laptops only) |
| Set brightness | `brightness:50` | Sets brightness to value 0-100 (laptops only, WmiSetBrightness) |
| Monitor on | `monitor_on` | Turns the monitor on (SC_MONITORPOWER -1) |
| Monitor off | `monitor_off` | Turns the monitor off (SC_MONITORPOWER 2) |
| Screenshot | `screenshot` | Screenshot + upload as HA event (CopyFromScreen → PNG → Base64) |
| Screenshot save | `screenshot_save` | Like screenshot, additionally saves locally |
| Snipping Tool | `snipping_tool` | Opens Windows Snipping Tool (Win+Shift+S) |
| Text-to-Speech | `tts:Hello World` | Speaks the text via Windows SAPI |
| Launch app | `launch:spotify` | Launches a configured app (see [App Launchers](#7-app-launchers)) |
| Custom command | (custom name) | Executes a configured script (see [Custom Commands](#6-custom-commands)) |
| Notification | *(no command)* | Shows only a notification |

> ⚠️ **Brightness commands** (`brightness_up`, `brightness_down`, `brightness:XX`) only work on **laptops** with built-in displays. On desktop PCs with external monitors, the commands are ignored.

### YAML Examples

#### Shut down

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Shut down PC"
  message: "The PC will shut down in 30 seconds"
  data:
    command: "shutdown"
```

#### Restart

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Restart PC"
  message: "The PC will restart"
  data:
    command: "restart"
```

#### Hibernate

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Hibernate"
  message: "PC going into hibernation"
  data:
    command: "hibernate"
```

#### Lock PC

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Lock PC"
  message: "The PC will be locked"
  data:
    command: "lock_screen"
```

#### Mute volume

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Volume"
  message: "Audio muted"
  data:
    command: "volume_mute"
```

#### Volume up

```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Volume up"
  data:
    command: "volume_up"
```

#### Media control – Play/Pause

```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Play/Pause"
  data:
    command: "media_play_pause"
```

#### Set brightness (laptops only)

```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Brightness to 50%"
  data:
    command: "brightness:50"
```

#### Turn off monitor

```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Monitor off"
  data:
    command: "monitor_off"
```

#### Take screenshot

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Screenshot"
  message: "Taking a screenshot..."
  data:
    command: "screenshot"
```

> 💡 The screenshot is sent to HA as an event `ha_desklink_screenshot` with Base64 image data. In HA, you can capture the event and save or display the image.

#### Text-to-Speech

```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Speak"
  data:
    command: "tts:The washing machine is done!"
```

#### Simple notification (no command)

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Reminder"
  message: "Don't forget to take out the trash!"
```

### Automation in HA

#### Shut down PC at 10 PM (only if PC is online)

```yaml
automation:
  - alias: "Shut down PC at 10 PM"
    trigger:
      - platform: time
        at: "22:00:00"
    condition:
      - condition: state
        entity_id: binary_sensor.ha_desklink_pc_status
        state: "on"
    action:
      - service: notify.mobile_app_ha_desklink
        data:
          title: "Good night!"
          message: "The PC is being shut down now."
          data:
            command: "shutdown"
```

#### Put PC to sleep on inactivity

```yaml
automation:
  - alias: "Sleep PC on inactivity"
    trigger:
      - platform: numeric_state
        entity_id: sensor.ha_desklink_idle_time
        above: 1800
    action:
      - service: notify.mobile_app_ha_desklink
        data:
          title: "Sleep"
          message: "PC is being put to sleep."
          data:
            command: "sleep"
```

### Dashboard Button in HA

```yaml
type: button
name: "Shut down PC"
tap_action:
  action: call-service
  service: notify.mobile_app_ha_desklink
  service_data:
    title: "Shut down PC"
    message: "Shutting down..."
    data:
      command: "shutdown"
```

```yaml
type: button
name: "Lock PC"
icon: mdi:lock
tap_action:
  action: call-service
  service: notify.mobile_app_ha_desklink
  service_data:
    message: "PC is being locked"
    data:
      command: "lock_screen"
```

---

## 4. Actionable Notifications

Since version 3.0, HA DeskLink supports **Actionable Notifications** – notifications with interactive action buttons. On Windows, a modern WinForms dialog with rounded corners is displayed.

### How It Works

- The notification contains a list of `actions` (buttons)
- Each button has an `action` key, a `title` (label), and optionally a `command`
- When a button is clicked, the associated `command` is executed
- `command_on_action` is a fallback command that is executed when a button has no own `command`

### YAML Example: Shut down PC with confirmation

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Shut down PC?"
  message: "Do you really want to shut down the PC?"
  data:
    actions:
      - action: SHUTDOWN
        title: "Shut down"
        command: shutdown
      - action: CANCEL
        title: "Cancel"
    command_on_action: shutdown
```

### YAML Example: Media control with buttons

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Media Control"
  message: "What do you want to do?"
  data:
    actions:
      - action: PLAY_PAUSE
        title: "Play/Pause"
        command: media_play_pause
      - action: NEXT
        title: "Next"
        command: media_next
      - action: PREV
        title: "Previous"
        command: media_previous
      - action: MUTE
        title: "Mute"
        command: volume_mute
```

### YAML Example: Power options

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Power Saving"
  message: "Choose an option:"
  data:
    actions:
      - action: SLEEP
        title: "Sleep"
        command: sleep
      - action: HIBERNATE
        title: "Hibernate"
        command: hibernate
      - action: SHUTDOWN
        title: "Shut Down"
        command: shutdown
      - action: CANCEL
        title: "Cancel"
```

### Fields in Detail

| Field | Type | Description |
|---|---|---|
| `actions` | List | List of action buttons |
| `actions[].action` | String | Unique action key (e.g. "SHUTDOWN") |
| `actions[].title` | String | Button label |
| `actions[].command` | String | Command to execute on click (optional) |
| `command_on_action` | String | Fallback command for buttons without their own `command` |

---

## 5. Quick Actions

Quick Actions allow you to **toggle Home Assistant entities directly from the PC** – via global hotkey or through the tray icon menu.

### How It Works

- When the Quick Actions hotkey is pressed, a popup opens with all configured entities
- Clicking an entity sends `homeassistant.toggle` to HA
- Entities are configured in the Settings

### Default Hotkeys

| Action | Default Hotkey | Description |
|---|---|---|
| Quick Actions | `Ctrl+Shift+H` | Opens the Quick Actions popup |
| Dashboard | `Ctrl+Shift+D` | Opens the WebView2 Dashboard |
| Settings | `Ctrl+Shift+S` | Opens the Settings window |

> 💡 All hotkeys are configurable (modifier + key). See [Settings → Hotkeys](#hotkeys).

### Configuration via Settings

1. Tray icon → Right-click → **Settings**
2. Go to **⚡ Quick Actions** (sidebar)
3. Click **"Add"** and select an entity from the dropdown list (loads all HA entities automatically)
4. Enter a display name
5. Save

### Configuration via config.json

Quick Actions are stored as a JSON array:

```json
{
  "QuickActions": "[{\"entityId\":\"light.living_room\",\"name\":\"Living Room\"},{\"entityId\":\"switch.outlet\",\"name\":\"Outlet\"}]"
}
```

### YAML Example: Automation that works with Quick Actions

Quick Actions send `homeassistant.toggle` to HA. The following automation reacts to it:

```yaml
automation:
  - alias: "React to Quick Action Toggle"
    trigger:
      - platform: state
        entity_id: light.living_room
    action:
      - service: notify.mobile_app_ha_desklink
        data:
          title: "Living Room"
          message: "Light is now {{ states('light.living_room') }}"
```

---

## 6. Custom Commands

Custom Commands allow you to define **your own scripts or commands** that can be triggered from Home Assistant.

### JSON Format

Custom Commands are stored in `config.json` as a JSON array:

```json
{
  "CustomCommands": "[{\"command\":\"run_backup\",\"script\":\"C:\\\\Scripts\\\\backup.bat\",\"name\":\"Run Backup\"}]"
}
```

Each entry has the following fields:

| Field | Type | Description |
|---|---|---|
| `command` | String | The command name that will be sent from HA (e.g. "run_backup") |
| `script` | String | Path to the script or command to execute (e.g. `C:\\Scripts\\backup.bat`) |
| `name` | String | Display name (optional) |

### Examples

#### Example 1: Start a backup script

**config.json entry:**
```json
{
  "CustomCommands": "[{\"command\":\"run_backup\",\"script\":\"C:\\\\Scripts\\\\backup.bat\",\"name\":\"Run Backup\"}]"
}
```

**HA YAML:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Backup"
  message: "Starting backup..."
  data:
    command: "run_backup"
```

#### Example 2: Restart a Docker container

**config.json entry:**
```json
{
  "CustomCommands": "[{\"command\":\"restart_docker\",\"script\":\"docker restart homeassistant\",\"name\":\"Restart HA Docker\"}]"
}
```

**HA YAML:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Restarting Docker container"
  data:
    command: "restart_docker"
```

#### Example 3: Multiple Custom Commands

```json
{
  "CustomCommands": "[{\"command\":\"clear_temp\",\"script\":\"C:\\\\Scripts\\\\clear_temp.bat\",\"name\":\"Clear Temp Files\"},{\"command\":\"defrag_c\",\"script\":\"defrag C: /U /V\",\"name\":\"Defragment\"},{\"command\":\"ipconfig_flush\",\"script\":\"ipconfig /flushdns\",\"name\":\"Flush DNS Cache\"}]"
}
```

**HA YAML for flushing DNS cache:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Flushing DNS cache"
  data:
    command: "ipconfig_flush"
```

> ⚠️ Custom Commands are executed via `cmd /c <script>`. This means any Windows command or batch script can be executed.

---

## 7. App Launchers

App Launchers allow you to **launch applications on the PC from Home Assistant**.

### JSON Format

App Launchers are stored in `config.json` as a JSON array:

```json
{
  "AppLaunchers": "[{\"command\":\"launch_spotify\",\"path\":\"spotify\",\"name\":\"Spotify\"}]"
}
```

Each entry has the following fields:

| Field | Type | Description |
|---|---|---|
| `command` | String | The command name (sent from HA with `launch:` prefix) |
| `path` | String | Path to the app or executable |
| `name` | String | Display name (optional) |

### Examples

#### Example 1: Launch Spotify

**config.json entry:**
```json
{
  "AppLaunchers": "[{\"command\":\"launch_spotify\",\"path\":\"spotify\",\"name\":\"Spotify\"}]"
}
```

**HA YAML:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Spotify"
  message: "Launching Spotify..."
  data:
    command: "launch:launch_spotify"
```

#### Example 2: Multiple apps

```json
{
  "AppLaunchers": "[{\"command\":\"spotify\",\"path\":\"spotify\",\"name\":\"Spotify\"},{\"command\":\"steam\",\"path\":\"C:\\\\Program Files (x86)\\\\Steam\\\\Steam.exe\",\"name\":\"Steam\"},{\"command\":\"notepad\",\"path\":\"notepad.exe\",\"name\":\"Notepad\"},{\"command\":\"calc\",\"path\":\"calc.exe\",\"name\":\"Calculator\"}]"
}
```

**HA YAML for launching Steam:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Launching Steam"
  data:
    command: "launch:steam"
```

> 💡 Apps are launched via `Process.Start` with `UseShellExecute=true`. This means UWP apps (e.g., Spotify from the Microsoft Store) and protocol handlers (e.g., `spotify:`, `steam://`) also work.

---

## 8. Notifications

HA DeskLink displays **toast notifications** on screen when Home Assistant sends a notification.

### How It Works

- Notifications are displayed as modern **dark-theme toasts**
- Rounded corners (GraphicsPath, no P/Invoke)
- Accent-colored left bar (blue for normal, green for connection status)
- **Auto-close after 8 seconds** – unless the mouse cursor is over the toast (pause on hover)
- Title, message, and timestamp are shown
- Close button (✕) in the top right

### Sending a notification

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Washing machine done"
  message: "The washing machine is done! Please hang up the laundry."
```

### Position Configuration

The position of toast notifications can be configured in the Settings:

| Position | Description |
|---|---|
| `bottom_left` | Bottom left (default) |
| `bottom_right` | Bottom right |
| `top_left` | Top left |
| `top_right` | Top right |

**Settings → Notifications → Position**

### Monitor Configuration

For multi-monitor setups, the monitor for notifications can be selected:

| Value | Description |
|---|---|
| `0` | Primary monitor (default) |
| `1` | Second monitor |
| `2` | Third monitor |
| etc. | |

**Settings → Notifications → Monitor**

### Example: Notification with action

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Doorbell"
  message: "Someone is at the door!"
  data:
    actions:
      - action: OPEN_CAMERA
        title: "Open Camera"
        command: launch:launch_camera
      - action: UNLOCK
        title: "Open Door"
        command: launch:open_door
```

---

## 9. WebView2 Dashboard

HA DeskLink includes an **embedded dashboard** based on Microsoft WebView2. It displays your Home Assistant instance directly in the app – like a browser, but without a browser.

### Setup

1. **Open:** Tray icon → Double-click, or Right-click → "Dashboard", or hotkey `Ctrl+Shift+D`
2. **WebView2 Runtime:** If WebView2 is not installed, a dialog appears with the option to automatically download it
   - Download URL: `https://go.microsoft.com/fwlink/p/?LinkId=2124703`
   - After installation, HA DeskLink must be restarted
3. **Login:** The first time you open it, the normal HA login form appears
   - Log in with username & password (as in the browser)
4. **Session persists:** WebView2 stores the session in the directory `%APPDATA%\HA_DeskLink\WebView2Data\`
   - After the first login, you do not need to log in again
   - The session survives app restarts

### Usage

- The dashboard opens in a 1300×850 pixel window
- Minimum size: 800×600
- You can use HA as usual (dashboards, settings, automations, etc.)
- Right-click context menus are enabled, DevTools are disabled
- Status bar is hidden

### Fallback

If WebView2 is not installed and not downloaded, HA opens in your **default browser**.

### Opening programmatically

```yaml
# HA cannot directly open the dashboard, but you can send
# a notification that reminds the user to open it on their desktop:
service: notify.mobile_app_ha_desklink
data:
  title: "Open Dashboard"
  message: "Press Ctrl+Shift+D on your PC to open the dashboard."
```

---

## 10. MQTT (Optional Features)

HA DeskLink v5.0 supports **optional MQTT** for advanced features. MQTT is **optional** – the app works fully without MQTT.

### MQTT Features

| Feature | Description |
|---|---|
| 🔊 **Media Player Entity** | Your PC appears as a Media Player in HA with now-playing info, Play/Pause, and volume control |
| 📡 **PC Status Binary Sensor** | Instant online/offline detection via Last Will Testament (LWT) |
| ⚡ **Commands to sleeping PC** | MQTT commands reach the PC even in sleep mode |
| 🔍 **Auto device discovery** | Media Player and PC Status appear automatically in HA (MQTT Discovery) |
| 🔒 **Reliable connection** | Auto-reconnect with exponential backoff (1s, 2s, 4s, 8s, 16s, 30s max) |
| 🪄 **Zero-Config Setup** | Automatically searches for Mosquitto on first launch |
| 🧭 **Smart Routing** | MQTT for sensors + commands, WebSocket remains for notifications |

### Configuration

MQTT can be configured in the setup wizard or in the Settings:

**Settings → 📡 MQTT**

| Field | Description | Default |
|---|---|---|
| MQTT enabled | Checkbox whether MQTT is used | Off |
| Broker | MQTT broker hostname/IP | (empty) |
| Port | MQTT port | 1883 |
| Username | Optional, for authentication | (empty) |
| Password | Optional, for authentication | (empty) |
| SSL/TLS | TLS encryption (TLS 1.2/1.3) | Off |
| Fallback Broker | Alternative broker address for auto-config | (empty) |

### config.json Values

```json
{
  "MqttEnabled": true,
  "MqttBroker": "192.168.1.100",
  "MqttPort": 1883,
  "MqttUsername": "desklink",
  "MqttPasswordEncrypted": "(DPAPI encrypted)",
  "MqttUseSsl": false,
  "MqttAutoConfigured": false,
  "MqttBrokerFallback": ""
}
```

> 🔒 The MQTT password is encrypted with **DPAPI** (same as the HA token).

### MQTT Topics

HA DeskLink uses the following topic structure:

| Topic | Direction | Description |
|---|---|---|
| `ha_desklink/{deviceId}/availability` | Publish | Online/Offline status (LWT, retained) |
| `ha_desklink/{deviceId}/{component}/{objectId}/state` | Publish | Sensor states |
| `ha_desklink/{deviceId}/media_player/state` | Publish | Media Player state (playing/paused/idle) |
| `ha_desklink/{deviceId}/media_player/attributes` | Publish | Media Player attributes (Title, Artist, Album, Source) |
| `ha_desklink/{deviceId}/command/media` | Subscribe | Receive media commands |
| `ha_desklink/{deviceId}/command/system` | Subscribe | Receive system commands |
| `homeassistant/{component}/{nodeId}/{objectId}/config` | Publish | MQTT Discovery config (retained) |
| `homeassistant/status` | Subscribe | HA status (birth message) |

### Media Player

The Media Player shows the currently playing music/video on the PC:

| Attribute | Description |
|---|---|
| State | `playing`, `paused`, `idle` |
| Title | Title of the current track |
| Artist | Artist |
| Album | Album |
| Source | App name (Spotify, Chrome, Firefox, Edge, VLC, etc.) |

Data is collected via the Windows **GlobalSystemMediaTransportControlsSessionManager** API (Windows 10+ build 18362+), with PowerShell fallback.

### PC Status (LWT)

- On startup: `ha_desklink/{deviceId}/availability` = `online` (retained)
- On exit: `ha_desklink/{deviceId}/availability` = `offline` (retained)
- On connection loss: LWT is automatically sent by the broker

### Using MQTT in HA

```yaml
# Media Player in Dashboard
type: media-control
entity: media_player.ha_desklink_media_player
```

```yaml
# Automation: Pause music when PC is locked
automation:
  - alias: "Pause music when PC is locked"
    trigger:
      - platform: state
        entity_id: binary_sensor.ha_desklink_pc_status
        to: "off"
    action:
      - service: media_player.media_pause
        target:
          entity_id: media_player.ha_desklink_media_player
```

---

## 11. Auto-Update

HA DeskLink automatically checks for new versions and installs them.

### How It Works

1. **On startup:** HA DeskLink checks GitHub Releases for a new version
2. **Periodically:** Checked every 2 hours
3. **Download:** If a new version is found, the installer is downloaded (`HA_DeskLink_Setup.exe`)
4. **Validation:** The file must be at least 1 MB in size (protection against corrupt downloads)
5. **Installation:** The installer is run with `/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS`
6. **Restart:** The old app exits, the installer installs the new version and restarts the app

### Update Protection (Loop Prevention)

- Before updating, a `.update_pending` file is written with the current version
- If the file exists and the version in it is >= the installed version, no update is offered
- After a successful update, the file is deleted
- Additional 1-hour cooldown between update checks

### Update Channels

| Channel | Description | config.json Value |
|---|---|---|
| **Stable** | Only stable releases (default) | `"stable"` |
| **Prerelease** | Includes beta/pre-release versions | `"prerelease"` |

**Settings → General → Update Channel**

### Manual Update Check

Tray icon → Right-click → **"Check for update"**

- If an update is available, a dialog appears asking whether to install it
- If no update is available, "You are up to date" is shown

---

## 12. Autostart

HA DeskLink can start automatically when you log into Windows.

### Task Scheduler (primary method)

HA DeskLink uses the **Windows Task Scheduler** for autostart:

- **Task name:** `HA_DeskLink`
- **Trigger:** On logon (`LogonTrigger`)
- **Privileges:** Highest available (`RunLevel: HighestAvailable`) – no UAC prompt!
- **Priority:** High (Priority: 2) – fastest startup
- **Properties:**
  - `MultipleInstancesPolicy: IgnoreNew` – only one instance
  - `DisallowStartIfOnBatteries: false` – starts even on battery
  - `StopIfGoingOnBatteries: false` – does not stop on battery
  - `StartWhenAvailable: true` – starts after delay if scheduled time was missed

### Registry Fallback

If Task Scheduler is unavailable, the **Registry** is used:

- **Key:** `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
- **Value:** `HA_DeskLink` = `"<path to HA_DeskLink.exe>"`
- ⚠️ With registry autostart, a UAC prompt appears because the app runs as administrator

### Start Menu Shortcut

Additionally, a **Start Menu shortcut** is created:
- Path: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\HA DeskLink.lnk`

### Enabling/Disabling Autostart

**Settings → General → Autostart** (checkbox)

Or via `config.json`:
```json
{
  "Autostart": true
}
```

When enabled, `Autostart.Enable()` is called (Task Scheduler + Start Menu shortcut).
When disabled, `Autostart.Disable()` is called (Task Scheduler + Registry entry are removed).

---

## 13. Settings

The settings window has a sidebar navigation with 7 sections. Open via:
- Tray icon → Right-click → **Settings**
- Hotkey: `Ctrl+Shift+S`

### Connection

| Setting | Type | Default | Description |
|---|---|---|---|
| HA URL | TextBox | `https://homeassistant.local:8123` | URL of the Home Assistant instance |
| Long-Lived Token | TextBox (password) | (empty) | Long-Lived Access Token from HA |
| Verify SSL Certificate | CheckBox | (empty) | Whether SSL certificates are validated; disable for self-signed certificates |
| Reconnect | Button | – | Re-registers the app with HA |

### General

| Setting | Type | Default | Description |
|---|---|---|---|
| Autostart | CheckBox | Enabled | Starts HA DeskLink automatically on Windows login |
| Sensor Interval | NumericUpDown | 30 (10-300) | Seconds between sensor updates |
| Update Channel | ComboBox | Stable | Stable or Prerelease |
| Reset Device ID | Button | – | Generates a new device ID (for new registration) |
| Re-register Sensors | Button | – | Re-registers all sensors with HA (helps after updates) |

### Appearance

| Setting | Type | Default | Description |
|---|---|---|---|
| Language | ComboBox | Deutsch | UI language (15 languages available) |
| Theme | ComboBox | System | System (follows Windows), Light, or Dark |

### Notifications

| Setting | Type | Default | Description |
|---|---|---|---|
| Position | ComboBox | Bottom left | Position of toast notifications |
| Monitor | ComboBox | 0 (Primary) | Monitor for notifications (0-N) |

Available positions:
- `bottom_left` – Bottom left (default)
- `bottom_right` – Bottom right
- `top_left` – Top left
- `top_right` – Top right

### Hotkeys

| Setting | Type | Default | Description |
|---|---|---|---|
| Quick Actions Modifier | ComboBox | Ctrl+Shift | Modifier keys for Quick Actions |
| Quick Actions Key | ComboBox | H | Key for Quick Actions |
| Dashboard Modifier | ComboBox | Ctrl+Shift | Modifier keys for Dashboard |
| Dashboard Key | ComboBox | D | Key for Dashboard |
| Settings Modifier | ComboBox | Ctrl+Shift | Modifier keys for Settings |
| Settings Key | ComboBox | S | Key for Settings |

Available modifiers: `ctrl_shift`, `ctrl_alt`, `ctrl`, `alt`, `shift`, `none`

> ⚠️ If the modifier is set to `none`, the respective hotkey is disabled.

### MQTT

| Setting | Type | Default | Description |
|---|---|---|---|
| MQTT enabled | CheckBox | Off | Enables/disables MQTT |
| Broker | TextBox | (empty) | MQTT broker hostname/IP |
| Port | TextBox | 1883 | MQTT port |
| Username | TextBox | (empty) | Optional, for authentication |
| Password | TextBox (password) | (empty) | Optional, for authentication |
| SSL/TLS | CheckBox | Off | TLS encryption |
| Fallback Broker | TextBox | (empty) | Alternative broker address |

### Quick Actions

Here you can add, remove, and sort Quick Actions (HA entity toggles). The entity list is loaded automatically from HA.

### Webhook Bind Address

| Setting | Type | Default | Description |
|---|---|---|---|
| WebhookBindAddress | String | `+` | Bind address for the webhook server (Port 59123) |

- `+` = All network interfaces (default, HA can access from anywhere)
- `localhost` = Local only (more secure, when HA is on the same machine)

### config.json Overview

All settings are stored in `%APPDATA%\HA_DeskLink\config.json`:

```json
{
  "HaUrl": "https://homeassistant.local:8123",
  "HaToken": "",
  "HaTokenEncrypted": "(DPAPI encrypted)",
  "VerifySsl": true,
  "Autostart": true,
  "SensorInterval": 30,
  "UpdateChannel": "stable",
  "Language": "de",
  "Theme": "system",
  "QuickActions": "[]",
  "HotkeyModifiers": "ctrl_shift",
  "HotkeyKey": "H",
  "HotkeyDashboardModifiers": "ctrl_shift",
  "HotkeyDashboardKey": "D",
  "HotkeySettingsModifiers": "ctrl_shift",
  "HotkeySettingsKey": "S",
  "MqttEnabled": false,
  "MqttBroker": "",
  "MqttPort": 1883,
  "MqttUsername": "",
  "MqttPassword": "",
  "MqttPasswordEncrypted": "",
  "MqttUseSsl": false,
  "MqttAutoConfigured": false,
  "MqttBrokerFallback": "",
  "CustomCommands": "[]",
  "AppLaunchers": "[]",
  "WebhookBindAddress": "+",
  "NotificationPosition": "bottom_left",
  "NotificationMonitor": 0
}
```

---

## 14. Security

### DPAPI Encryption

HA DeskLink encrypts sensitive data using the **Windows Data Protection API (DPAPI)**:

- **HA Token:** Encrypted with `ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser)`
- **MQTT Password:** Also DPAPI-encrypted
- **Storage location:** `%APPDATA%\HA_DeskLink\config.json` – the `HaToken` field is **always empty** in the saved file, only `HaTokenEncrypted` contains the encrypted value

**What does this mean?**
- The token can **only be decrypted by the same Windows user on the same PC**
- An attacker who copies the `config.json` **cannot** decrypt the token on a different PC or as a different user
- DPAPI is built into Windows and requires no additional software

### Plaintext to Encrypted Migration

When an old `config.json` with a plaintext token is found, the following happens on load:
1. The plaintext token (`HaToken`) is detected
2. It is encrypted with DPAPI and stored in `HaTokenEncrypted`
3. The `HaToken` field is cleared
4. The file is saved immediately

### Login Retry Limit

- MQTT: After 10 consecutive connection failures, a 60-second pause occurs, then retries
- Auto-reconnect with exponential backoff: 1s, 2s, 4s, 8s, 16s, 30s (max)

### Administrator Privileges

- The installer requires Administrator privileges (`PrivilegesRequired=admin`)
- The app automatically starts as Administrator (for CPU/GPU temperature via WMI)
- Autostart via Task Scheduler with `HighestAvailable` privileges (no UAC prompt on every start)
- **Without admin rights** the app still works – only CPU temperature, GPU temperature, and fan speed will be missing

### Webhook Security

- The webhook server (Port 59123) validates the token on every request
- Token validation uses `CryptographicOperations.FixedTimeEquals` (timing-safe, prevents timing attacks)
- Token is preferably read from the `Authorization: Bearer` header (more secure)
- Fallback: Token in query string (less secure, may appear in logs)
- `WebhookBindAddress` can be set to `localhost` to restrict access to the local machine

### TTS Security

- The TTS text is escaped before execution (`'` → `''`) to prevent **command injection**
- The text is wrapped in single quotes before being passed to PowerShell

---

## 15. Languages

HA DeskLink supports **15 languages**. The language can be changed in the Settings:

**Settings → Appearance → Language**

| Code | Language (native) |
|---|---|
| `de` | Deutsch |
| `en` | English |
| `es` | Español |
| `fr` | Français |
| `ja` | 日本語 |
| `zh` | 中文 |
| `it` | Italiano |
| `pt` | Português |
| `ru` | Русский |
| `nl` | Nederlands |
| `pl` | Polski |
| `tr` | Türkçe |
| `ar` | العربية |
| `ko` | 한국어 |
| `sv` | Svenska |

> 💡 Languages are automatically detected from the `Lang/` folder (all `*.json` files except `languages.json`). Display names are read from `languages.json`. Fallback is always German.

---

## 16. Build & Development

### Prerequisites

- **.NET 8 SDK** (or newer)
- **InnoSetup** (for the installer)
- **WebView2 SDK** (NuGet: `Microsoft.Web.WebView2`)
- **MQTTnet** (NuGet: `MQTTnet`)

### Build

```bash
# Compile app (self-contained, win-x64)
dotnet publish src/HaDeskLink -c Release -r win-x64 --self-contained -o publish

# Create installer
iscc installer.iss
```

### Project Structure

```
src/HaDeskLink/
├── HaDeskLink.csproj    # Project file
├── Program.cs           # Entry point (Main, setup wizard trigger)
├── DeskLinkApp.cs       # Main application (tray, sensor loop, update, hotkeys)
├── Config.cs            # Configuration (JSON, DPAPI encryption)
├── SensorManager.cs      # Sensor data collection (WMI, PerformanceCounter, COM)
├── SensorData.cs         # Sensor model
├── HaApiClient.cs       # HA mobile_app API client (registration, sensors, updates)
├── HaWebSocketClient.cs  # WebSocket client for push notifications
├── WebhookServer.cs      # HTTP listener for commands/notifications
├── CommandHandler.cs     # Command execution (shutdown, volume, screenshot, etc.)
├── NotificationHandler.cs # Toast notification handler
├── DashboardWindow.cs    # WebView2 dashboard window
├── SetupWizard.cs         # Initial setup wizard
├── SettingsWindow.cs      # Settings window
├── QuickActionWindow.cs   # Quick Actions popup
├── QuickActionHandler.cs  # Global hotkey handler
├── Autostart.cs           # Task Scheduler / Registry autostart
├── Localization.cs        # JSON-based localization system
├── MqttClient.cs          # MQTT client (Discovery, LWT, Media Player)
├── MqttSetupHelper.cs     # MQTT auto-setup helper
├── MediaPlayer.cs         # Media Player state (GSMT COM API)
├── Lang/                  # Language files (*.json)
│   ├── languages.json     # Language names (code → native name)
│   ├── de.json            # German
│   ├── en.json            # English
│   └── ...                # More languages
└── Assets/
    └── icon.ico           # App icon
```

### Versioning

Since v2.2.1, platform-independent version numbers apply:

| Change | Example | Explanation |
|---|---|---|
| Bug Fix | 2.2.1 → 2.2.2 | Bug fix, only the affected platform |
| New Features | 2.2.x → 3.0.0 | New features, all platforms simultaneously |

The version number is read from the `VERSION` file in the app directory (fallback: Assembly version).

### License

GPL v3 – Copyright © 2026 Fabian Kirchweger

This program is free software. If you modify or distribute it, you **must** release your changes under the same GPL v3 license. Closed-source or proprietary use is **not** permitted.

---

## 17. Troubleshooting

### Common Problems and Solutions

| Problem | Solution |
|---|---|
| **Cannot connect to HA** | 1. Check HA URL (including port 8123)<br>2. Check token (Long-Lived Access Token)<br>3. Firewall: allow port 8123<br>4. Disable SSL verification for self-signed certificates |
| **Sensors missing in HA** | 1. Wait 30-60 seconds (sensors are registered on startup)<br>2. Open device in HA (Settings → Devices & Services → mobile_app)<br>3. Restart app<br>4. "Re-register sensors" in Settings |
| **CPU temperature is empty** | Run app as Administrator (WMI MSAcpi_ThermalZoneTemperature requires admin rights) |
| **GPU temperature missing** | NVIDIA: nvidia-smi installed? AMD: Radeon Software installed? Intel: WMI thermal zone available? |
| **Webcam sensor always "off"** | On Windows, webcam detection is unreliable (WMI Win32_PnPEntity). The sensor only shows whether the device is present, not whether it's actively in use. |
| **SSL error** | Disable SSL verification in Settings (uncheck "Verify SSL Certificate") |
| **Token could not be loaded** | DPAPI decryption failed (different user or different machine). Reconfigure app (new token). |
| **Notifications don't appear** | 1. Check WebSocket connection (tray icon shows status)<br>2. Check notification service: `notify.mobile_app_ha_desklink`<br>3. App running as Administrator? |
| **Hotkeys don't work** | 1. Another app is blocking the global hotkey<br>2. Check hotkey configuration in Settings<br>3. Run app as Administrator |
| **MQTT won't connect** | 1. Check broker address and port<br>2. Check username/password<br>3. Check SSL option<br>4. Firewall: allow port 1883 (or configured port) |
| **Auto-update fails** | 1. Check internet connection<br>2. GitHub accessible?<br>3. Download manually from [Releases](https://github.com/TechFlipsi/ha-desklink-windows/releases/latest) and install as Administrator |
| **Dashboard (WebView2) won't load** | 1. Install WebView2 Runtime (automatically offered)<br>2. HA URL correct? |
| **App doesn't start automatically** | 1. Settings → Autostart enabled?<br>2. Task Scheduler: Task "HA_DeskLink" present? (`schtasks /query /tn "HA_DeskLink"`)<br>3. Registry: `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run\HA_DeskLink` |

### Log File

The log file is located at: `%APPDATA%\HA_DeskLink\error.log`

Open via: Tray icon → Right-click → **"Open Log"**

### Config Directory

All configuration files are in: `%APPDATA%\HA_DeskLink\`

| File | Description |
|---|---|
| `config.json` | Main configuration (settings, token encrypted) |
| `registration.json` | HA registration (webhook_id, device_id, cloud_url) |
| `error.log` | Log file |
| `.update_pending` | Marker file during an update |
| `WebView2Data/` | WebView2 session data (login cookies) |
| `device_id.txt` | Temporary file for device ID reset |

### Resetting the Device ID

If the app is no longer properly registered with HA (e.g., after a PC change or Windows reinstall):

1. Settings → General → **"Reset Device ID"**
2. A new UUID is generated and written to `device_id.txt`
3. On next launch, the app registers with the new ID at HA
4. The old device in HA must be deleted manually

### Support

- 💬 **Discord:** [discord.com/invite/zHPhQ7EaqH](https://discord.com/invite/zHPhQ7EaqH)
- 🐛 **GitHub Issues:** [github.com/TechFlipsi/ha-desklink-windows/issues](https://github.com/TechFlipsi/ha-desklink-windows/issues)

---

**Idea:** Fabian Kirchweger | **Code:** J.A.R.V.I.S. (Hermes Agent) | **License:** GPL v3

> This manual was generated from the source code of version 5.0.4. All entity names, commands, and configurations correspond to the actual implementation.
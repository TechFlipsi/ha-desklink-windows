# HA DeskLink – Umfassende Benutzeranleitung

**Version 5.0.4** | Windows Companion App für Home Assistant

> 📖 **Diese Anleitung erklärt jede Funktion von HA DeskLink im Detail** – von der Installation über alle Sensoren, Befehle und Einstellungen bis hin zur Fehlerbehebung. Sie richtet sich an neue Benutzer, die HA DeskLink noch nie verwendet haben.

---

## Inhaltsverzeichnis

1. [Installation & Ersteinrichtung](#1-installation--ersteinrichtung)
2. [Sensoren](#2-sensoren)
3. [PC-Befehle aus Home Assistant](#3-pc-befehle-aus-home-assistant)
4. [Actionable Notifications](#4-actionable-notifications)
5. [Quick Actions](#5-quick-actions)
6. [Custom Commands](#6-custom-commands)
7. [App Launchers](#7-app-launchers)
8. [Benachrichtigungen](#8-benachrichtigungen)
9. [WebView2 Dashboard](#9-webview2-dashboard)
10. [MQTT (optionale Features)](#10-mqtt-optionale-features)
11. [Auto-Update](#11-auto-update)
12. [Autostart](#12-autostart)
13. [Einstellungen](#13-einstellungen)
14. [Sicherheit](#14-sicherheit)
15. [Sprachen](#15-sprachen)
16. [Build & Entwicklung](#16-build--entwicklung)
17. [Fehlerbehebung](#17-fehlerbehebung)

---

## 1. Installation & Ersteinrichtung

### Systemanforderungen

- **Windows 10 oder 11** (64-Bit, x64)
- **Kein .NET Runtime nötig** – alles ist im Installer enthalten (self-contained)
- **Kein Kernel-Treiber nötig** – HA DeskLink verwendet WMI + PerformanceCounter (treiberlos)
- **Optional:** WebView2 Runtime (für das eingebettete Dashboard – wird bei Bedarf automatisch heruntergeladen)

### Installation

1. Lade die neueste `HA_DeskLink_Setup_x.x.x.exe` von [GitHub Releases](https://github.com/TechFlipsi/ha-desklink-windows/releases/latest) herunter.
2. **Wichtig:** Mache einen **Rechtsklick** auf die heruntergeladene `.exe`-Datei und wähle **„Als Administrator ausführen"**.
   > ⚠️ Ein normaler Doppelklick oder das Warten auf die UAC-Anfrage führt zu einer Fehlermeldung – bitte direkt per Rechtsklick als Administrator starten.
3. Der Installer (InnoSetup) installiert HA DeskLink nach `C:\Program Files\HA DeskLink\`.
   - Der Installer benötigt **Administrator-Rechte** (`PrivilegesRequired=admin` in `installer.iss`).
   - Es wird ein Start-Menü-Eintrag und eine Desktop-Verknüpfung erstellt.
4. Nach der Installation startet HA DeskLink automatisch.

### Ersteinrichtung (Setup-Wizard)

Beim ersten Start erscheint der **Setup-Wizard** (wenn noch keine `registration.json` existiert):

#### Schritt 1: Home Assistant verbinden

| Feld | Beschreibung | Beispiel |
|---|---|---|
| **HA URL** | Die URL deiner Home Assistant Instanz | `https://homeassistant.local:8123` |
| **Long-Lived Token** | Ein langlebiger Access-Token aus HA | (siehe unten) |
| **SSL-Zertifikat prüfen** | Checkbox, ob SSL-Zertifikate validiert werden | Bei self-signed Zertifikaten deaktivieren |

**Token erstellen in Home Assistant:**
1. Öffne Home Assistant im Browser
2. Klicke unten links auf dein **Profil**
3. Gehe zu **Sicherheit** → **Long-Lived Access Tokens**
4. Klicke **Token erstellen**, gib einen Namen ein (z.B. „HA DeskLink")
5. Kopiere den Token und füge ihn in den Setup-Wizard ein

Klicke **„Verbinden"**. HA DeskLink registriert sich über das `mobile_app`-Protokoll bei Home Assistant (genau wie die Handy-App – keine Extra-Integration nötig!).

#### Schritt 2: MQTT (optional)

Nach erfolgreicher HA-Verbindung erscheint der MQTT-Schritt:

- **Ohne MQTT fortfahren:** HA DeskLink funktioniert vollständig ohne MQTT (Sensoren, Befehle, Benachrichtigungen, Quick Actions).
- **Mit MQTT:** Ermöglicht Media Player, schnellere Sensor-Updates und PC Status-Erkennung. Gib Broker, Port, Benutzername, Passwort und SSL-Option ein.

| Feld | Beschreibung | Standardwert |
|---|---|---|
| **Broker** | MQTT-Broker Hostname/IP | (aus HA URL abgeleitet) |
| **Port** | MQTT-Port | 1883 |
| **Benutzername** | Optional, für Authentifizierung | (leer) |
| **Passwort** | Optional, für Authentifizierung | (leer) |
| **SSL/TLS verwenden** | TLS-Verschlüsselung | Aus |

Klicke **„Testen"** um die Verbindung zu prüfen, dann **„Übernehmen & fortfahren"**.

> 💡 MQTT kann auch später in den Einstellungen konfiguriert werden.

### Nach der Einrichtung

Nach dem Setup läuft HA DeskLink im **System Tray** (unten rechts in der Taskleiste). Sensoren erscheinen automatisch in Home Assistant unter **Einstellungen → Geräte & Dienste → mobile_app**.

---

## 2. Sensoren

HA DeskLink sammelt umfangreiche System-Sensordaten und überträgt sie an Home Assistant. Alle Sensoren erscheinen mit dem Präfix `sensor.ha_desklink_` bzw. `binary_sensor.ha_desklink_` in HA.

> 📌 **Entity-Namen-Schema:** Die Entity-IDs folgen dem Muster `sensor.ha_desklink_<sensor_id>` und `binary_sensor.ha_desklink_<sensor_id>`. Beispiel: `sensor.ha_desklink_cpu_percent` für die CPU-Auslastung.

### Sensoren im Überblick

#### CPU-Sensoren

| Entity-ID | Name | Einheit | Beschreibung | Mögliche Werte |
|---|---|---|---|---|
| `sensor.ha_desklink_cpu_percent` | CPU Usage | % | CPU-Auslastung (WMI Win32_Processor.LoadPercentage, Fallback: PerformanceCounter) | 0.0 – 100.0 |
| `sensor.ha_desklink_cpu_temperature` | CPU Temperature | °C | CPU-Temperatur (WMI MSAcpi_ThermalZoneTemperature, **benötigt Admin-Rechte**) | 20.0 – 100.0+ |
| `sensor.ha_desklink_cpu_clock` | CPU Clock | MHz | Aktuelle CPU-Taktrate (WMI CurrentClockSpeed, Fallback: MaxClockSpeed × PerformanceCounter) | z.B. 3400.0 |

> ⚠️ **CPU-Temperatur** benötigt Administrator-Rechte. Ohne Admin-Rechte erscheint dieser Sensor nicht.

#### GPU-Sensoren

| Entity-ID | Name | Einheit | Beschreibung | Mögliche Werte |
|---|---|---|---|---|
| `sensor.ha_desklink_gpu_load` | GPU Load | % | GPU-Auslastung (PerformanceCounter „GPU Engine", alle Vendor) | 0.0 – 100.0 |
| `sensor.ha_desklink_gpu_temperature` | GPU Temperature | °C | GPU-Temperatur (NVIDIA: nvidia-smi, AMD: WMI/ADLX, Intel: WMI) | 20.0 – 100.0+ |
| `sensor.ha_desklink_gpu_memory_used` | GPU Memory Used | MB | GPU VRAM verwendet (NVIDIA: nvidia-smi, AMD: rocm-smi) | z.B. 2048.0 |
| `sensor.ha_desklink_gpu_memory_total` | GPU Memory Total | MB | GPU VRAM gesamt | z.B. 8192.0 |
| `sensor.ha_desklink_gpu_fan_speed` | GPU Fan Speed | % | GPU-Lüftergeschwindigkeit (nvidia-smi, nur NVIDIA) | 0 – 100 |

> 💡 GPU-Sensoren erscheinen nur, wenn eine GPU vorhanden ist. Bei mehreren GPUs wird die erste erkannte GPU verwendet.

#### Arbeitsspeicher (RAM)

| Entity-ID | Name | Einheit | Beschreibung | Mögliche Werte |
|---|---|---|---|---|
| `sensor.ha_desklink_memory_percent` | Memory Usage | % | RAM-Auslastung (WMI Win32_OperatingSystem) | 0.0 – 100.0 |
| `sensor.ha_desklink_memory_used` | Memory Used | GB | RAM verwendet | z.B. 12.50 |
| `sensor.ha_desklink_memory_free` | Memory Free | GB | RAM frei | z.B. 3.50 |
| `sensor.ha_desklink_memory_total` | Memory Total | GB | RAM gesamt (statisch) | z.B. 16.00 |

#### Festplatten / Laufwerke

Für **jedes** gefundene Festplatten-Laufwerk (C:, D:, E:, etc.) werden vier Sensoren erstellt:

| Entity-ID (Beispiel C:) | Name | Einheit | Beschreibung |
|---|---|---|---|
| `sensor.ha_desklink_disk_c_percent` | Disk C: Usage | % | Belegung in % |
| `sensor.ha_desklink_disk_c_free` | Disk C: Free | GB | Freier Speicher |
| `sensor.ha_desklink_disk_c_used` | Disk C: Used | GB | Verwendeter Speicher |
| `sensor.ha_desklink_disk_c_total` | Disk C: Total | GB | Gesamtkapazität |

> 💡 Weitere Laufwerke (D:, E:, etc.) werden automatisch erkannt. Der Laufwerksbuchstabe wird klein geschrieben und der Doppelpunkt entfernt: `disk_d_percent`, `disk_e_free`, etc.

#### System & Netzwerk

| Entity-ID | Name | Einheit | Beschreibung | Mögliche Werte |
|---|---|---|---|---|
| `sensor.ha_desklink_uptime` | Uptime | h | PC-Laufzeit in Stunden (TickCount64) | z.B. 4.5 |
| `sensor.ha_desklink_last_activity` | Last Activity | min | Minuten seit letzter Maus/Tastatur-Eingabe | z.B. 2.3 |
| `sensor.ha_desklink_idle_time` | Idle Time | s | Sekunden seit letzter Eingabe (GetLastInputInfo) | z.B. 138.5 |
| `sensor.ha_desklink_ip_address` | IP Address | – | Aktuelle IPv4-Adresse (WMI) | z.B. 192.168.1.100 |
| `sensor.ha_desklink_process_count` | Running Processes | – | Anzahl laufender Prozesse | z.B. 234 |
| `sensor.ha_desklink_page_file_percent` | Page File Usage | % | Auslagerungsdatei-Auslastung (WMI Win32_PageFileUsage) | z.B. 45.3 |
| `sensor.ha_desklink_network_upload` | Upload Speed | KB/s | Upload-Geschwindigkeit (PerformanceCounter, erste nicht-Loopback NIC) | z.B. 125.5 |
| `sensor.ha_desklink_network_download` | Download Speed | KB/s | Download-Geschwindigkeit | z.B. 2300.8 |
| `sensor.ha_desklink_bluetooth_devices_connected` | Bluetooth Devices Connected | – | Anzahl verbundener Bluetooth-Geräte (PowerShell Get-PnpDevice) | z.B. 3 |

#### Binäre Sensoren (on/off)

| Entity-ID | Name | Beschreibung | Mögliche Werte |
|---|---|---|---|
| `binary_sensor.ha_desklink_connectivity` | Connectivity | Ping zu HA-Host (Fallback: 8.8.8.8) | `on` / `off` |
| `binary_sensor.ha_desklink_audio_mute` | Audio Mute | Stummschaltung des System-Audio (IAudioEndpointVolume COM) | `on` / `off` |
| `binary_sensor.ha_desklink_mic_active` | Microphone Active | Mikrofon in Benutzung (AudioSessionManager COM) | `on` / `off` |
| `binary_sensor.ha_desklink_webcam_active` | Webcam Active | Webcam aktiv (WMI Win32_PnPEntity Image/Camera) | `on` / `off` |
| `binary_sensor.ha_desklink_presence` | Presence | Präsenz: on wenn idle_time < 300s UND connectivity = on | `on` / `off` |
| `binary_sensor.ha_desklink_pc_status` | PC Status | on solange App läuft (beim Beenden: off) | `on` / `off` |

#### Audio & Helligkeit

| Entity-ID | Name | Einheit | Beschreibung | Mögliche Werte |
|---|---|---|---|---|
| `sensor.ha_desklink_audio_volume` | Audio Volume | % | System-Lautstärke (IAudioEndpointVolume COM) | 0 – 100 |
| `sensor.ha_desklink_brightness` | Brightness | % | Bildschirmhelligkeit (WMI WmiMonitorBrightness, nur Laptops) | 0 – 100 |

#### WLAN

| Entity-ID | Name | Einheit | Beschreibung | Mögliche Werte |
|---|---|---|---|---|
| `sensor.ha_desklink_wifi_ssid` | WiFi Network | – | Verbundenes WLAN-Netzwerk (WMI Win32_NetworkConnection) | z.B. „MeinWLAN" |
| `sensor.ha_desklink_wifi_signal` | WiFi Signal | % | WLAN-Signalstärke (netsh wlan show interfaces) | 0 – 100 |

#### Bildschirm & Fenster

| Entity-ID | Name | Einheit | Beschreibung | Mögliche Werte |
|---|---|---|---|---|
| `sensor.ha_desklink_active_window` | Active Window | – | Titel des aktiven Fensters (GetForegroundWindow) | z.B. „Google Chrome" |
| `sensor.ha_desklink_fullscreen` | Fullscreen | – | Vollbild-Modus erkannt (Fenstergröße vs. Monitor) | `on` / `off` |
| `sensor.ha_desklink_monitor_layout` | Monitor Layout | – | Monitor-Konfiguration | „1", „1+2", „1+2+3" |

#### Akku (nur Laptops)

| Entity-ID | Name | Einheit | Beschreibung | Mögliche Werte |
|---|---|---|---|---|
| `sensor.ha_desklink_battery` | Battery | % | Akkustand (WMI Win32_Battery) | 0 – 100 |

#### Lüfter

| Entity-ID | Name | Einheit | Beschreibung | Mögliche Werte |
|---|---|---|---|---|
| `sensor.ha_desklink_gpu_fan_speed` | GPU Fan Speed | % | GPU-Lüfter (nvidia-smi, nur NVIDIA) | 0 – 100 |
| `sensor.ha_desklink_fan_*` | Fan: * | RPM | System-Lüfter (WMI Win32_Fan, selten verfügbar) | z.B. 1500 |

#### App-Version

| Entity-ID | Name | Beschreibung |
|---|---|---|
| `sensor.ha_desklink_ha_desklink_version` | HA DeskLink Version | Aktuelle App-Version (Assembly.Version) |

### Sensoren in HA verwenden

#### Beispiel: Dashboard-Karte für CPU-Temperatur

```yaml
type: gauge
entity: sensor.ha_desklink_cpu_temperature
name: CPU Temperatur
unit: °C
min: 20
max: 100
severity:
  green: 0
  yellow: 70
  red: 85
```

#### Beispiel: Automatisierung bei hoher CPU-Temperatur

```yaml
automation:
  - alias: "CPU-Temperatur Warnung"
    trigger:
      - platform: numeric_state
        entity_id: sensor.ha_desklink_cpu_temperature
        above: 85
        for:
          minutes: 5
    action:
      - service: notify.mobile_app_ha_desklink
        data:
          title: "⚠️ CPU zu heiß!"
          message: "Die CPU-Temperatur beträgt {{ states('sensor.ha_desklink_cpu_temperature') }}°C"
```

#### Beispiel: PC nur herunterfahren, wenn niemand am PC ist

```yaml
automation:
  - alias: "PC bei Inaktivität herunterfahren"
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
          title: "PC herunterfahren"
          message: "PC wurde 30+ Minuten nicht benutzt."
          data:
            command: "shutdown"
```

---

## 3. PC-Befehle aus Home Assistant

HA DeskLink empfängt Befehle über **Benachrichtigungen** – genau wie die Handy-App. Es wird keine Extra-Integration in HA benötigt. Befehle werden im `data`-Feld der Notification übergeben.

### Alle verfügbaren Befehle

| Befehl | Schreibweise | Wirkung |
|---|---|---|
| Herunterfahren | `shutdown` | Fährt den PC in 30 Sekunden herunter (`shutdown /s /t 30`) |
| Neustarten | `restart` oder `reboot` | Startet den PC in 30 Sekunden neu (`shutdown /r /t 30`) |
| Ruhezustand | `hibernate` | Versetzt den PC in den Ruhezustand (SetSuspendState) |
| Energie sparen | `sleep` | Versetzt den PC in den Energiesparmodus (SetSuspendState) |
| PC sperren | `lock_screen` oder `lock` | Sperrt den Windows-Bildschirm (LockWorkStation) |
| Lautstärke stumm | `volume_mute` oder `mute` | Schaltet den Ton stumm/entstummt (ToggleMute) |
| Lautstärke lauter | `volume_up` | Erhöht die Lautstärke um ~10% (5× VK_VOLUME_UP) |
| Lautstärke leiser | `volume_down` | Verringert die Lautstärke um ~10% (5× VK_VOLUME_DOWN) |
| Media Play/Pause | `media_play_pause` | Play/Pause für Medienwiedergabe (VK_MEDIA_PLAY_PAUSE) |
| Media Nächster | `media_next` | Nächster Titel (VK_MEDIA_NEXT_TRACK) |
| Media Vorheriger | `media_previous` | Vorheriger Titel (VK_MEDIA_PREV_TRACK) |
| Helligkeit rauf | `brightness_up` | Erhöht Bildschirmhelligkeit um ~10% (nur Laptops) |
| Helligkeit runter | `brightness_down` | Verringert Bildschirmhelligkeit um ~10% (nur Laptops) |
| Helligkeit setzen | `brightness:50` | Setzt Helligkeit auf Wert 0-100 (nur Laptops, WmiSetBrightness) |
| Monitor an | `monitor_on` | Schaltet den Monitor an (SC_MONITORPOWER -1) |
| Monitor aus | `monitor_off` | Schaltet den Monitor aus (SC_MONITORPOWER 2) |
| Bildschirmfoto | `screenshot` | Screenshot + Upload als HA-Event (CopyFromScreen → PNG → Base64) |
| Bildschirmfoto speichern | `screenshot_save` | Wie screenshot, speichert zusätzlich lokal |
| Snipping Tool | `snipping_tool` | Öffnet Windows Snipping Tool (Win+Shift+S) |
| Text-to-Speech | `tts:Hallo Welt` | Spricht den Text über Windows SAPI |
| App starten | `launch:spotify` | Startet eine konfigurierte App (siehe [App Launchers](#7-app-launchers)) |
| Custom Command | (eigener Name) | Führt ein konfiguriertes Skript aus (siehe [Custom Commands](#6-custom-commands)) |
| Nachricht | *(kein command)* | Zeigt nur eine Benachrichtigung an |

> ⚠️ **Helligkeits-Befehle** (`brightness_up`, `brightness_down`, `brightness:XX`) funktionieren **nur auf Laptops** mit integriertem Display. An Desktop-PCs mit externen Monitoren werden die Befehle ignoriert.

### YAML-Beispiele

#### Herunterfahren

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "PC herunterfahren"
  message: "Der PC wird in 30 Sekunden heruntergefahren"
  data:
    command: "shutdown"
```

#### Neustarten

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "PC neustarten"
  message: "Der PC wird neu gestartet"
  data:
    command: "restart"
```

#### Ruhezustand

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Ruhezustand"
  message: "PC geht in den Ruhezustand"
  data:
    command: "hibernate"
```

#### PC sperren

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "PC sperren"
  message: "Der PC wird gesperrt"
  data:
    command: "lock_screen"
```

#### Lautstärke stumm schalten

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Lautstärke"
  message: "Ton stumm geschaltet"
  data:
    command: "volume_mute"
```

#### Lauter

```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Lauter"
  data:
    command: "volume_up"
```

#### Mediensteuerung – Play/Pause

```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Play/Pause"
  data:
    command: "media_play_pause"
```

#### Helligkeit setzen (nur Laptops)

```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Helligkeit auf 50%"
  data:
    command: "brightness:50"
```

#### Monitor ausschalten

```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Monitor aus"
  data:
    command: "monitor_off"
```

#### Screenshot machen

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Screenshot"
  message: "Bildschirmfoto wird erstellt..."
  data:
    command: "screenshot"
```

> 💡 Der Screenshot wird als Event `ha_desklink_screenshot` mit Base64-Bild an HA gesendet. In HA kannst du das Event abfangen und das Bild speichern oder anzeigen.

#### Text-to-Speech

```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Sprich"
  data:
    command: "tts:Die Waschmaschine ist fertig!"
```

#### Einfache Benachrichtigung (ohne Befehl)

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Erinnerung"
  message: "Müll rausbringen nicht vergessen!"
```

### Automatisierung in HA

#### PC um 22 Uhr herunterfahren (nur wenn PC online)

```yaml
automation:
  - alias: "PC um 22 Uhr herunterfahren"
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
          title: "Gute Nacht!"
          message: "Der PC wird jetzt heruntergefahren."
          data:
            command: "shutdown"
```

#### PC bei Inaktivität in den Energiesparmodus

```yaml
automation:
  - alias: "PC bei Inaktivität schlafen legen"
    trigger:
      - platform: numeric_state
        entity_id: sensor.ha_desklink_idle_time
        above: 1800
    action:
      - service: notify.mobile_app_ha_desklink
        data:
          title: "Energiesparen"
          message: "PC wird in den Energiesparmodus versetzt."
          data:
            command: "sleep"
```

### Dashboard-Button in HA

```yaml
type: button
name: "PC herunterfahren"
tap_action:
  action: call-service
  service: notify.mobile_app_ha_desklink
  service_data:
    title: "PC herunterfahren"
    message: "Wird heruntergefahren..."
    data:
      command: "shutdown"
```

```yaml
type: button
name: "PC sperren"
icon: mdi:lock
tap_action:
  action: call-service
  service: notify.mobile_app_ha_desklink
  service_data:
    message: "PC wird gesperrt"
    data:
      command: "lock_screen"
```

---

## 4. Actionable Notifications

Ab Version 3.0 unterstützt HA DeskLink **Actionable Notifications** – Benachrichtigungen mit interaktiven Aktions-Buttons. Auf Windows wird ein modernes WinForms-Dialog mit abgerundeten Ecken angezeigt.

### Funktionsweise

- Die Benachrichtigung enthält eine Liste von `actions` (Buttons)
- Jeder Button hat einen `action`-Key, einen `title` (Beschriftung) und optional einen `command`
- Beim Klick auf einen Button wird der zugehörige `command` ausgeführt
- `command_on_action` ist ein Fallback-Befehl, der ausgeführt wird, wenn ein Button keinen eigenen `command` hat

### YAML-Beispiel: PC herunterfahren mit Bestätigung

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "PC herunterfahren?"
  message: "Soll der PC wirklich heruntergefahren werden?"
  data:
    actions:
      - action: SHUTDOWN
        title: "Ausschalten"
        command: shutdown
      - action: CANCEL
        title: "Abbrechen"
    command_on_action: shutdown
```

### YAML-Beispiel: Mediensteuerung mit Buttons

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Mediensteuerung"
  message: "Was möchtest du tun?"
  data:
    actions:
      - action: PLAY_PAUSE
        title: "Play/Pause"
        command: media_play_pause
      - action: NEXT
        title: "Weiter"
        command: media_next
      - action: PREV
        title: "Zurück"
        command: media_previous
      - action: MUTE
        title: "Stumm"
        command: volume_mute
```

### YAML-Beispiel: Energiespar-Optionen

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Energie sparen"
  message: "Wähle eine Option:"
  data:
    actions:
      - action: SLEEP
        title: "Energiesparen"
        command: sleep
      - action: HIBERNATE
        title: "Ruhezustand"
        command: hibernate
      - action: SHUTDOWN
        title: "Herunterfahren"
        command: shutdown
      - action: CANCEL
        title: "Abbrechen"
```

### Felder im Detail

| Feld | Typ | Beschreibung |
|---|---|---|
| `actions` | Liste | Liste der Aktions-Buttons |
| `actions[].action` | String | Eindeutiger Aktions-Key (z.B. „SHUTDOWN") |
| `actions[].title` | String | Beschriftung des Buttons |
| `actions[].command` | String | Befehl, der beim Klick ausgeführt wird (optional) |
| `command_on_action` | String | Fallback-Befehl für Buttons ohne eigenes `command` |

---

## 5. Quick Actions

Quick Actions erlauben es, **Home Assistant Entities direkt vom PC aus umzuschalten** – per globalen Hotkey oder über das Tray-Icon-Menü.

### Funktionsweise

- Beim Drücken des Quick Actions-Hotkeys öffnet sich ein Popup mit allen konfigurierten Entities
- Beim Klick auf ein Entity wird `homeassistant.toggle` an HA gesendet
- Die Entities werden in den Einstellungen konfiguriert

### Standard-Hotkeys

| Aktion | Standard-Hotkey | Beschreibung |
|---|---|---|
| Quick Actions | `Ctrl+Shift+H` | Öffnet das Quick Actions Popup |
| Dashboard | `Ctrl+Shift+D` | Öffnet das WebView2 Dashboard |
| Einstellungen | `Ctrl+Shift+S` | Öffnet das Einstellungsfenster |

> 💡 Alle Hotkeys sind konfigurierbar (Modifier + Taste). Siehe [Einstellungen → Tastenkombinationen](#tastenkombinationen).

### Konfiguration über Einstellungen

1. Tray-Icon → Rechtsklick → **Einstellungen**
2. Gehe zu **⚡ Quick Actions** (Sidebar)
3. Klicke **„Hinzufügen"** und wähle eine Entity aus der Dropdown-Liste (lädt alle HA Entities automatisch)
4. Gib einen Anzeigenamen ein
5. Speichern

### Konfiguration über config.json

Quick Actions werden als JSON-Array gespeichert:

```json
{
  "QuickActions": "[{\"entityId\":\"light.wohnzimmer\",\"name\":\"Wohnzimmerlicht\"},{\"entityId\":\"switch.steckdose\",\"name\":\"Steckdose\"}]"
}
```

### YAML-Beispiel: Automatisierung, die Quick Actions nutzt

Quick Actions senden `homeassistant.toggle` an HA. Die folgende Automatisierung reagiert darauf:

```yaml
automation:
  - alias: "Reagiere auf Quick Action Toggle"
    trigger:
      - platform: state
        entity_id: light.wohnzimmer
    action:
      - service: notify.mobile_app_ha_desklink
        data:
          title: "Wohnzimmer"
          message: "Licht ist jetzt {{ states('light.wohnzimmer') }}"
```

---

## 6. Custom Commands

Custom Commands erlauben es, **eigene Skripte oder Befehle** zu definieren, die von Home Assistant ausgelöst werden können.

### JSON-Format

Custom Commands werden in der `config.json` als JSON-Array gespeichert:

```json
{
  "CustomCommands": "[{\"command\":\"start_streaming\",\"script\":\"C:\\\\Scripts\\\\start_streaming.bat\",\"name\":\"Streaming starten\"}]"
}
```

Jeder Eintrag hat folgende Felder:

| Feld | Typ | Beschreibung |
|---|---|---|
| `command` | String | Der Befehlsname, der von HA gesendet wird (z.B. „start_streaming") |
| `script` | String | Pfad zum Skript oder Befehl, der ausgeführt wird (z.B. `C:\\Scripts\\stream.bat`) |
| `name` | String | Anzeigename (optional) |

### Beispiele

#### Beispiel 1: Backup-Skript starten

**config.json Eintrag:**
```json
{
  "CustomCommands": "[{\"command\":\"run_backup\",\"script\":\"C:\\\\Scripts\\\\backup.bat\",\"name\":\"Backup starten\"}]"
}
```

**HA YAML:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Backup"
  message: "Backup wird gestartet..."
  data:
    command: "run_backup"
```

#### Beispiel 2: Docker-Container neustarten

**config.json Eintrag:**
```json
{
  "CustomCommands": "[{\"command\":\"restart_docker\",\"script\":\"docker restart homeassistant\",\"name\":\"HA Docker Neustart\"}]"
}
```

**HA YAML:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Docker-Container wird neugestartet"
  data:
    command: "restart_docker"
```

#### Beispiel 3: Mehrere Custom Commands

```json
{
  "CustomCommands": "[{\"command\":\"clear_temp\",\"script\":\"C:\\\\Scripts\\\\clear_temp.bat\",\"name\":\"Temp-Dateien löschen\"},{\"command\":\"defrag_c\",\"script\":\"defrag C: /U /V\",\"name\":\"Defragmentieren\"},{\"command\":\"ipconfig_flush\",\"script\":\"ipconfig /flushdns\",\"name\":\"DNS-Cache leeren\"}]"
}
```

**HA YAML für DNS-Cache leeren:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "DNS-Cache wird geleert"
  data:
    command: "ipconfig_flush"
```

> ⚠️ Custom Commands werden über `cmd /c <script>` ausgeführt. Das bedeutet, jeder Windows-Befehl oder jedes Batch-Skript kann ausgeführt werden.

---

## 7. App Launchers

App Launchers erlauben es, **Anwendungen vom PC aus über Home Assistant zu starten**.

### JSON-Format

App Launchers werden in der `config.json` als JSON-Array gespeichert:

```json
{
  "AppLaunchers": "[{\"command\":\"launch_spotify\",\"path\":\"spotify\",\"name\":\"Spotify\"}]"
}
```

Jeder Eintrag hat folgende Felder:

| Feld | Typ | Beschreibung |
|---|---|---|
| `command` | String | Der Befehlsname (mit `launch:` Präfix von HA gesendet) |
| `path` | String | Pfad zur App oder ausführbaren Datei |
| `name` | String | Anzeigename (optional) |

### Beispiele

#### Beispiel 1: Spotify starten

**config.json Eintrag:**
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
  message: "Spotify wird gestartet..."
  data:
    command: "launch:launch_spotify"
```

#### Beispiel 2: Mehrere Apps

```json
{
  "AppLaunchers": "[{\"command\":\"spotify\",\"path\":\"spotify\",\"name\":\"Spotify\"},{\"command\":\"steam\",\"path\":\"C:\\\\Program Files (x86)\\\\Steam\\\\Steam.exe\",\"name\":\"Steam\"},{\"command\":\"notepad\",\"path\":\"notepad.exe\",\"name\":\"Editor\"},{\"command\":\"calc\",\"path\":\"calc.exe\",\"name\":\"Taschenrechner\"}]"
}
```

**HA YAML für Steam starten:**
```yaml
service: notify.mobile_app_ha_desklink
data:
  message: "Steam wird gestartet"
  data:
    command: "launch:steam"
```

> 💡 Apps werden über `Process.Start` mit `UseShellExecute=true` gestartet. Das bedeutet, UWP-Apps (z.B. Spotify aus dem Microsoft Store) und Protokoll-Handler (z.B. `spotify:`, `steam://`) funktionieren ebenfalls.

---

## 8. Benachrichtigungen

HA DeskLink zeigt **Toast-Notifications** auf dem Bildschirm an, wenn Home Assistant eine Benachrichtigung sendet.

### Funktionsweise

- Benachrichtigungen werden als moderne **dunkel-theme Toasts** angezeigt
- Abgerundete Ecken (GraphicsPath, kein P/Invoke)
- Akzentfarbige linke Leiste (Blau für normal, Grün für Verbindungsstatus)
- **Auto-Schließen nach 8 Sekunden** – außer der Mauszeiger ist über dem Toast (Pause bei Hover)
- Title, Message und Timestamp werden angezeigt
- Schließen-Button (✕) oben rechts

### Benachrichtigung senden

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Waschmaschine fertig"
  message: "Die Waschmaschine ist fertig! Bitte Wäsche aufhängen."
```

### Position konfigurieren

Die Position der Toast-Notifications kann in den Einstellungen konfiguriert werden:

| Position | Beschreibung |
|---|---|
| `bottom_left` | Unten links (Standard) |
| `bottom_right` | Unten rechts |
| `top_left` | Oben links |
| `top_right` | Oben rechts |

**Einstellungen → Benachrichtigungen → Position**

### Monitor konfigurieren

Bei Multi-Monitor-Setups kann der Monitor für Benachrichtigungen gewählt werden:

| Wert | Beschreibung |
|---|---|
| `0` | Primärer Monitor (Standard) |
| `1` | Zweiter Monitor |
| `2` | Dritter Monitor |
| etc. | |

**Einstellungen → Benachrichtigungen → Monitor**

### Beispiel: Benachrichtigung mit Aktion

```yaml
service: notify.mobile_app_ha_desklink
data:
  title: "Türklingel"
  message: "Jemand ist an der Tür!"
  data:
    actions:
      - action: OPEN_CAMERA
        title: "Kamera öffnen"
        command: launch:launch_camera
      - action: UNLOCK
        title: "Tür öffnen"
        command: launch:open_door
```

---

## 9. WebView2 Dashboard

HA DeskLink enthält ein **eingebettetes Dashboard** basierend auf Microsoft WebView2. Es zeigt deine Home Assistant Instanz direkt in der App an – wie ein Browser, aber ohne Browser.

### Einrichtung

1. **Öffnen:** Tray-Icon → Doppelklick, oder Rechtsklick → „Dashboard", oder Hotkey `Ctrl+Shift+D`
2. **WebView2 Runtime:** Falls WebView2 nicht installiert ist, erscheint ein Dialog mit der Option zum automatischen Download
   - Download-URL: `https://go.microsoft.com/fwlink/p/?LinkId=2124703`
   - Nach der Installation muss HA DeskLink neu gestartet werden
3. **Login:** Beim ersten Öffnen erscheint das normale HA-Login-Formular
   - Melde dich mit Benutzername & Passwort an (wie im Browser)
4. **Session bleibt erhalten:** WebView2 speichert die Session im Verzeichnis `%APPDATA%\HA_DeskLink\WebView2Data\`
   - Nach dem ersten Login musst du dich nicht erneut anmelden
   - Die Session überlebt App-Neustarts

### Verwendung

- Das Dashboard öffnet sich in einem 1300×850 Pixel großen Fenster
- Minimale Größe: 800×600
- Du kannst HA wie gewohnt bedienen (Dashboards, Einstellungen, Automatisierungen, etc.)
- Rechtsklick-Menüs sind aktiviert, DevTools sind deaktiviert
- Status-Bar ist ausgeblendet

### Fallback

Wenn WebView2 nicht installiert ist und nicht heruntergeladen wird, öffnet sich HA im **Standard-Browser**.

### Programmatisches Öffnen

```yaml
# HA kann das Dashboard nicht direkt öffnen, aber du kannst
# eine Benachrichtigung senden, die den Benutzer ans Desktop erinnert:
service: notify.mobile_app_ha_desklink
data:
  title: "Dashboard öffnen"
  message: "Drücke Ctrl+Shift+D auf deinem PC um das Dashboard zu öffnen."
```

---

## 10. MQTT (optionale Features)

HA DeskLink v5.0 unterstützt **optionales MQTT** für erweiterte Features. MQTT ist **optional** – die App funktioniert auch ohne MQTT vollständig.

### MQTT-Features

| Feature | Beschreibung |
|---|---|
| 🔊 **Media Player Entity** | PC erscheint als Media Player in HA mit now-playing Info, Play/Pause und Lautstärke-Regelung |
| 📡 **PC Status Binary Sensor** | Sofortige Online/Offline-Erkennung via Last Will Testament (LWT) |
| ⚡ **Befehle an schlafenden PC** | MQTT-Befehle erreichen den PC auch im Energiesparmodus |
| 🔍 **Automatische Geräteerkennung** | Media Player und PC Status erscheinen automatisch in HA (MQTT Discovery) |
| 🔒 **Zuverlässige Verbindung** | Auto-Reconnect mit exponentiellem Backoff (1s, 2s, 4s, 8s, 16s, 30s max) |
| 🪄 **Zero-Config Setup** | Beim ersten Start wird automatisch nach Mosquitto gesucht |
| 🧭 **Smart Routing** | MQTT für Sensoren + Befehle, WebSocket bleibt für Benachrichtigungen |

### Konfiguration

MQTT kann im Setup-Wizard oder in den Einstellungen konfiguriert werden:

**Einstellungen → 📡 MQTT**

| Feld | Beschreibung | Standardwert |
|---|---|---|
| MQTT aktiviert | Checkbox, ob MQTT verwendet wird | Aus |
| Broker | MQTT-Broker Hostname/IP | (leer) |
| Port | MQTT-Port | 1883 |
| Benutzername | Optional, für Authentifizierung | (leer) |
| Passwort | Optional, für Authentifizierung | (leer) |
| SSL/TLS | TLS-Verschlüsselung (TLS 1.2/1.3) | Aus |
| Fallback Broker | Alternative Broker-Adresse für Auto-Config | (leer) |

### config.json Werte

```json
{
  "MqttEnabled": true,
  "MqttBroker": "192.168.1.100",
  "MqttPort": 1883,
  "MqttUsername": "desklink",
  "MqttPasswordEncrypted": "(DPAPI verschlüsselt)",
  "MqttUseSsl": false,
  "MqttAutoConfigured": false,
  "MqttBrokerFallback": ""
}
```

> 🔒 Das MQTT-Passwort wird mit **DPAPI** verschlüsselt gespeichert (wie der HA-Token).

### MQTT Topics

HA DeskLink verwendet folgende Topic-Struktur:

| Topic | Richtung | Beschreibung |
|---|---|---|
| `ha_desklink/{deviceId}/availability` | Publish | Online/Offline Status (LWT, retained) |
| `ha_desklink/{deviceId}/{component}/{objectId}/state` | Publish | Sensor-States |
| `ha_desklink/{deviceId}/media_player/state` | Publish | Media Player State (playing/paused/idle) |
| `ha_desklink/{deviceId}/media_player/attributes` | Publish | Media Player Attribute (Title, Artist, Album, Source) |
| `ha_desklink/{deviceId}/command/media` | Subscribe | Media-Befehle empfangen |
| `ha_desklink/{deviceId}/command/system` | Subscribe | System-Befehle empfangen |
| `homeassistant/{component}/{nodeId}/{objectId}/config` | Publish | MQTT Discovery Config (retained) |
| `homeassistant/status` | Subscribe | HA-Status (birth message) |

### Media Player

Der Media Player zeigt die aktuell wiedergegebene Musik/Video auf dem PC:

| Attribut | Beschreibung |
|---|---|
| State | `playing`, `paused`, `idle` |
| Title | Titel des aktuellen Tracks |
| Artist | Künstler |
| Album | Album |
| Source | App-Name (Spotify, Chrome, Firefox, Edge, VLC, etc.) |

Die Daten werden über die Windows **GlobalSystemMediaTransportControlsSessionManager** API (Windows 10+ build 18362+) gesammelt, mit PowerShell-Fallback.

### PC Status (LWT)

- Beim Start: `ha_desklink/{deviceId}/availability` = `online` (retained)
- Beim Beenden: `ha_desklink/{deviceId}/availability` = `offline` (retained)
- Bei Verbindungsverlust: LWT wird automatisch vom Broker gesendet

### MQTT in HA verwenden

```yaml
# Media Player in Dashboard
type: media-control
entity: media_player.ha_desklink_media_player
```

```yaml
# Automatisierung: Musik pausieren wenn PC gesperrt wird
automation:
  - alias: "Musik pausieren bei PC-Sperre"
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

HA DeskLink prüft automatisch auf neue Versionen und installiert diese.

### Funktionsweise

1. **Beim Start:** HA DeskLink prüft GitHub Releases auf eine neue Version
2. **Periodisch:** Alle 2 Stunden wird erneut geprüft
3. **Download:** Bei einer neuen Version wird der Installer heruntergeladen (`HA_DeskLink_Setup.exe`)
4. **Validierung:** Die Datei muss mindestens 1 MB groß sein (Schutz vor fehlerhaften Downloads)
5. **Installation:** Der Installer wird mit `/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS` ausgeführt
6. **Neustart:** Die alte App beendet sich, der Installer installiert die neue Version und startet die App neu

### Update-Schutz (Loop Prevention)

- Vor dem Update wird eine `.update_pending` Datei mit der aktuellen Version geschrieben
- Wenn die Datei existiert und die dortige Version >= der installierten Version ist, wird kein Update angeboten
- Nach erfolgreichem Update wird die Datei gelöscht
- Zusätzlicher 1-Stunden-Cooldown zwischen Update-Checks

### Update-Kanäle

| Kanal | Beschreibung | config.json Wert |
|---|---|---|
| **Stable** | Nur stabile Releases (Standard) | `"stable"` |
| **Prerelease** | Inkl. Beta/Vorab-Versionen | `"prerelease"` |

**Einstellungen → Allgemein → Update-Kanal**

### Manuelle Update-Prüfung

Tray-Icon → Rechtsklick → **„Nach Update suchen"**

- Wenn ein Update verfügbar ist, erscheint ein Dialog mit der Frage, ob es installiert werden soll
- Wenn kein Update verfügbar ist, erscheint „Du bist auf dem neuesten Stand."

---

## 12. Autostart

HA DeskLink kann sich automatisch beim Windows-Login starten.

### Task Scheduler (primäre Methode)

HA DeskLink verwendet den **Windows Task Scheduler** für den Autostart:

- **Task-Name:** `HA_DeskLink`
- **Trigger:** Bei Logon (`LogonTrigger`)
- **Privilegien:** Höchste verfügbar (`RunLevel: HighestAvailable`) – kein UAC-Prompt!
- **Priorität:** High (Priority: 2) – schnellster Start
- **Eigenschaften:**
  - `MultipleInstancesPolicy: IgnoreNew` – nur eine Instanz
  - `DisallowStartIfOnBatteries: false` – startet auch im Akkubetrieb
  - `StopIfGoingOnBatteries: false` – stoppt nicht beim Akkubetrieb
  - `StartWhenAvailable: true` – startet nach Verzögerung, wenn der geplante Zeitpunkt verpasst wurde

### Registry-Fallback

Wenn der Task Scheduler nicht verfügbar ist, wird die **Registry** verwendet:

- **Key:** `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
- **Wert:** `HA_DeskLink` = `"<Pfad zur HA_DeskLink.exe>"`
- ⚠️ Beim Registry-Autostart erscheint eine UAC-Abfrage, da die App als Administrator läuft

### Start-Menü-Verknüpfung

Zusätzlich wird eine **Start-Menü-Verknüpfung** erstellt:
- Pfad: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\HA DeskLink.lnk`

### Autostart aktivieren/deaktivieren

**Einstellungen → Allgemein → Autostart** (Checkbox)

Oder über die `config.json`:
```json
{
  "Autostart": true
}
```

Wenn aktiviert, wird `Autostart.Enable()` aufgerufen (Task Scheduler + Start-Menü-Verknüpfung).
Wenn deaktiviert, wird `Autostart.Disable()` aufgerufen (Task Scheduler + Registry-Eintrag werden entfernt).

---

## 13. Einstellungen

Das Einstellungsfenster hat eine Sidebar-Navigation mit 7 Bereichen. Öffnen über:
- Tray-Icon → Rechtsklick → **Einstellungen**
- Hotkey: `Ctrl+Shift+S`

### Verbindung

| Einstellung | Typ | Standardwert | Beschreibung |
|---|---|---|---|
| HA URL | TextBox | `https://homeassistant.local:8123` | URL der Home Assistant Instanz |
| Long-Lived Token | TextBox (Passwort) | (leer) | Long-Lived Access Token aus HA |
| SSL-Zertifikat prüfen | CheckBox | (leer) | Ob SSL-Zertifikate validiert werden; bei self-signed Zertifikaten deaktivieren |
| Neu verbinden | Button | – | Registriert die App neu bei HA |

### Allgemein

| Einstellung | Typ | Standardwert | Beschreibung |
|---|---|---|---|
| Autostart | CheckBox | Aktiviert | Startet HA DeskLink automatisch beim Windows-Login |
| Sensor-Intervall | NumericUpDown | 30 (10-300) | Sekunden zwischen Sensor-Updates |
| Update-Kanal | ComboBox | Stable | Stable oder Prerelease |
| Geräte-ID zurücksetzen | Button | – | Generiert eine neue Geräte-ID (für neue Registrierung) |
| Sensoren neu registrieren | Button | – | Registriert alle Sensoren neu bei HA (hilft nach Updates) |

### Erscheinungsbild

| Einstellung | Typ | Standardwert | Beschreibung |
|---|---|---|---|
| Sprache | ComboBox | Deutsch | UI-Sprache (15 Sprachen verfügbar) |
| Design | ComboBox | System | System (folgt Windows), Hell, oder Dunkel |

### Benachrichtigungen

| Einstellung | Typ | Standardwert | Beschreibung |
|---|---|---|---|
| Position | ComboBox | Unten links | Position der Toast-Notifications |
| Monitor | ComboBox | 0 (Primär) | Monitor für Benachrichtigungen (0-N) |

Verfügbare Positionen:
- `bottom_left` – Unten links (Standard)
- `bottom_right` – Unten rechts
- `top_left` – Oben links
- `top_right` – Oben rechts

### Tastenkombinationen

| Einstellung | Typ | Standardwert | Beschreibung |
|---|---|---|---|
| Quick Actions Modifier | ComboBox | Ctrl+Shift | Modifikator-Tasten für Quick Actions |
| Quick Actions Taste | ComboBox | H | Taste für Quick Actions |
| Dashboard Modifier | ComboBox | Ctrl+Shift | Modifikator-Tasten für Dashboard |
| Dashboard Taste | ComboBox | D | Taste für Dashboard |
| Einstellungen Modifier | ComboBox | Ctrl+Shift | Modifikator-Tasten für Einstellungen |
| Einstellungen Taste | ComboBox | S | Taste für Einstellungen |

Verfügbare Modifikatoren: `ctrl_shift`, `ctrl_alt`, `ctrl`, `alt`, `shift`, `none`

> ⚠️ Wenn Modifier auf `none` gesetzt ist, wird der jeweilige Hotkey deaktiviert.

### MQTT

| Einstellung | Typ | Standardwert | Beschreibung |
|---|---|---|---|
| MQTT aktiviert | CheckBox | Aus | Aktiviert/deaktiviert MQTT |
| Broker | TextBox | (leer) | MQTT-Broker Hostname/IP |
| Port | TextBox | 1883 | MQTT-Port |
| Benutzername | TextBox | (leer) | Optional, für Authentifizierung |
| Passwort | TextBox (Passwort) | (leer) | Optional, für Authentifizierung |
| SSL/TLS | CheckBox | Aus | TLS-Verschlüsselung |
| Fallback Broker | TextBox | (leer) | Alternative Broker-Adresse |

### Quick Actions

Hier können Quick Actions (HA-Entity-Toggles) hinzugefügt, entfernt und sortiert werden. Die Entity-Liste wird automatisch von HA geladen.

### Webhook Bind Address

| Einstellung | Typ | Standardwert | Beschreibung |
|---|---|---|---|
| WebhookBindAddress | String | `+` | Bind-Adresse für den Webhook-Server (Port 59123) |

- `+` = Alle Netzwerk-Interfaces (Standard, HA kann von überall zugreifen)
- `localhost` = Nur lokal (sicherer, wenn HA auf dem gleichen PC läuft)

### config.json Übersicht

Alle Einstellungen werden in `%APPDATA%\HA_DeskLink\config.json` gespeichert:

```json
{
  "HaUrl": "https://homeassistant.local:8123",
  "HaToken": "",
  "HaTokenEncrypted": "(DPAPI verschlüsselt)",
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

## 14. Sicherheit

### DPAPI-Verschlüsselung

HA DeskLink verschlüsselt sensible Daten mit der **Windows Data Protection API (DPAPI)**:

- **HA-Token:** Wird mit `ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser)` verschlüsselt
- **MQTT-Passwort:** Ebenfalls DPAPI-verschlüsselt
- **Speicherort:** `%APPDATA%\HA_DeskLink\config.json` – das Feld `HaToken` ist **immer leer** in der gespeicherten Datei, nur `HaTokenEncrypted` enthält den verschlüsselten Wert

**Was bedeutet das?**
- Der Token kann **nur vom selben Windows-Benutzer auf demselben PC** entschlüsselt werden
- Ein Angreifer, der die `config.json` kopiert, kann den Token **nicht** auf einem anderen PC oder als anderer Benutzer entschlüsseln
- DPAPI ist in Windows integriert und benötigt keine zusätzliche Software

### Migration von Klartext zu verschlüsselt

Wenn eine alte `config.json` mit Klartext-Token gefunden wird, passiert Folgendes beim Laden:
1. Der Klartext-Token (`HaToken`) wird erkannt
2. Er wird mit DPAPI verschlüsselt und in `HaTokenEncrypted` gespeichert
3. Das `HaToken`-Feld wird geleert
4. Die Datei wird sofort neu gespeichert

### Login-Retry-Limit

- MQTT: Nach 10 aufeinanderfolgenden Verbindungsfehlern wird 60 Sekunden pausiert, dann neu versucht
- Auto-Reconnect mit exponentiellem Backoff: 1s, 2s, 4s, 8s, 16s, 30s (max)

### Admin-Rechte

- Der Installer benötigt Administrator-Rechte (`PrivilegesRequired=admin`)
- Die App startet automatisch als Administrator (für CPU/GPU-Temperatur über WMI)
- Autostart über Task Scheduler mit `HighestAvailable` Privilegien (kein UAC-Prompt bei jedem Start)
- **Ohne Admin-Rechte** funktioniert die App auch – nur CPU-Temperatur, GPU-Temperatur und Lüfter-Drehzahl fehlen

### Webhook-Sicherheit

- Der Webhook-Server (Port 59123) validiert den Token bei jeder Anfrage
- Token-Validierung mit `CryptographicOperations.FixedTimeEquals` (timing-safe, verhindert Timing-Angriffe)
- Token wird vorzugsweise aus dem `Authorization: Bearer` Header gelesen (sicherer)
- Fallback: Token im Query-String (weniger sicher, kann in Logs erscheinen)
- `WebhookBindAddress` kann auf `localhost` gesetzt werden, um den Zugriff auf die lokale Maschine zu beschränken

### TTS-Sicherheit

- Der TTS-Text wird vor der Ausführung escapet (`'` → `''`), um **Command-Injection** zu verhindern
- Der Text wird in single quotes gewickelt, bevor er an PowerShell übergeben wird

---

## 15. Sprachen

HA DeskLink unterstützt **15 Sprachen**. Die Sprache kann in den Einstellungen geändert werden:

**Einstellungen → Erscheinungsbild → Sprache**

| Code | Sprache (nativ) |
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

> 💡 Sprachen werden automatisch aus dem `Lang/` Ordner erkannt (alle `*.json` Dateien außer `languages.json`). Die Anzeigenamen werden aus `languages.json` gelesen. Fallback ist immer Deutsch.

---

## 16. Build & Entwicklung

### Voraussetzungen

- **.NET 8 SDK** (oder neuer)
- **InnoSetup** (für den Installer)
- **WebView2 SDK** (NuGet: `Microsoft.Web.WebView2`)
- **MQTTnet** (NuGet: `MQTTnet`)

### Build

```bash
# App kompilieren (self-contained, win-x64)
dotnet publish src/HaDeskLink -c Release -r win-x64 --self-contained -o publish

# Installer erstellen
iscc installer.iss
```

### Projekt-Struktur

```
src/HaDeskLink/
├── HaDeskLink.csproj    # Projekt-Datei
├── Program.cs           # Einstiegspunkt (Main, Setup-Wizard-Trigger)
├── DeskLinkApp.cs       # Hauptanwendung (Tray, Sensor-Loop, Update, Hotkeys)
├── Config.cs            # Konfiguration (JSON, DPAPI-Verschlüsselung)
├── SensorManager.cs      # Sensor-Datensammlung (WMI, PerformanceCounter, COM)
├── SensorData.cs         # Sensor-Modell
├── HaApiClient.cs       # HA mobile_app API-Client (Registrierung, Sensoren, Update)
├── HaWebSocketClient.cs  # WebSocket-Client für Push-Notifications
├── WebhookServer.cs      # HTTP-Listener für Befehle/Notifications
├── CommandHandler.cs     # Befehlsausführung (shutdown, volume, screenshot, etc.)
├── NotificationHandler.cs # Toast-Notification-Handler
├── DashboardWindow.cs    # WebView2 Dashboard-Fenster
├── SetupWizard.cs         # Ersteinrichtungs-Wizard
├── SettingsWindow.cs      # Einstellungs-Fenster
├── QuickActionWindow.cs   # Quick Actions Popup
├── QuickActionHandler.cs  # Globaler Hotkey-Handler
├── Autostart.cs           # Task Scheduler / Registry Autostart
├── Localization.cs        # JSON-basiertes Lokalisierungssystem
├── MqttClient.cs          # MQTT-Client (Discovery, LWT, Media Player)
├── MqttSetupHelper.cs     # MQTT Auto-Setup Helper
├── MediaPlayer.cs         # Media Player State (GSMT COM API)
├── Lang/                  # Sprachdateien (*.json)
│   ├── languages.json     # Sprachnamen (Code → nativer Name)
│   ├── de.json            # Deutsch
│   ├── en.json            # English
│   └── ...                # Weitere Sprachen
└── Assets/
    └── icon.ico           # App-Icon
```

### Versionierung

Ab v2.2.1 gelten plattformunabhängige Versionsnummern:

| Änderung | Beispiel | Erklärung |
|---|---|---|
| Bug Fix | 2.2.1 → 2.2.2 | Fehlerbehebung, nur betroffene Plattform |
| Neue Funktionen | 2.2.x → 3.0.0 | Neue Features, alle Plattformen gleichzeitig |

Die Versionsnummer wird aus der `VERSION` Datei im App-Verzeichnis gelesen (Fallback: Assembly-Version).

### Lizenz

GPL v3 – Copyright © 2026 Fabian Kirchweger

Dieses Programm ist freie Software. Wenn du es modifizierst oder verteilst, **musst** du die Änderungen unter derselben GPL v3 Lizenz veröffentlichen. Closed-source oder proprietäre Nutzung ist **nicht** erlaubt.

---

## 17. Fehlerbehebung

### Häufige Probleme und Lösungen

| Problem | Lösung |
|---|---|
| **Verbindung zu HA klappt nicht** | 1. HA URL prüfen (inkl. Port 8123)<br>2. Token prüfen (Long-Lived Access Token)<br>3. Firewall: Port 8123 freigeben<br>4. SSL-Prüfung deaktivieren bei self-signed Zertifikaten |
| **Sensoren fehlen in HA** | 1. 30-60 Sekunden warten (Sensoren werden beim Start registriert)<br>2. Gerät in HA öffnen (Einstellungen → Geräte & Dienste → mobile_app)<br>3. App neu starten<br>4. „Sensoren neu registrieren" in den Einstellungen |
| **CPU-Temperatur ist leer** | App als Administrator starten (WMI MSAcpi_ThermalZoneTemperature benötigt Admin-Rechte) |
| **GPU-Temperatur fehlt** | NVIDIA: nvidia-smi installiert? AMD: Radeon Software installiert? Intel: WMI thermal zone verfügbar? |
| **Webcam-Sensor immer „off"** | Auf Windows ist die Webcam-Erkennung unzuverlässig (WMI Win32_PnPEntity). Der Sensor zeigt nur, ob das Gerät vorhanden ist, nicht ob es aktiv verwendet wird. |
| **SSL-Fehler** | SSL-Prüfung in Einstellungen deaktivieren („SSL-Zertifikat prüfen" abwählen) |
| **Token konnte nicht geladen werden** | DPAPI-Entschlüsselung fehlgeschlagen (anderer Benutzer oder andere Maschine). App neu einrichten (neuer Token). |
| **Benachrichtigungen erscheinen nicht** | 1. WebSocket-Verbindung prüfen (Tray-Icon zeigt Status)<br>2. Notification-Service prüfen: `notify.mobile_app_ha_desklink`<br>3. App läuft als Administrator? |
| **Hotkeys funktionieren nicht** | 1. Andere App blockiert den globalen Hotkey<br>2. Hotkey-Konfiguration in Einstellungen prüfen<br>3. App als Administrator starten |
| **MQTT verbindet nicht** | 1. Broker-Adresse und Port prüfen<br>2. Benutzername/Passwort prüfen<br>3. SSL-Option prüfen<br>4. Firewall: Port 1883 (oder konfigurierten Port) freigeben |
| **Auto-Update schlägt fehl** | 1. Internetverbindung prüfen<br>2. GitHub erreichbar?<br>3. Manuell von [Releases](https://github.com/TechFlipsi/ha-desklink-windows/releases/latest) herunterladen und als Administrator installieren |
| **Dashboard (WebView2) lädt nicht** | 1. WebView2 Runtime installieren (wird automatisch angeboten)<br>2. HA URL korrekt? |
| **App startet nicht automatisch** | 1. Einstellungen → Autostart aktiviert?<br>2. Task Scheduler: Task „HA_DeskLink" vorhanden? (`schtasks /query /tn "HA_DeskLink"`)<br>3. Registry: `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run\HA_DeskLink` |

### Log-Datei

Die Log-Datei befindet sich unter: `%APPDATA%\HA_DeskLink\error.log`

Öffnen über: Tray-Icon → Rechtsklick → **„Log öffnen"**

### Config-Verzeichnis

Alle Konfigurationsdateien liegen in: `%APPDATA%\HA_DeskLink\`

| Datei | Beschreibung |
|---|---|
| `config.json` | Hauptkonfiguration (Einstellungen, Token verschlüsselt) |
| `registration.json` | HA-Registrierung (webhook_id, device_id, cloud_url) |
| `error.log` | Log-Datei |
| `.update_pending` | Marker-Datei während eines Updates |
| `WebView2Data/` | WebView2 Session-Daten (Login-Cookies) |
| `device_id.txt` | Temporäre Datei für Geräte-ID-Reset |

### Geräte-ID zurücksetzen

Wenn die App nicht mehr korrekt bei HA registriert ist (z.B. nach einem PC-Wechsel oder Windows-Neuinstallation):

1. Einstellungen → Allgemein → **„Geräte-ID zurücksetzen"**
2. Es wird eine neue UUID generiert und in `device_id.txt` geschrieben
3. Beim nächsten Start registriert sich die App mit der neuen ID bei HA
4. Das alte Gerät in HA muss manuell gelöscht werden

### Support

- 💬 **Discord:** [discord.com/invite/zHPhQ7EaqH](https://discord.com/invite/zHPhQ7EaqH)
- 🐛 **GitHub Issues:** [github.com/TechFlipsi/ha-desklink-windows/issues](https://github.com/TechFlipsi/ha-desklink-windows/issues)

---

**Idee:** Fabian Kirchweger | **Code:** J.A.R.V.I.S. (Hermes Agent) | **Lizenz:** GPL v3

> Diese Anleitung wurde aus dem Sourcecode der Version 5.0.4 generiert. Alle Entity-Namen, Befehle und Konfigurationen entsprechen der tatsächlichen Implementierung.
#This project is now NeoHub
https://github.com/BrianHumlicek/NeoHub


# TLinkWebPortal

A real-time web portal for monitoring and controlling DSC PowerSeries NEO alarm panels via the ITv2 (TLink) protocol. Built with Blazor Server, MudBlazor, and MediatR.

[![Build and Publish Docker Image](https://github.com/BrianHumlicek/TLinkWebPortal/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/BrianHumlicek/TLinkWebPortal/actions/workflows/docker-publish.yml)

## 🐳 Container Images

| Registry | Image | Pull Command |
|---|---|---|
| GitHub Container Registry | [`ghcr.io/brianhumlicek/tlinkwebportal`](https://ghcr.io/brianhumlicek/tlinkwebportal) | `docker pull ghcr.io/brianhumlicek/tlinkwebportal:latest` |
| Docker Hub | [`brianhumlicek/tlinkwebportal`](https://hub.docker.com/r/brianhumlicek/tlinkwebportal) | `docker pull brianhumlicek/tlinkwebportal:latest` |

---

## 🚀 Quick Start

### Docker Compose (Recommended)

1. Create a `docker-compose.yml` file:

````````
version: '3.4'

services:
  tlinkwebportal:
    image: ghcr.io/brianhumlicek/tlinkwebportal:latest
    container_name: tlinkwebportal
    ports:
      - "80:80"
    volumes:
      - ./userSettings.json:/app/userSettings.json
      - tlinkwebportal_data:/app/data
    restart: unless-stopped

volumes:
  tlinkwebportal_data:
````````

2. Create a `userSettings.json` file (see [Configuration](#-configuration) below)

3. Start the container:

````````
docker-compose up -d
````````

4. Access the UI at `https://localhost:7013` or `http://localhost:5181`

### Docker Run

Alternatively, you can run the container directly using the Docker CLI:

````````
docker run -d --name tlinkwebportal \
  -p 80:80 \
  -v ./userSettings.json:/app/userSettings.json \
  -v tlinkwebportal_data:/app/data \
  ghcr.io/brianhumlicek/tlinkwebportal:latest
````````

## 🔧 Configuration

The application requires a `userSettings.json` file for configuration. A sample file is given below:

````````json
{
  "AppSettings": {
    "SomeSetting": "SomeValue"
  }
}
````````

Place your `userSettings.json` file in the same directory as your `docker-compose.yml` file, or mount it as a volume when running the Docker container.

### Advanced Configuration Options

In addition to the basic settings, the following advanced options are available:

| Setting | Description | Format | Default |
|---|---|---|---|
| `IntegrationAccessCodeType1` | Type 1 encryption access code (panel tag `[851][423]`) | 8 digits | `12345678` |
| `IntegrationAccessCodeType2` | Type 2 encryption key (panel tags `[851][700-703]`) | 32-character hex string | `1234567812345678...` |
| `IntegrationIdentificationNumber` | Integration ID (panel tag `[851][422]`) | 12 digits | `200328900112` |
| `ListenPort` | TCP port for panel connections | 1-65535 | `3072` |

> **💡 Tip:** Settings can also be configured via the **Settings** page in the web UI after startup.

---

## 🔧 DSC Panel Programming

The DSC PowerSeries NEO panel must be programmed to connect to your TLinkWebPortal instance. All programming is done in **Installer Mode**.

### Entering Installer Mode

1. Enter `*8` on the panel keypad
2. Enter your **Installer Code** (default is usually `5555`)
3. Navigate to section **[851]** (Integration / Alternate Communicator)

### Understanding Tags

DSC panels organize programming into **sections** and **tags**:

- **Sections** (e.g., `[851]`) are groups of related settings
- **Tags** (e.g., `[422]`, `[423]`) are individual fields within a section
- Some settings span multiple tags (e.g., Type 2 encryption uses tags `[700]` through `[703]`)

### Required Programming

Program the following tags in section **[851]**:

| Tag | Name | Description | Example |
|---|---|---|---|
| `[422]` | Integration ID | 12-digit identifier for this integration | `200328900112` |
| `[423]` | Type 1 Access Code (Slot 1) | 8-digit code for initial handshake | `12345678` |
| `[450]` | Type 1 Access Code (Slot 2) | Alternate 8-digit code | `12345678` |
| `[477]` | Type 1 Access Code (Slot 3) | Alternate 8-digit code | `12345678` |
| `[504]` | Type 1 Access Code (Slot 4) | Alternate 8-digit code | `12345678` |
| `[700]`-`[703]` | Type 2 Access Code | 32-character hex key (split across 4 tags) | `12345678` per tag |

### Programming Steps

1. **Enter Installer Mode:** `*8` → Installer Code → `[851]`

2. **Program Integration ID (`[422]`):**
   - Navigate to tag `[422]`
   - Enter your 12-digit Integration ID (e.g., `200328900112`)
   - Press `#` to save

3. **Program Type 1 Access Code (`[423]`):**
   - Navigate to tag `[423]`
   - Enter your 8-digit Type 1 code (e.g., `12345678`)
   - Press `#` to save
   - *Optional:* Program slots 2-4 at tags `[450]`, `[477]`, `[504]`

4. **Program Type 2 Access Code (`[700]`-`[703]`):**
   - Navigate to tag `[700]`
   - Enter characters 1-8 of your 32-character hex key
   - Press `#` and navigate to `[701]`
   - Enter characters 9-16
   - Press `#` and navigate to `[702]`
   - Enter characters 17-24
   - Press `#` and navigate to `[703]`
   - Enter characters 25-32
   - Press `#` to save

5. **Configure Network Settings:**
   - Set the panel's IP address for the TLinkWebPortal server (section `[801]`)
   - Set the port to `3072` (or your configured `ListenPort`)

6. **Exit Installer Mode:** Press `*99`

> ⚠️ **Critical:** The values programmed in the panel **must exactly match** the values in your `userSettings.json` file.

---

## 🏗️ Architecture

````````markdown
### Key Components

- **DSC.TLink** — Protocol library (ITv2 framing, encryption, session management)
- **MediatR** — Decouples panel messages from business logic via notification handlers
- **Notification Handlers** — Transform panel notifications into application state
- **State Services** — Singleton stores for partition/zone status
- **Event-Driven UI** — Blazor components subscribe to state change events (zero polling)

---

## 🌐 Web UI

| Page | Route | Description |
|---|---|---|
| **Home** | `/` | Dashboard showing all partitions and zones |
| **Zone Details** | `/zones/{sessionId}/{partition}` | Detailed view of zones for a specific partition |
| **Settings** | `/settings` | Edit application configuration (auto-saved to `userSettings.json`) |

### Features

- ✅ **Real-time updates** — UI refreshes instantly when zones open/close
- ✅ **Multi-session support** — Monitor multiple panels simultaneously
- ✅ **Visual zone indicators** — Color-coded chips with status icons
- ✅ **Event-driven architecture** — No polling, all updates triggered by actual panel events
- ✅ **Responsive design** — Works on desktop, tablet, and mobile

---

## 🔌 Ports

| Port | Protocol | Purpose |
|---|---|---|
| `5181` | HTTP | Web UI |
| `7013` | HTTPS | Web UI (secure) |
| `3072` | TCP | Panel ITv2 connection (configurable) |

---

## 🛠️ Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Local Development

````````

---

## 📖 Protocol Reference

For more details about the DSC ITv2 (TLink) protocol, see:
- [DSC TLink Library](https://github.com/BrianHumlicek/DSC-TLink) (underlying protocol implementation)
- DSC Integration Guide (consult your dealer/integrator documentation)

---

## 🐛 Troubleshooting

### Panel won't connect

1. Verify network connectivity: `ping <panel-ip>` and `ping <server-ip>` (from panel side if accessible)
2. Check firewall rules allow TCP port `3072` (or your configured `ListenPort`)
3. Verify panel is programmed with correct IP address and port
4. Check Docker logs: `docker logs tlinkwebportal`

### "Waiting for partition status data..."

- The panel has connected but hasn't sent partition status yet
- This is normal on first connection — wait a few seconds
- Partition status is broadcast automatically by the panel every few minutes

### Encryption errors in logs

- Verify the Integration ID, Type 1, and Type 2 codes in `userSettings.json` **exactly match** the panel programming
- Type 2 code is case-sensitive hex (A-F must match)

### Check logs

````````

---

## 🤝 Contributing

Issues and pull requests are welcome! For major changes, please open an issue first to discuss the proposed changes.

---

## 🙏 Acknowledgements

Built with:
- [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) — .NET web framework
- [MudBlazor](https://mudblazor.com/) — Material Design component library
- [MediatR](https://github.com/jbogard/MediatR) — In-process messaging
- [DSC TLink](https://github.com/BrianHumlicek/DSC-TLink) — ITv2 protocol library

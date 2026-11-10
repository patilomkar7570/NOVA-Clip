# NOVA Clip

<p align="center">
  <img src="assets/icon.ico" width="96" height="96" alt="NOVA Clip Logo" />
</p>

<p align="center">
  <strong>Everything you copy. Always within reach.</strong><br>
  <em>Version 1.0.0 — Production-Grade Windows Desktop Clipboard Manager</em>
</p>

---

## Overview

**NOVA Clip** is a modern, privacy-first Windows desktop clipboard manager built in C# and .NET 10 LTS on top of native Windows APIs and WPF. It is designed to work completely offline with zero telemetry, protecting every byte stored on disk using **Windows DPAPI** (`CurrentUser` scope) and local SQLite storage.

NOVA Clip is an independent product in the NOVA software family. It does not require any other NOVA application or external service to function.

---

## Signature Features

### 1. Floating Quick Panel
- **Instant Activation**: Accessible anywhere with a configurable global shortcut (default: `Ctrl + Shift + Space`).
- **Target App Focus Restoration**: Tracks the previously active window before taking focus so pasting works seamlessly.
- **Fast Search & Filter**: Real-time search across recent entries and quick filter chips (All, Starred, Text, Links, Images, Files).
- **Keyboard Navigation**: Navigate entries with arrow keys, paste with `Enter`, and dismiss with `Esc`.
- **Screen Corner Launcher**: Optional discreet screen pill button positioned in any corner of the display.

### 2. Advanced Multi-Item Paste Queue
- **Named Queues**: Create, save, rename, duplicate, and delete multiple named queues.
- **Copy Next**: Restores the next queued item to the Windows system clipboard and advances queue position (`Ctrl + Alt + C`).
- **Paste Next**: Automatically switches back to the target application, sends native `Ctrl + V` via Windows `SendInput`, advances the queue, and displays a feedback confirmation (`Ctrl + Alt + V`). Includes a safe, non-advancing fallback if input injection is restricted.
- **Paste All**: Concatenates multiple items using configurable separators (newline, double newline, comma, tab, or custom separator), or executes multi-file Windows operations.
- **Safe Item Snapshots**: Saved queues carry snapshots of payloads so deleting original clipboard history never breaks saved queues.

### 3. Native OLE Drag-and-Drop
- Drag items directly from either the Main Application or Floating Quick Panel into external Windows applications:
  - Text and links into Notepad, Word, or Web Browsers
  - Images into paint and photo editing software
  - Copied files into Windows File Explorer or document attachments

### 4. Enterprise-Grade Privacy & Security
- **100% Local & Offline**: Zero network calls, zero accounts, zero cloud dependencies, zero telemetry.
- **Protected at Rest**: All payload bytes (text, HTML, RTF, images, file paths) are encrypted on disk via **Windows DPAPI** (`ProtectedData` with `CurrentUser` scope).
- **Heuristic Sensitive Content Filtering**: Automatically skips capturing credit card numbers (verified via the Luhn algorithm), OTP / 2FA tokens, private keys, API secrets, and passwords.
- **Application Exclusions**: Block capture from sensitive processes (e.g., 1Password, Bitwarden, KeePass, KeePassXC).
- **Safe Diagnostic Logging**: Strictly prevents logging clipboard content, passwords, URLs, or image data.

---

## Keyboard Shortcuts

| Shortcut | Action | Scope |
| :--- | :--- | :--- |
| `Ctrl + Shift + Space` | Toggle Floating Quick Panel | Global (System-Wide) |
| `Ctrl + Alt + C` | Copy Next item in Paste Queue | Global (System-Wide) |
| `Ctrl + Alt + V` | Paste Next item into active window | Global (System-Wide) |
| `Up` / `Down` | Navigate list in Floating Panel | Floating Quick Panel |
| `Enter` | Paste selected item into active window | Floating Quick Panel |
| `Esc` | Hide Floating Quick Panel | Floating Quick Panel |

*All global shortcuts can be customized in Settings.*

---

## Architecture & Technology Stack

```
NOVA-Clip/
├── src/
│   ├── NOVA.Clip.Core/            # Models, Interfaces, Configuration, Enums
│   ├── NOVA.Clip.Infrastructure/  # Win32 Native, SQLite DB, DPAPI, Clipboard Monitor
│   ├── NOVA.Clip.App/             # WPF Windows, ViewModels, Controls, Themes
│   └── NOVA.Clip.Tests/           # Automated xUnit Test Suite (44 tests)
├── installer/                     # Inno Setup 6 Script (NovaClip.iss)
├── publish/                       # Self-contained release binaries
├── dist/                          # Installer & Portable zip distributions
├── assets/                        # Multi-resolution icons & graphics
├── docs/                          # Architecture, Installation, Troubleshooting
└── scripts/                       # PowerShell build & packaging scripts
```

- **Runtime**: C# / .NET 10 LTS (`net10.0-windows`)
- **UI Framework**: WPF / XAML / MVVM CommunityToolkit
- **Database**: SQLite (`Microsoft.Data.Sqlite` in WAL mode)
- **Encryption**: Windows DPAPI (`System.Security.Cryptography.ProtectedData`)
- **Installer**: Inno Setup 6.7.3

---

## Installation & Distribution

### Option A: Windows Installer
Download and run `NOVA-Clip-Setup.exe` from the `dist/` directory. The installer sets up shortcuts and automatically registers startup options without requiring administrator permissions.

### Option B: Portable Edition
Extract `NOVA-Clip-Portable.zip` into any folder and run `NOVA.Clip.App.exe`. All data is stored in the user's local application data directory (`%LOCALAPPDATA%\NOVA.Clip`).

---

## Building from Source

### Prerequisites
- Windows 10 (Build 19041+) or Windows 11
- .NET 10.0 SDK
- Inno Setup 6 (optional, required only for compiling installer)

### Quick Commands
```powershell
# Restore and build solution
powershell -File scripts/build.ps1

# Run tests
dotnet test NOVA.Clip.sln

# Publish self-contained release and portable zip
powershell -File scripts/publish.ps1

# Package Inno Setup installer
powershell -File scripts/package-installer.ps1

# Launch development instance
powershell -File scripts/launch-dev.ps1
```

---

## License

NOVA Clip is distributed under the MIT License. See [LICENSE](LICENSE) for details.

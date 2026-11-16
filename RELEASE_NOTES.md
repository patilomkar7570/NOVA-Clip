# NOVA Clip v1.0.0 — Production-Grade Windows Clipboard Manager

Welcome to the first official release of **NOVA Clip**! 🚀

NOVA Clip is a modern, privacy-first Windows desktop clipboard manager built with **.NET 10 LTS and WPF**. It redefines clipboard productivity with two flagship capabilities: a sleek **Floating Top Island Dock** and instant **Phone-to-PC Local Sync** over your local Wi-Fi.

---

## 🌟 What's New in v1.0.0

### 🏝️ Floating Top Island Dock
- **Top-Center Minimalist Bar**: Floats centered directly at the top of your screen (~40% width) just below your webcam.
- **4 Real-Time Clip Slots**: Displays color-coded badges for **TEXT**, **LINK**, **FILE**, and **IMG** with immediate type detection.
- **1-Click Copy**: Click any slot to copy directly back into your Windows clipboard with visual confirmation.
- **Native OLE Drag-and-Drop**: Drag text, links, images, or files directly into any application or Windows File Explorer.
- **Global Hotkey**: Press `Ctrl + Shift + Space` anywhere in Windows to toggle the island in/out of view.

### 📱 Phone-to-PC Local Sync (Cross-Device Clipboard)
- **Zero Cloud & Zero Internet**: Runs an embedded lightweight HTTP server directly on your PC over local Wi-Fi.
- **Works on Any Mobile Browser**: Open the local IP in Safari (iOS) or Chrome (Android) — no mobile app needed!
- **Instant System Injection**: Pasting on your phone sends the content straight into your Windows clipboard (`Ctrl+V` ready) and places it into Slot 1 of your Top Island dock.
- **File Uploads**: Transfer photos, documents, and files directly to your PC.

### 🛡️ Privacy & Security
- **Encrypted at Rest**: All payload data is protected using **Windows DPAPI** (`CurrentUser` scope) and local SQLite in WAL mode.
- **Smart Filter Heuristics**: Automatically detects and ignores credit cards (Luhn algorithm), OTP/2FA codes, API keys, and sensitive data.
- **Process Protection**: Automatically skips capture from password managers (1Password, Bitwarden, KeePass, etc.).

### ⚙️ System Integration
- **Auto-Startup on Boot**: Runs silently in the background on Windows startup with zero configuration.
- **Clean Theme**: Default light mode theme with white logo styling.
- **Sequential Paste Queue**: Multi-item sequential copying (`Ctrl + Alt + C`) and pasting (`Ctrl + Alt + V`).

---

## 📦 Downloads

| File | Description | Checksum / Details |
| :--- | :--- | :--- |
| **[NOVA-Clip-Setup.exe](https://github.com/patilomkar7570/NOVA-Clip/releases/download/v1.0.0/NOVA-Clip-Setup.exe)** | Standard Windows Installer | Recommended for all Windows 10/11 users |
| **[NOVA-Clip-Portable.zip](https://github.com/patilomkar7570/NOVA-Clip/releases/download/v1.0.0/NOVA-Clip-Portable.zip)** | Self-Contained Portable Edition | Zero install required, unzip and run |

---

*Sha-256 integrity hashes and automated release binaries packaged directly from tested builds.*

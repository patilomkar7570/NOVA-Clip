# Installation Guide — NOVA Clip 1.0

## System Requirements
- **OS**: Windows 10 (Version 2004 or later) or Windows 11
- **Architecture**: 64-bit (x64)
- **Permissions**: Standard user permissions (No administrative rights required!)
- **Disk Space**: ~100 MB free disk space

---

## Installation Methods

### Method 1: Windows Installer (Recommended)
1. Navigate to the `dist/` directory.
2. Double-click `NOVA-Clip-Setup.exe`.
3. Follow the modern wizard:
   - Choose whether to create a desktop shortcut.
   - Choose whether to start NOVA Clip automatically on Windows startup.
4. Click **Finish**. NOVA Clip will launch immediately in your system tray and ready to capture clipboard items.

### Method 2: Portable Deployment
1. Navigate to `dist/NOVA-Clip-Portable.zip`.
2. Extract the archive into any folder (e.g. `C:\Tools\NovaClip` or a USB flash drive).
3. Run `NOVA.Clip.App.exe`.
4. All application settings and encrypted clipboard history will be safely stored in your local Windows user directory at `%LOCALAPPDATA%\NOVA.Clip\`.

---

## Uninstall Procedure
To uninstall NOVA Clip:
1. Open Windows **Settings** > **Apps** > **Installed apps**.
2. Find **NOVA Clip** in the list and click **Uninstall**.
3. Or run the uninstaller directly from the installation folder or Start Menu (`Uninstall NOVA Clip`).
4. (Optional) To remove your encrypted history database, delete the folder `%LOCALAPPDATA%\NOVA.Clip`.

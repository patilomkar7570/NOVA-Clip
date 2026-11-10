# Building NOVA Clip 1.0

This guide explains how to compile, test, publish, and package **NOVA Clip** on Windows.

---

## 1. Environment Requirements

- **Operating System**: Windows 10 (Version 2004 or later) / Windows 11
- **Architecture**: x64
- **.NET SDK**: .NET 10.0 SDK (`10.0.403` or later)
- **Installer Tool**: Inno Setup 6 (Version 6.7.3 or later)

---

## 2. Directory Structure

- `src/NOVA.Clip.Core`: Shared abstractions, models, interfaces, configuration.
- `src/NOVA.Clip.Infrastructure`: SQLite database, DPAPI data protection, Win32 P/Invoke, native clipboard monitors.
- `src/NOVA.Clip.App`: WPF desktop interface, XAML templates, ViewModels, System Tray integration.
- `src/NOVA.Clip.Tests`: xUnit automated test projects (44 automated tests).
- `installer/`: Inno Setup `.iss` compiler script.
- `scripts/`: PowerShell build, publish, and test scripts.
- `dist/`: Final release deliverables (`NOVA-Clip-Setup.exe` and `NOVA-Clip-Portable.zip`).

---

## 3. Step-by-Step Build Commands

### Step 1: Restore Dependencies
```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
dotnet restore NOVA.Clip.sln
```

### Step 2: Compile Solution
```powershell
dotnet build NOVA.Clip.sln -c Release
```

### Step 3: Run Automated Test Suite
```powershell
dotnet test NOVA.Clip.sln -c Release --no-build
```

### Step 4: Publish Self-Contained Binary
```powershell
dotnet publish src/NOVA.Clip.App/NOVA.Clip.App.csproj -c Release -r win-x64 --self-contained -o publish/win-x64
```

### Step 5: Build Inno Setup Installer
```powershell
$env:PATH = "$env:LOCALAPPDATA\Programs\Inno Setup 6;$env:PATH"
iscc installer/NovaClip.iss
```

---

## 4. Automation Scripts

You can execute the entire pipeline with the automated scripts located in `scripts/`:

- `scripts/build.ps1`: Restores, compiles in Release mode, and runs all 44 unit tests.
- `scripts/publish.ps1`: Publishes self-contained binaries to `publish/win-x64` and archives `dist/NOVA-Clip-Portable.zip`.
- `scripts/package-installer.ps1`: Compiles `dist/NOVA-Clip-Setup.exe` using Inno Setup.
- `scripts/launch-dev.ps1`: Runs the WPF application directly for development and testing.

---

## 5. Artifact Verification

After building, verify the following files exist in `dist/`:
- `dist/NOVA-Clip-Setup.exe` (Standalone Windows installer)
- `dist/NOVA-Clip-Portable.zip` (Portable release archive)

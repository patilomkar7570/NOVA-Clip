# Architecture Documentation — NOVA Clip 1.0

## System Overview

NOVA Clip is architected using a maintainable, clean layered architecture designed specifically for the Windows desktop environment using C#, .NET 10 LTS, and WPF.

```
┌────────────────────────────────────────────────────────┐
│                   NOVA.Clip.App                        │
│   (WPF Views, ViewModels, Themes, Controls, Services)  │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│               NOVA.Clip.Infrastructure                 │
│  (Clipboard Monitor, DPAPI Security, SQLite DB, Win32) │
└──────────────────────────┬─────────────────────────────┘
                           │
                           ▼
┌────────────────────────────────────────────────────────┐
│                   NOVA.Clip.Core                       │
│      (Models, Interfaces, Enums, Configuration)        │
└────────────────────────────────────────────────────────┘
```

---

## Layer Descriptions

### 1. `NOVA.Clip.Core`
The innermost foundation layer, containing pure domain entities, contract interfaces, and application configuration. It has zero UI dependencies.
- **`Models`**:
  - `ClipItem`: Core clipboard record containing metadata, hash, and encrypted payload.
  - `ClipContentType`: Enumeration (`Text`, `Link`, `RichText`, `Image`, `Files`).
  - `PayloadData`: Structured representation of decrypted data (plain text, HTML, RTF, file paths, image bitmap bytes).
  - `SnippetItem`: Reusable text templates with tags and titles.
  - `PasteQueue` & `PasteQueueItem`: Ordered queue data structure supporting sequential workflows.
  - `StorageUsageInfo`: Aggregated statistics on items and storage space.
- **`Interfaces`**:
  - `IClipRepository`, `IClipboardMonitor`, `IClipboardService`, `IDataProtectionService`, `IPrivacyFilter`, `IStorageService`, `ISettingsService`, `IHotKeyService`, `IInputSimulator`, `IThemeService`.
- **`Configuration`**:
  - `AppSettings`: Strongly typed settings model with JSON serialization.

### 2. `NOVA.Clip.Infrastructure`
The implementation layer for native operating system interactions, storage, cryptography, and databases.
- **`Windows/NativeMethods.cs`**:
  - Safe P/Invoke wrappers for Win32 User32, Kernel32, and Shell32 APIs.
  - Manages `AddClipboardFormatListener`, `RemoveClipboardFormatListener`, `RegisterHotKey`, `SendInput`, `GetForegroundWindow`, `SetWindowPos`, and `Shell_NotifyIcon`.
- **`Clipboard/ClipboardMonitor.cs`**:
  - Listens to `WM_CLIPBOARDUPDATE` messages via a dedicated hidden message-only window (`HwndSource`).
  - Automatically identifies foreground window handle, active process image name, and window title.
- **`Clipboard/ClipboardService.cs`**:
  - Handles clipboard reads on STA apartments with bounded exponential backoff retries on `CLIPBRD_E_CANT_OPEN`.
  - Self-copy sequence detection to prevent recursive clipboard capture loops.
  - Content format identification (text, URL domain detection, rich text, image bitmap extraction, file drop).
  - Encryption using DPAPI and offloading large binary assets to disk.
- **`Security/DpapiDataProtectionService.cs`**:
  - Uses Windows DPAPI (`DataProtectionScope.CurrentUser`) to protect stored payloads on disk.
- **`Security/PrivacyFilter.cs`**:
  - Heuristic detection of payment cards (Luhn check), OTP tokens, passwords, and private keys.
  - Application exclusions by process name.
- **`Database/SqliteClipRepository.cs`**:
  - SQLite storage engine in WAL (Write-Ahead Logging) mode.
  - Handles automatic schema creation, indexing, duplicate detection, and retention cleanup.

### 3. `NOVA.Clip.App`
The presentation layer built in WPF with modern XAML and MVVM.
- **`ViewModels`**:
  - `MainViewModel`: Application coordinator, navigation manager, and event router.
  - `OverviewViewModel`: Summary cards, storage metrics, quick actions.
  - `ClipboardListViewModel`: Filtered and searchable clipboard history.
  - `SnippetsViewModel`: CRUD management of reusable snippet items.
  - `PasteQueueViewModel`: Signature multi-item queue with Copy Next, Paste Next, and Paste All.
  - `FloatingPanelViewModel`: Signature compact quick panel with keyboard navigation.
  - `SettingsViewModel`: Comprehensive application preferences and startup configuration.
- **`Views`**:
  - `MainWindow.xaml`: Main management desktop window.
  - `FloatingPanelWindow.xaml`: Real independent floating window (`WindowStyle="None"`, `AllowsTransparency="True"`, `Topmost="True"`).
  - `CornerLauncherWindow.xaml`: Screen corner launcher button.
  - `ItemDetailsDialog.xaml`: Item inspector dialog.
- **`Themes`**:
  - `DarkTheme.xaml`: Charcoal dark surfaces with modern blue accents.
  - `LightTheme.xaml`: Clean light surfaces.
  - `Icons.xaml`: Vector geometries for Lucide-style consistent iconography.
- **`Services`**:
  - `SingleInstanceService`: Named mutex and named pipe IPC to prevent multiple instances from competing.
  - `TrayIconService`: Native Win32 notification icon with context menu.
  - `ThemeService`: Registry-based Windows theme detector and dynamic resource swapper.

---

## Paste Queue State Flow

```
User selects items from history
            │
            ▼
Adds items to named PasteQueue
(Creates independent payload snapshots)
            │
    ┌───────┴────────────────────────┐
    ▼                                ▼
[Copy Next]                     [Paste Next]
    │                                │
    ▼                                ▼
1. Restores payload to      1. Saves target foreground HWND
   Windows clipboard.       2. Hides floating panel
2. Marks item completed.    3. Restores target app window focus
3. Advances index.          4. Sets payload to Windows clipboard
                            5. Synthesizes Ctrl+V via SendInput
                            6. Advances index and displays feedback
                               (Falls back safely if paste fails)
```

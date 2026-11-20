using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NOVA.Clip.App.Services;
using NOVA.Clip.App.ViewModels;
using NOVA.Clip.App.Views;
using NOVA.Clip.Core.Configuration;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;
using NOVA.Clip.Infrastructure.Clipboard;
using NOVA.Clip.Infrastructure.Configuration;
using NOVA.Clip.Infrastructure.Database;
using NOVA.Clip.Infrastructure.Security;
using NOVA.Clip.Infrastructure.Storage;
using NOVA.Clip.Infrastructure.Windows;
using NOVA.Clip.Infrastructure.Network;

namespace NOVA.Clip.App;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    private SingleInstanceService? _singleInstanceService;
    private TrayIconService? _trayIconService;
    private IHotKeyService? _hotKeyService;
    private IClipboardMonitor? _clipboardMonitor;
    private IInputSimulator? _inputSimulator;
    private ISettingsService? _settingsService;
    private IThemeService? _themeService;
    private IPhoneSyncServer? _phoneSyncServer;

    private HwndSource? _messageHwndSource;
    private MainWindow? _mainWindow;
    private FloatingPanelWindow? _floatingPanel;
    private CornerLauncherWindow? _cornerLauncher;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NOVA.Clip");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"), $"[{DateTime.UtcNow:O}] AppDomain Unhandled: {args.ExceptionObject}\n");
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NOVA.Clip");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"), $"[{DateTime.UtcNow:O}] Dispatcher Unhandled: {args.Exception}\n");
            }
            catch { }
        };

        // 1. Dependency Injection setup
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // 2. Single instance check
        _singleInstanceService = ServiceProvider.GetRequiredService<SingleInstanceService>();
        if (!_singleInstanceService.CheckIsFirstInstance())
        {
            Shutdown();
            return;
        }

        _singleInstanceService.AnotherInstanceStarted += (s, args) =>
        {
            Dispatcher.Invoke(() => ShowMainWindow());
        };

        // 3. Settings & Theme
        _settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
        await _settingsService.LoadSettingsAsync();

        _themeService = ServiceProvider.GetRequiredService<IThemeService>();
        _themeService.ApplyTheme(_settingsService.Settings.ThemeMode);

        // 4. Auto-startup: sync registry to current setting (default = enabled)
        // Only register startup when running as a published .exe (not via `dotnet run`)
        string exePath = Environment.ProcessPath
            ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        bool isPublishedExe = exePath.EndsWith("NOVA.Clip.App.exe", StringComparison.OrdinalIgnoreCase);
        if (isPublishedExe)
        {
            StartupService.SyncToSetting(_settingsService.Settings.StartWithWindows, exePath);

            // Wire setting changes so toggling in Settings UI updates the registry live
            _settingsService.SettingsChanged += (_, settings) =>
                StartupService.SyncToSetting(settings.StartWithWindows, exePath);
        }

        // 5. Create hidden message window for clipboard notifications & hotkeys
        CreateMessageWindow();

        // 6. Initialize Tray Icon
        _trayIconService = ServiceProvider.GetRequiredService<TrayIconService>();
        if (_messageHwndSource != null)
        {
            _trayIconService.Initialize(_messageHwndSource.Handle);
        }
        _trayIconService.OpenMainAppRequested += (s, args) => ShowMainWindow();
        _trayIconService.OpenFloatingPanelRequested += (s, args) => ToggleFloatingPanel();
        _trayIconService.ExitRequested += (s, args) => ShutdownApp();

        // 5b. Start Phone Sync Server (Local Wi-Fi Receiver)
        _phoneSyncServer = ServiceProvider.GetRequiredService<IPhoneSyncServer>();
        var clipService = ServiceProvider.GetRequiredService<IClipboardService>();

        _phoneSyncServer.TextReceived += async (text, source) =>
        {
            await clipService.IngestTextAsync(text, source);
            Dispatcher.Invoke(() =>
            {
                string snippet = text.Length > 35 ? text[..35] + "..." : text;
                _trayIconService?.ShowNotification("Phone Sync", $"Received from phone: {snippet}");
            });
        };

        _phoneSyncServer.FileReceived += async (filePath) =>
        {
            await clipService.IngestFilesAsync([filePath], "iPhone");
            Dispatcher.Invoke(() =>
            {
                _trayIconService?.ShowNotification("Phone Sync", $"Received file: {System.IO.Path.GetFileName(filePath)}");
            });
        };

        // Two-Way Sync: Automatically forward PC clipboard captures to Phone Sync
        clipService.ItemCaptured += (item) =>
        {
            if (item.Type == ClipContentType.Text || item.Type == ClipContentType.Link)
            {
                if (!string.Equals(item.SourceApplication, "iPhone", StringComparison.OrdinalIgnoreCase))
                {
                    _phoneSyncServer?.PushTextFromPc(item.Title, item.Type.ToString(), item.SourceApplication ?? "PC");
                }
            }
        };

        _phoneSyncServer.Start();

        // Seed recent clips from repository to Phone Sync
        var clipRepo = ServiceProvider.GetRequiredService<IClipRepository>();
        _ = Task.Run(async () =>
        {
            try
            {
                var recents = await clipRepo.GetAllItemsAsync(limit: 5);
                foreach (var clip in recents.Where(c => c.Type == ClipContentType.Text || c.Type == ClipContentType.Link).Reverse())
                {
                    _phoneSyncServer?.PushTextFromPc(clip.Title, clip.Type.ToString(), clip.SourceApplication ?? "PC");
                }
            }
            catch { }
        });

        // 6. Initialize Windows & ViewModels
        var mainVm = ServiceProvider.GetRequiredService<MainViewModel>();
        _mainWindow = new MainWindow(mainVm, _settingsService, _trayIconService);

        var floatingVm = ServiceProvider.GetRequiredService<FloatingPanelViewModel>();
        _floatingPanel = new FloatingPanelWindow(floatingVm, _settingsService.Settings);

        mainVm.RequestOpenFloatingPanel += () => ToggleFloatingPanel();

        // 7. Register Global Hotkeys
        RegisterHotkeys();

        // 8. Screen Corner Launcher (if enabled)
        UpdateCornerLauncher();
        _settingsService.SettingsChanged += (s, settings) => UpdateCornerLauncher();

        // 9. Startup Window display
        bool startMinimized = e.Args.Contains("--minimized") || _settingsService.Settings.LaunchFloatingPanelAtStartup;
        if (_settingsService.Settings.LaunchFloatingPanelAtStartup)
        {
            ToggleFloatingPanel();
        }
        else if (!startMinimized)
        {
            ShowMainWindow();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core Infrastructure
        services.AddSingleton<IDataProtectionService, DpapiDataProtectionService>();
        services.AddSingleton<IPrivacyFilter, PrivacyFilter>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IClipRepository, SqliteClipRepository>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IClipboardMonitor, ClipboardMonitor>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IHotKeyService, HotKeyService>();
        services.AddSingleton<IInputSimulator, WindowsInputSimulator>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IPhoneSyncServer, PhoneSyncServer>();

        // App Services
        services.AddSingleton<SingleInstanceService>();
        services.AddSingleton<TrayIconService>();

        // ViewModels
        services.AddSingleton<OverviewViewModel>(sp => new OverviewViewModel(
            sp.GetRequiredService<IClipRepository>(),
            sp.GetRequiredService<IClipboardService>(),
            sp.GetRequiredService<ISettingsService>(),
            () => ToggleFloatingPanel()));

        services.AddTransient<ClipboardListViewModel>();
        services.AddTransient<SnippetsViewModel>();
        services.AddSingleton<PasteQueueViewModel>();
        services.AddSingleton<SettingsViewModel>();

        services.AddSingleton<FloatingPanelViewModel>(sp => new FloatingPanelViewModel(
            sp.GetRequiredService<IClipRepository>(),
            sp.GetRequiredService<IClipboardService>(),
            sp.GetRequiredService<IInputSimulator>(),
            () => ShowMainWindow(),
            card => _ = sp.GetRequiredService<PasteQueueViewModel>().AddItemToQueueAsync(card.Model),
            () => { ShowMainWindow(); sp.GetRequiredService<MainViewModel>().Navigate("Settings"); },
            sp.GetService<IPhoneSyncServer>()));

        services.AddSingleton<MainViewModel>();
    }

    private void CreateMessageWindow()
    {
        var parameters = new HwndSourceParameters("NOVA_CLIP_MESSAGE_LISTENER")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0x800000 // WS_BORDER (hidden)
        };

        _messageHwndSource = new HwndSource(parameters);
        _messageHwndSource.AddHook(HwndMessageHook);

        _clipboardMonitor = ServiceProvider?.GetRequiredService<IClipboardMonitor>();
        var clipboardService = ServiceProvider?.GetRequiredService<IClipboardService>();
        if (_clipboardMonitor != null && clipboardService != null)
        {
            _clipboardMonitor.ClipboardChanged += async (s, args) =>
            {
                try
                {
                    await clipboardService.ProcessClipboardChangeAsync(args);
                }
                catch { }
            };
        }
        _clipboardMonitor?.Start(_messageHwndSource.Handle);

        _inputSimulator = ServiceProvider?.GetRequiredService<IInputSimulator>();
        _hotKeyService = ServiceProvider?.GetRequiredService<IHotKeyService>();
    }

    private IntPtr HwndMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_CLIPBOARDUPDATE)
        {
            if (_clipboardMonitor is ClipboardMonitor cm)
            {
                cm.HandleWindowMessage(msg);
                handled = true;
            }
        }
        else if (msg == NativeMethods.WM_HOTKEY)
        {
            if (_hotKeyService?.ProcessHotKeyMessage(msg, wParam, lParam) == true)
            {
                handled = true;
            }
        }
        else if (msg == TrayIconService.WM_TRAYICON)
        {
            _trayIconService?.ProcessWindowMessage(msg, wParam, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void RegisterHotkeys()
    {
        if (_messageHwndSource == null || _hotKeyService == null || _settingsService == null) return;

        var s = _settingsService.Settings;

        _hotKeyService.HotKeyTriggered += OnHotKeyTriggered;

        // 1. Toggle Floating Panel (default Ctrl+Shift+Space)
        _hotKeyService.Register(HotKeyAction.ToggleFloatingPanel, s.GlobalHotkeyModifier, s.GlobalHotkeyKey, _messageHwndSource.Handle);

        // 2. Copy Next (default Ctrl+Alt+C)
        _hotKeyService.Register(HotKeyAction.CopyNext, s.CopyNextHotkeyModifier, s.CopyNextHotkeyKey, _messageHwndSource.Handle);

        // 3. Paste Next (default Ctrl+Alt+V)
        _hotKeyService.Register(HotKeyAction.PasteNext, s.PasteNextHotkeyModifier, s.PasteNextHotkeyKey, _messageHwndSource.Handle);
    }

    private async void OnHotKeyTriggered(object? sender, HotKeyEventArgs e)
    {
        switch (e.Action)
        {
            case HotKeyAction.ToggleFloatingPanel:
                ToggleFloatingPanel();
                break;

            case HotKeyAction.CopyNext:
                var queueVm = ServiceProvider?.GetService<PasteQueueViewModel>();
                if (queueVm != null)
                {
                    await queueVm.CopyNextAsync();
                }
                break;

            case HotKeyAction.PasteNext:
                var qVm = ServiceProvider?.GetService<PasteQueueViewModel>();
                if (qVm != null && _inputSimulator != null)
                {
                    IntPtr target = _inputSimulator.GetForegroundWindow();
                    await qVm.PasteNextAsync(target);
                }
                break;
        }
    }

    private void ToggleFloatingPanel()
    {
        if (_floatingPanel == null) return;

        if (_floatingPanel.IsVisible)
        {
            _floatingPanel.Hide();
        }
        else
        {
            IntPtr currentActiveHwnd = _inputSimulator?.GetForegroundWindow() ?? IntPtr.Zero;
            _floatingPanel.ShowPanel(currentActiveHwnd);
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Activate();
    }

    private void UpdateCornerLauncher()
    {
        if (_settingsService == null) return;

        if (_settingsService.Settings.CornerLauncherEnabled)
        {
            if (_cornerLauncher == null)
            {
                _cornerLauncher = new CornerLauncherWindow(_settingsService.Settings, () => ToggleFloatingPanel());
            }
            _cornerLauncher.PositionLauncher();
            _cornerLauncher.Show();
        }
        else
        {
            _cornerLauncher?.Hide();
        }
    }

    private void ShutdownApp()
    {
        string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NOVA.Clip");
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "startup.log"), $"[{DateTime.UtcNow:O}] ShutdownApp called! StackTrace:\n{Environment.StackTrace}\n"); } catch { }

        _phoneSyncServer?.Dispose();
        _hotKeyService?.UnregisterAll();
        _clipboardMonitor?.Stop();
        _trayIconService?.Dispose();
        _singleInstanceService?.Dispose();

        _messageHwndSource?.RemoveHook(HwndMessageHook);
        _messageHwndSource?.Dispose();

        _mainWindow?.ForceClose();
        _floatingPanel?.Close();
        _cornerLauncher?.Close();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ShutdownApp();
        base.OnExit(e);
    }
}

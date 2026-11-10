using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NOVA.Clip.Core.Configuration;
using NOVA.Clip.Core.Interfaces;

namespace NOVA.Clip.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IClipRepository _repository;

    // General
    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private bool _launchFloatingPanelAtStartup;

    [ObservableProperty]
    private string _defaultView = "Overview";

    // Appearance
    [ObservableProperty]
    private string _themeMode = "System";

    [ObservableProperty]
    private string _accentColor = "#2563EB";

    [ObservableProperty]
    private double _floatingPanelOpacity = 0.98;

    // Activation
    [ObservableProperty]
    private string _globalHotkeyModifier = "Ctrl+Shift";

    [ObservableProperty]
    private string _globalHotkeyKey = "Space";

    [ObservableProperty]
    private bool _cornerLauncherEnabled;

    [ObservableProperty]
    private string _cornerLauncherPosition = "BottomRight";

    [ObservableProperty]
    private double _cornerLauncherSize = 42;

    [ObservableProperty]
    private double _cornerLauncherOpacity = 0.85;

    // Clipboard Monitoring
    [ObservableProperty]
    private bool _monitoringEnabled = true;

    [ObservableProperty]
    private bool _captureText = true;

    [ObservableProperty]
    private bool _captureLinks = true;

    [ObservableProperty]
    private bool _captureRichText = true;

    [ObservableProperty]
    private bool _captureImages = true;

    [ObservableProperty]
    private bool _captureFiles = true;

    [ObservableProperty]
    private bool _duplicateDetectionEnabled = true;

    [ObservableProperty]
    private int _maxHistoryItems = 500;

    [ObservableProperty]
    private int _retentionDays = 30;

    [ObservableProperty]
    private long _maxStorageMb = 500;

    // Privacy
    [ObservableProperty]
    private bool _sensitiveContentProtectionEnabled = true;

    [ObservableProperty]
    private string _newExcludedProcess = string.Empty;

    // Paste Queue
    [ObservableProperty]
    private string _copyNextHotkey = "Ctrl+Alt+C";

    [ObservableProperty]
    private string _pasteNextHotkey = "Ctrl+Alt+V";

    [ObservableProperty]
    private string _defaultSeparator = "\n";

    [ObservableProperty]
    private bool _autoAdvanceQueue = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<string> ExcludedProcesses { get; } = [];

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeService themeService,
        IClipRepository repository)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _repository = repository;

        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Settings;

        StartWithWindows = s.StartWithWindows;
        MinimizeToTray = s.MinimizeToTray;
        CloseToTray = s.CloseToTray;
        LaunchFloatingPanelAtStartup = s.LaunchFloatingPanelAtStartup;
        DefaultView = s.DefaultView;

        ThemeMode = s.ThemeMode;
        AccentColor = s.AccentColor;
        FloatingPanelOpacity = s.FloatingPanelOpacity;

        GlobalHotkeyModifier = s.GlobalHotkeyModifier;
        GlobalHotkeyKey = s.GlobalHotkeyKey;
        CornerLauncherEnabled = s.CornerLauncherEnabled;
        CornerLauncherPosition = s.CornerLauncherPosition;
        CornerLauncherSize = s.CornerLauncherSize;
        CornerLauncherOpacity = s.CornerLauncherOpacity;

        MonitoringEnabled = s.MonitoringEnabled;
        CaptureText = s.CaptureText;
        CaptureLinks = s.CaptureLinks;
        CaptureRichText = s.CaptureRichText;
        CaptureImages = s.CaptureImages;
        CaptureFiles = s.CaptureFiles;
        DuplicateDetectionEnabled = s.DuplicateDetectionEnabled;
        MaxHistoryItems = s.MaxHistoryItems;
        RetentionDays = s.RetentionDays;
        MaxStorageMb = s.MaxStorageMb;

        SensitiveContentProtectionEnabled = s.SensitiveContentProtectionEnabled;

        CopyNextHotkey = $"{s.CopyNextHotkeyModifier}+{s.CopyNextHotkeyKey}";
        PasteNextHotkey = $"{s.PasteNextHotkeyModifier}+{s.PasteNextHotkeyKey}";
        DefaultSeparator = s.DefaultSeparator;
        AutoAdvanceQueue = s.AutoAdvanceQueue;

        ExcludedProcesses.Clear();
        foreach (var p in s.ExcludedProcesses)
        {
            ExcludedProcesses.Add(p);
        }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        var s = _settingsService.Settings;

        s.StartWithWindows = StartWithWindows;
        s.MinimizeToTray = MinimizeToTray;
        s.CloseToTray = CloseToTray;
        s.LaunchFloatingPanelAtStartup = LaunchFloatingPanelAtStartup;
        s.DefaultView = DefaultView;

        s.ThemeMode = ThemeMode;
        s.AccentColor = AccentColor;
        s.FloatingPanelOpacity = FloatingPanelOpacity;

        s.GlobalHotkeyModifier = GlobalHotkeyModifier;
        s.GlobalHotkeyKey = GlobalHotkeyKey;
        s.CornerLauncherEnabled = CornerLauncherEnabled;
        s.CornerLauncherPosition = CornerLauncherPosition;
        s.CornerLauncherSize = CornerLauncherSize;
        s.CornerLauncherOpacity = CornerLauncherOpacity;

        s.MonitoringEnabled = MonitoringEnabled;
        s.CaptureText = CaptureText;
        s.CaptureLinks = CaptureLinks;
        s.CaptureRichText = CaptureRichText;
        s.CaptureImages = CaptureImages;
        s.CaptureFiles = CaptureFiles;
        s.DuplicateDetectionEnabled = DuplicateDetectionEnabled;
        s.MaxHistoryItems = MaxHistoryItems;
        s.RetentionDays = RetentionDays;
        s.MaxStorageMb = MaxStorageMb;

        s.SensitiveContentProtectionEnabled = SensitiveContentProtectionEnabled;
        s.ExcludedProcesses = ExcludedProcesses.ToList();

        s.DefaultSeparator = DefaultSeparator;
        s.AutoAdvanceQueue = AutoAdvanceQueue;

        // Apply theme immediately
        _themeService.ApplyTheme(ThemeMode);

        // Configure startup in registry
        UpdateStartupRegistry(StartWithWindows);

        await _settingsService.SaveSettingsAsync();
        StatusMessage = "Settings saved successfully.";
    }

    [RelayCommand]
    public void AddExcludedProcess()
    {
        if (string.IsNullOrWhiteSpace(NewExcludedProcess)) return;
        string proc = NewExcludedProcess.Trim();
        if (!proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            proc += ".exe";
        }

        if (!ExcludedProcesses.Contains(proc))
        {
            ExcludedProcesses.Add(proc);
            NewExcludedProcess = string.Empty;
        }
    }

    [RelayCommand]
    public void RemoveExcludedProcess(string processName)
    {
        ExcludedProcesses.Remove(processName);
    }

    [RelayCommand]
    public async Task ClearAllHistoryAsync()
    {
        int deleted = await _repository.ClearAllHistoryAsync();
        StatusMessage = $"Cleared {deleted} history items (favorites preserved).";
    }

    [RelayCommand]
    public async Task ClearImagesAsync()
    {
        int deleted = await _repository.ClearImagesAsync();
        StatusMessage = $"Cleared {deleted} saved images.";
    }

    private static void UpdateStartupRegistry(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            string appPath = Environment.ProcessPath ?? string.Empty;
            if (enable && !string.IsNullOrEmpty(appPath))
            {
                key.SetValue("NOVAClip", $"\"{appPath}\" --minimized");
            }
            else
            {
                key.DeleteValue("NOVAClip", false);
            }
        }
        catch { }
    }
}

namespace NOVA.Clip.Core.Configuration;

public class AppSettings
{
    // General
    public bool StartWithWindows { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool LaunchFloatingPanelAtStartup { get; set; } = false;
    public string DefaultView { get; set; } = "Overview";

    // Appearance
    public string ThemeMode { get; set; } = "Light"; // "System", "Dark", "Light"
    public string AccentColor { get; set; } = "#2563EB";
    public double FloatingPanelOpacity { get; set; } = 0.98;
    public double FloatingPanelWidth { get; set; } = 380;
    public double FloatingPanelHeight { get; set; } = 520;
    public double FloatingPanelLeft { get; set; } = -1;
    public double FloatingPanelTop { get; set; } = -1;

    // Activation
    public string GlobalHotkeyModifier { get; set; } = "Ctrl+Shift";
    public string GlobalHotkeyKey { get; set; } = "Space";
    public bool CornerLauncherEnabled { get; set; } = false;
    public string CornerLauncherPosition { get; set; } = "BottomRight"; // "TopLeft", "TopRight", "BottomLeft", "BottomRight"
    public double CornerLauncherSize { get; set; } = 42;
    public double CornerLauncherOpacity { get; set; } = 0.85;

    // Clipboard Monitoring
    public bool MonitoringEnabled { get; set; } = true;
    public bool CaptureText { get; set; } = true;
    public bool CaptureLinks { get; set; } = true;
    public bool CaptureRichText { get; set; } = true;
    public bool CaptureImages { get; set; } = true;
    public bool CaptureFiles { get; set; } = true;
    public bool DuplicateDetectionEnabled { get; set; } = true;
    public int MaxHistoryItems { get; set; } = 500;
    public int RetentionDays { get; set; } = 30; // 7, 30, 90, 0 (Never)
    public long MaxStorageMb { get; set; } = 500;

    // Privacy & Security
    public bool SensitiveContentProtectionEnabled { get; set; } = true;
    public List<string> ExcludedProcesses { get; set; } =
    [
        "1Password.exe",
        "KeePass.exe",
        "KeePassXC.exe",
        "Bitwarden.exe",
        "LastPass.exe",
        "Enpass.exe",
        "Dashlane.exe"
    ];
    public bool PauseMonitoring { get; set; } = false;

    // Paste Queue
    public string CopyNextHotkeyModifier { get; set; } = "Ctrl+Alt";
    public string CopyNextHotkeyKey { get; set; } = "C";
    public string PasteNextHotkeyModifier { get; set; } = "Ctrl+Alt";
    public string PasteNextHotkeyKey { get; set; } = "V";
    public string DefaultSeparator { get; set; } = "\n";
    public bool AutoAdvanceQueue { get; set; } = true;

    // Static metadata
    public const string ApplicationName = "NOVA Clip";
    public const string Version = "1.0.0";
    public const string Tagline = "Everything you copy. Always within reach.";
}

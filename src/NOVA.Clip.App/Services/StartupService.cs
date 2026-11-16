using Microsoft.Win32;

namespace NOVA.Clip.App.Services;

/// <summary>
/// Manages Windows auto-startup via the HKCU Run registry key.
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName  = "NOVAClip";

    /// <summary>Register NOVA Clip to launch on Windows startup (minimized).</summary>
    public static void Enable(string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.SetValue(ValueName, $"\"{exePath}\" --minimized");
        }
        catch { /* Registry write failed — non-fatal */ }
    }

    /// <summary>Remove the Windows startup entry.</summary>
    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { /* Registry delete failed — non-fatal */ }
    }

    /// <summary>Returns true if the startup entry currently exists.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch { return false; }
    }

    /// <summary>Sync registry state to match the given setting value.</summary>
    public static void SyncToSetting(bool enabled, string exePath)
    {
        if (enabled)
            Enable(exePath);
        else
            Disable();
    }
}

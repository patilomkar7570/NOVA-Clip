using System.Windows;
using Microsoft.Win32;
using NOVA.Clip.Core.Interfaces;

namespace NOVA.Clip.App.Services;

public class ThemeService : IThemeService
{
    private string _currentTheme = "System";
    private bool _isDark = true;

    public string CurrentTheme => _currentTheme;
    public bool IsDarkTheme => _isDark;

    public event EventHandler<string>? ThemeChanged;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            if (_currentTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                ApplyTheme("System");
            }
        };
    }

    public void ApplyTheme(string themeMode)
    {
        _currentTheme = themeMode;
        bool useDark;

        if (themeMode.Equals("Dark", StringComparison.OrdinalIgnoreCase))
        {
            useDark = true;
        }
        else if (themeMode.Equals("Light", StringComparison.OrdinalIgnoreCase))
        {
            useDark = false;
        }
        else // System
        {
            useDark = IsWindowsInDarkMode();
        }

        _isDark = useDark;

        string themeUri = useDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        var dict = new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) };

        var appResources = Application.Current?.Resources;
        if (appResources != null)
        {
            // Remove previous theme dictionary
            var existing = appResources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && (d.Source.OriginalString.Contains("DarkTheme") || d.Source.OriginalString.Contains("LightTheme")));

            if (existing != null)
            {
                appResources.MergedDictionaries.Remove(existing);
            }

            appResources.MergedDictionaries.Add(dict);
        }

        ThemeChanged?.Invoke(this, _currentTheme);
    }

    private static bool IsWindowsInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int intVal)
            {
                return intVal == 0;
            }
        }
        catch { }

        return true; // Default dark
    }
}

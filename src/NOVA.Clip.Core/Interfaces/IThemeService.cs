namespace NOVA.Clip.Core.Interfaces;

public interface IThemeService
{
    string CurrentTheme { get; }
    bool IsDarkTheme { get; }
    void ApplyTheme(string themeMode);
    event EventHandler<string>? ThemeChanged;
}

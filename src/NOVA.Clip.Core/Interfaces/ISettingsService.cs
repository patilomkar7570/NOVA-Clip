using NOVA.Clip.Core.Configuration;

namespace NOVA.Clip.Core.Interfaces;

public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadSettingsAsync();
    Task SaveSettingsAsync();
    event EventHandler<AppSettings>? SettingsChanged;
}

using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NOVA.Clip.Core.Configuration;
using NOVA.Clip.Core.Interfaces;

namespace NOVA.Clip.Infrastructure.Configuration;

public class SettingsService : ISettingsService
{
    private readonly IStorageService _storageService;
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsFilePath;
    private AppSettings _settings = new();

    public event EventHandler<AppSettings>? SettingsChanged;

    public AppSettings Settings => _settings;

    public SettingsService(IStorageService storageService, ILogger<SettingsService> logger)
    {
        _storageService = storageService;
        _logger = logger;
        _settingsFilePath = Path.Combine(_storageService.AppDataDirectory, "settings.json");
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = await File.ReadAllTextAsync(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _settings = loaded;
                    _logger.LogInformation("Loaded application settings from {Path}", _settingsFilePath);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings file at {Path}, using defaults", _settingsFilePath);
        }

        // Default settings
        _settings = new AppSettings();
        await SaveSettingsAsync();
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_settings, options);
            await File.WriteAllTextAsync(_settingsFilePath, json);
            SettingsChanged?.Invoke(this, _settings);
            _logger.LogInformation("Saved application settings to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings file at {Path}", _settingsFilePath);
        }
    }
}

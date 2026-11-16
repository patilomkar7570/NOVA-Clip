using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NOVA.Clip.Core.Configuration;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.App.ViewModels;

public partial class OverviewViewModel : ObservableObject
{
    private readonly IClipRepository _repository;
    private readonly IClipboardService _clipboardService;
    private readonly ISettingsService _settingsService;
    private readonly Action _openFloatingPanel;

    [ObservableProperty]
    private int _totalItemsCount;

    [ObservableProperty]
    private int _textCount;

    [ObservableProperty]
    private int _linkCount;

    [ObservableProperty]
    private int _imageCount;

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private string _storageUsedFormatted = "0 KB";

    [ObservableProperty]
    private bool _isMonitoringActive;

    [ObservableProperty]
    private string _globalShortcutDisplay = "Ctrl + Shift + Space";

    public ObservableCollection<ItemCardViewModel> RecentItems { get; } = [];
    public ObservableCollection<ItemCardViewModel> FavoriteItems { get; } = [];

    public event Action<ItemCardViewModel>? ItemAddToQueueRequested;
    public event Action<ItemCardViewModel>? ItemDetailsRequested;

    public OverviewViewModel(
        IClipRepository repository,
        IClipboardService clipboardService,
        ISettingsService settingsService,
        Action openFloatingPanel)
    {
        _repository = repository;
        _clipboardService = clipboardService;
        _settingsService = settingsService;
        _openFloatingPanel = openFloatingPanel;

        UpdateSettingsState();
        _settingsService.SettingsChanged += (s, e) => UpdateSettingsState();
        _clipboardService.ItemCaptured += item => System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => RefreshAsync());
    }

    private void UpdateSettingsState()
    {
        var s = _settingsService.Settings;
        IsMonitoringActive = s.MonitoringEnabled && !s.PauseMonitoring;
        GlobalShortcutDisplay = $"{s.GlobalHotkeyModifier} + {s.GlobalHotkeyKey}".Replace("+", " + ");
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var usage = await _repository.GetStorageUsageAsync();
        TotalItemsCount = usage.TotalItemsCount;
        TextCount = usage.TextItemsCount;
        LinkCount = usage.LinkItemsCount;
        ImageCount = usage.ImageItemsCount;
        FileCount = usage.FileItemsCount;
        StorageUsedFormatted = usage.FormattedTotalSize;

        // Load recent items
        var recent = await _repository.GetAllItemsAsync(limit: 8);
        RecentItems.Clear();
        foreach (var item in recent)
        {
            RecentItems.Add(new ItemCardViewModel(
                item,
                _clipboardService,
                _repository,
                onDeleted: card => RecentItems.Remove(card),
                onAddToQueue: card => ItemAddToQueueRequested?.Invoke(card),
                onViewDetails: card => ItemDetailsRequested?.Invoke(card)));
        }

        // Load favorite items
        var favs = await _repository.GetFavoritesAsync(limit: 8);
        FavoriteItems.Clear();
        foreach (var item in favs)
        {
            FavoriteItems.Add(new ItemCardViewModel(
                item,
                _clipboardService,
                _repository,
                onDeleted: card => FavoriteItems.Remove(card),
                onAddToQueue: card => ItemAddToQueueRequested?.Invoke(card),
                onViewDetails: card => ItemDetailsRequested?.Invoke(card)));
        }
    }

    [RelayCommand]
    public async Task ToggleMonitoringAsync()
    {
        var settings = _settingsService.Settings;
        settings.PauseMonitoring = !settings.PauseMonitoring;
        await _settingsService.SaveSettingsAsync();
        UpdateSettingsState();
    }

    [RelayCommand]
    public void OpenFloatingPanel()
    {
        _openFloatingPanel();
    }

    [RelayCommand]
    public async Task ClearHistoryAsync()
    {
        await _repository.ClearAllHistoryAsync();
        await RefreshAsync();
    }
}

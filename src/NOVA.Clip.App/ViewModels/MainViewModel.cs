using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NOVA.Clip.Core.Configuration;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IClipboardMonitor _clipboardMonitor;
    private readonly IClipboardService _clipboardService;
    private readonly IClipRepository _repository;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private string _activeSection = "Overview";

    [ObservableProperty]
    private object? _currentViewModel;

    [ObservableProperty]
    private bool _isMonitoringActive;

    [ObservableProperty]
    private ItemCardViewModel? _selectedItemForDetails;

    [ObservableProperty]
    private bool _isDetailsOpen;

    // View models
    public OverviewViewModel OverviewVm { get; }
    public ClipboardListViewModel ListVm { get; }
    public SnippetsViewModel SnippetsVm { get; }
    public PasteQueueViewModel QueueVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public event Action? RequestOpenFloatingPanel;
    public event Action<string, string>? NotificationRequested;

    public MainViewModel(
        IClipboardMonitor clipboardMonitor,
        IClipboardService clipboardService,
        IClipRepository repository,
        ISettingsService settingsService,
        IThemeService themeService,
        OverviewViewModel overviewVm,
        ClipboardListViewModel listVm,
        SnippetsViewModel snippetsVm,
        PasteQueueViewModel queueVm,
        SettingsViewModel settingsVm,
        ILogger<MainViewModel> logger)
    {
        _clipboardMonitor = clipboardMonitor;
        _clipboardService = clipboardService;
        _repository = repository;
        _settingsService = settingsService;
        _themeService = themeService;
        _logger = logger;

        OverviewVm = overviewVm;
        ListVm = listVm;
        SnippetsVm = snippetsVm;
        QueueVm = queueVm;
        SettingsVm = settingsVm;

        // Subscribe to events
        _clipboardMonitor.ClipboardChanged += OnClipboardChanged;

        OverviewVm.ItemAddToQueueRequested += AddToQueueFromCard;
        ListVm.ItemAddToQueueRequested += AddToQueueFromCard;

        OverviewVm.ItemDetailsRequested += ShowItemDetails;
        ListVm.ItemDetailsRequested += ShowItemDetails;

        QueueVm.NotificationRequested += (title, msg) => NotificationRequested?.Invoke(title, msg);

        UpdateMonitoringState();
        _settingsService.SettingsChanged += (s, e) => UpdateMonitoringState();

        // Default navigation
        Navigate("Overview");
    }

    private void UpdateMonitoringState()
    {
        var s = _settingsService.Settings;
        IsMonitoringActive = s.MonitoringEnabled && !s.PauseMonitoring;
    }

    private async void OnClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        try
        {
            var item = await _clipboardService.ProcessClipboardChangeAsync(e);
            if (item != null)
            {
                // Run retention check in background
                _ = Task.Run(async () =>
                {
                    var s = _settingsService.Settings;
                    await _repository.CleanupRetentionAsync(s.RetentionDays, s.MaxHistoryItems, s.MaxStorageMb * 1024 * 1024);
                });

                // Update UI on dispatcher
                System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
                {
                    if (CurrentViewModel == OverviewVm)
                    {
                        await OverviewVm.RefreshAsync();
                    }
                    else if (CurrentViewModel == ListVm)
                    {
                        await ListVm.LoadItemsAsync();
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling clipboard change event");
        }
    }

    [RelayCommand]
    public async Task NavigateAsync(string section)
    {
        Navigate(section);
        await Task.CompletedTask;
    }

    public void Navigate(string section)
    {
        ActiveSection = section;

        switch (section)
        {
            case "Overview":
                CurrentViewModel = OverviewVm;
                _ = OverviewVm.RefreshAsync();
                break;

            case "All Items":
                CurrentViewModel = ListVm;
                _ = ListVm.InitializeAsync("All Items", type: null, favoritesOnly: false);
                break;

            case "Favorites":
                CurrentViewModel = ListVm;
                _ = ListVm.InitializeAsync("Favorites", type: null, favoritesOnly: true);
                break;

            case "Text":
                CurrentViewModel = ListVm;
                _ = ListVm.InitializeAsync("Text", type: ClipContentType.Text, favoritesOnly: false);
                break;

            case "Links":
                CurrentViewModel = ListVm;
                _ = ListVm.InitializeAsync("Links", type: ClipContentType.Link, favoritesOnly: false);
                break;

            case "Images":
                CurrentViewModel = ListVm;
                _ = ListVm.InitializeAsync("Images", type: ClipContentType.Image, favoritesOnly: false);
                break;

            case "Files":
                CurrentViewModel = ListVm;
                _ = ListVm.InitializeAsync("Files", type: ClipContentType.Files, favoritesOnly: false);
                break;

            case "Snippets":
                CurrentViewModel = SnippetsVm;
                _ = SnippetsVm.LoadSnippetsAsync();
                break;

            case "Paste Queues":
                CurrentViewModel = QueueVm;
                _ = QueueVm.InitializeAsync();
                break;

            case "Settings":
                CurrentViewModel = SettingsVm;
                break;
        }
    }

    private void AddToQueueFromCard(ItemCardViewModel card)
    {
        _ = QueueVm.AddItemToQueueAsync(card.Model);
        NotificationRequested?.Invoke("Queue Updated", $"Added '{card.Title}' to queue");
    }

    private void ShowItemDetails(ItemCardViewModel card)
    {
        SelectedItemForDetails = card;
        IsDetailsOpen = true;
    }

    [RelayCommand]
    public void CloseDetails()
    {
        IsDetailsOpen = false;
        SelectedItemForDetails = null;
    }

    [RelayCommand]
    public void TriggerFloatingPanel()
    {
        RequestOpenFloatingPanel?.Invoke();
    }
}

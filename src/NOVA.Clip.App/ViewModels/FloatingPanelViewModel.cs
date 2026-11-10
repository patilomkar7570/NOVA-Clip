using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.App.ViewModels;

public partial class FloatingPanelViewModel : ObservableObject
{
    private readonly IClipRepository _repository;
    private readonly IClipboardService _clipboardService;
    private readonly IInputSimulator _inputSimulator;
    private readonly Action _openMainWindow;
    private readonly Action<ItemCardViewModel> _addToQueue;
    private readonly Action _openSettings;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "All"; // "All", "Favorites", "Text", "Links", "Images", "Files"

    [ObservableProperty]
    private int _selectedIndex = 0;

    [ObservableProperty]
    private ItemCardViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<ItemCardViewModel> Items { get; } = [];

    public event Action? CloseRequested;
    public event Action<string, string>? NotificationRequested;

    public FloatingPanelViewModel(
        IClipRepository repository,
        IClipboardService clipboardService,
        IInputSimulator inputSimulator,
        Action openMainWindow,
        Action<ItemCardViewModel> addToQueue,
        Action openSettings)
    {
        _repository = repository;
        _clipboardService = clipboardService;
        _inputSimulator = inputSimulator;
        _openMainWindow = openMainWindow;
        _addToQueue = addToQueue;
        _openSettings = openSettings;
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = LoadItemsAsync();
    }

    partial void OnSelectedFilterChanged(string value)
    {
        _ = LoadItemsAsync();
    }

    [RelayCommand]
    public async Task LoadItemsAsync()
    {
        IsLoading = true;
        try
        {
            IReadOnlyList<ClipItem> results;

            if (SelectedFilter == "Favorites")
            {
                if (string.IsNullOrWhiteSpace(SearchQuery))
                {
                    results = await _repository.GetFavoritesAsync(limit: 50);
                }
                else
                {
                    var matches = await _repository.SearchItemsAsync(SearchQuery, limit: 100);
                    results = matches.Where(m => m.IsFavorite).Take(50).ToList();
                }
            }
            else
            {
                ClipContentType? type = SelectedFilter switch
                {
                    "Text" => ClipContentType.Text,
                    "Links" => ClipContentType.Link,
                    "Images" => ClipContentType.Image,
                    "Files" => ClipContentType.Files,
                    _ => null
                };

                if (string.IsNullOrWhiteSpace(SearchQuery))
                {
                    results = type.HasValue
                        ? await _repository.GetItemsByTypeAsync(type.Value, limit: 50)
                        : await _repository.GetAllItemsAsync(limit: 50);
                }
                else
                {
                    results = await _repository.SearchItemsAsync(SearchQuery, type, limit: 50);
                }
            }

            Items.Clear();
            foreach (var item in results)
            {
                Items.Add(new ItemCardViewModel(
                    item,
                    _clipboardService,
                    _repository,
                    onDeleted: card => Items.Remove(card),
                    onAddToQueue: card => _addToQueue(card),
                    onViewDetails: null));
            }

            if (Items.Count > 0)
            {
                SelectedIndex = 0;
                SelectedItem = Items[0];
            }
            else
            {
                SelectedIndex = -1;
                SelectedItem = null;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void MoveSelectionUp()
    {
        if (Items.Count == 0) return;
        if (SelectedIndex > 0)
        {
            SelectedIndex--;
            SelectedItem = Items[SelectedIndex];
        }
    }

    public void MoveSelectionDown()
    {
        if (Items.Count == 0) return;
        if (SelectedIndex < Items.Count - 1)
        {
            SelectedIndex++;
            SelectedItem = Items[SelectedIndex];
        }
    }

    [RelayCommand]
    public async Task PasteSelectedItemAsync()
    {
        if (SelectedItem != null)
        {
            await PasteItemAsync(SelectedItem, IntPtr.Zero);
        }
    }

    public async Task PasteItemAsync(ItemCardViewModel? card, IntPtr targetHwnd)
    {
        var targetCard = card ?? SelectedItem;
        if (targetCard == null) return;

        CloseRequested?.Invoke();

        bool copied = await _clipboardService.RestoreToClipboardAsync(targetCard.Model);
        if (copied)
        {
            await _repository.UpdateLastUsedAsync(targetCard.Id);
            if (targetHwnd != IntPtr.Zero)
            {
                await _inputSimulator.SimulatePasteAsync(targetHwnd);
            }
        }
    }

    [RelayCommand]
    public async Task CopyItemAsync(ItemCardViewModel? card)
    {
        var targetCard = card ?? SelectedItem;
        if (targetCard == null) return;

        bool copied = await _clipboardService.RestoreToClipboardAsync(targetCard.Model);
        if (copied)
        {
            await _repository.UpdateLastUsedAsync(targetCard.Id);
            CloseRequested?.Invoke();
            NotificationRequested?.Invoke("Copied", targetCard.Title);
        }
    }

    [RelayCommand]
    public void OpenFullApp()
    {
        CloseRequested?.Invoke();
        _openMainWindow();
    }

    [RelayCommand]
    public void OpenSettings()
    {
        CloseRequested?.Invoke();
        _openSettings();
    }

    [RelayCommand]
    public void AddToQueue(ItemCardViewModel? card)
    {
        var target = card ?? SelectedItem;
        if (target != null)
        {
            _addToQueue(target);
            NotificationRequested?.Invoke("Added to Queue", target.Title);
        }
    }
}

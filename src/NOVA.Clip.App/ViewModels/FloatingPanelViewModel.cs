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
    private readonly IPhoneSyncServer? _phoneSyncServer;

    public string PhoneSyncUrl => _phoneSyncServer?.ServerUrl ?? "http://localhost:5892";
    public string PhoneSyncHint => $"Open on phone: {PhoneSyncUrl}";

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
    public ObservableCollection<ItemCardViewModel> RecentSlots { get; } = [];

    public event Action? CloseRequested;
    public event Action<string, string>? NotificationRequested;

    public FloatingPanelViewModel(
        IClipRepository repository,
        IClipboardService clipboardService,
        IInputSimulator inputSimulator,
        Action openMainWindow,
        Action<ItemCardViewModel> addToQueue,
        Action openSettings,
        IPhoneSyncServer? phoneSyncServer = null)
    {
        _repository = repository;
        _clipboardService = clipboardService;
        _inputSimulator = inputSimulator;
        _openMainWindow = openMainWindow;
        _addToQueue = addToQueue;
        _openSettings = openSettings;
        _phoneSyncServer = phoneSyncServer;
        _clipboardService.ItemCaptured += item => System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => LoadItemsAsync());
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

            RecentSlots.Clear();
            for (int i = 0; i < 4; i++)
            {
                if (i < Items.Count)
                {
                    var card = Items[i];
                    card.SlotNumber = i + 1;
                    card.IsEmptySlot = false;
                    RecentSlots.Add(card);
                }
                else
                {
                    RecentSlots.Add(ItemCardViewModel.CreateEmptySlot(i + 1));
                }
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

    [RelayCommand]
    public async Task SlotClickedAsync(ItemCardViewModel? card)
    {
        if (card == null || card.IsEmptySlot) return;

        bool copied = await _clipboardService.RestoreToClipboardAsync(card.Model);
        if (copied)
        {
            await _repository.UpdateLastUsedAsync(card.Id);
            card.IsCopied = true;
            await Task.Delay(350);
            card.IsCopied = false;
            CloseRequested?.Invoke();
            NotificationRequested?.Invoke("Copied", card.Title);
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
    public void OpenPhoneSync()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = PhoneSyncUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch { }

        NotificationRequested?.Invoke("Phone Sync", $"Open on your phone: {PhoneSyncUrl}");
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

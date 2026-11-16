using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.App.ViewModels;

public partial class ClipboardListViewModel : ObservableObject
{
    private readonly IClipRepository _repository;
    private readonly IClipboardService _clipboardService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _viewTitle = "All Items";

    [ObservableProperty]
    private ClipContentType? _filterType;

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _itemCount;

    [ObservableProperty]
    private bool _isBulkSelectMode;

    public ObservableCollection<ItemCardViewModel> Items { get; } = [];

    public event Action<ItemCardViewModel>? ItemAddToQueueRequested;
    public event Action<ItemCardViewModel>? ItemDetailsRequested;

    public ClipboardListViewModel(IClipRepository repository, IClipboardService clipboardService)
    {
        _repository = repository;
        _clipboardService = clipboardService;
        _clipboardService.ItemCaptured += item => System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => LoadItemsAsync());
    }

    public async Task InitializeAsync(string title, ClipContentType? type = null, bool favoritesOnly = false)
    {
        ViewTitle = title;
        FilterType = type;
        FavoritesOnly = favoritesOnly;
        SearchQuery = string.Empty;
        await LoadItemsAsync();
    }

    partial void OnSearchQueryChanged(string value)
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

            if (FavoritesOnly)
            {
                if (string.IsNullOrWhiteSpace(SearchQuery))
                {
                    results = await _repository.GetFavoritesAsync(limit: 300);
                }
                else
                {
                    var allMatches = await _repository.SearchItemsAsync(SearchQuery, FilterType, limit: 300);
                    results = allMatches.Where(m => m.IsFavorite).ToList();
                }
            }
            else if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                results = await _repository.SearchItemsAsync(SearchQuery, FilterType, limit: 300);
            }
            else if (FilterType.HasValue)
            {
                results = await _repository.GetItemsByTypeAsync(FilterType.Value, limit: 300);
            }
            else
            {
                results = await _repository.GetAllItemsAsync(limit: 300);
            }

            Items.Clear();
            foreach (var item in results)
            {
                Items.Add(new ItemCardViewModel(
                    item,
                    _clipboardService,
                    _repository,
                    onDeleted: card => { Items.Remove(card); ItemCount = Items.Count; },
                    onAddToQueue: card => ItemAddToQueueRequested?.Invoke(card),
                    onViewDetails: card => ItemDetailsRequested?.Invoke(card)));
            }

            ItemCount = Items.Count;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void ToggleBulkSelectMode()
    {
        IsBulkSelectMode = !IsBulkSelectMode;
        if (!IsBulkSelectMode)
        {
            foreach (var item in Items) item.IsSelected = false;
        }
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return;

        var ids = selected.Select(s => s.Id).ToList();
        await _repository.DeleteItemsAsync(ids);

        foreach (var s in selected)
        {
            Items.Remove(s);
        }
        ItemCount = Items.Count;
        IsBulkSelectMode = false;
    }

    [RelayCommand]
    public void SelectAll()
    {
        bool allSelected = Items.All(i => i.IsSelected);
        foreach (var item in Items) item.IsSelected = !allSelected;
    }
}

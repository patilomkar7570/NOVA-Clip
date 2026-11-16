using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.App.ViewModels;

public partial class ItemCardViewModel : ObservableObject
{
    private readonly ClipItem _model;
    private readonly IClipboardService _clipboardService;
    private readonly IClipRepository _repository;
    private readonly Action<ItemCardViewModel>? _onDeleted;
    private readonly Action<ItemCardViewModel>? _onAddToQueue;
    private readonly Action<ItemCardViewModel>? _onViewDetails;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string? _previewText;

    [ObservableProperty]
    private string? _secondaryInfo;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private DateTime _lastUsedAt;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isCopied;

    [ObservableProperty]
    private bool _isEmptySlot;

    [ObservableProperty]
    private int _slotNumber;

    public ClipItem Model => _model;
    public Guid Id => _model.Id;
    public ClipContentType Type => _model.Type;
    public string? SourceApplication => _model.SourceApplication;
    public string? SourceWindowTitle => _model.SourceWindowTitle;
    public DateTime CreatedAt => _model.CreatedAt;
    public long ByteSize => _model.ByteSize;
    public string? StorageAssetPath => _model.StorageAssetPath;

    public string TypeLabel => Type switch
    {
        ClipContentType.Link => "LINK",
        ClipContentType.Files => "FILE",
        ClipContentType.Image => "IMG",
        ClipContentType.RichText => "RICH",
        _ => "TEXT"
    };

    public string TypeColorHex => Type switch
    {
        ClipContentType.Link => "#3B82F6",
        ClipContentType.Files => "#10B981",
        ClipContentType.Image => "#A855F7",
        ClipContentType.RichText => "#EC4899",
        _ => "#60A5FA"
    };

    public string TypeBgHex => Type switch
    {
        ClipContentType.Link => "#1E293B",
        ClipContentType.Files => "#064E3B",
        ClipContentType.Image => "#3B0764",
        ClipContentType.RichText => "#500724",
        _ => "#1E293B"
    };

    public string TypeIconKey => Type switch
    {
        ClipContentType.Link => "IconLink",
        ClipContentType.Files => "IconFiles",
        ClipContentType.Image => "IconImage",
        ClipContentType.RichText => "IconRichText",
        _ => "IconText"
    };

    public bool HasContent => !IsEmptySlot;

    public System.Windows.Media.Brush TypeBrush
    {
        get
        {
            try
            {
                return (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(TypeColorHex)!;
            }
            catch
            {
                return System.Windows.Media.Brushes.DodgerBlue;
            }
        }
    }

    public System.Windows.Media.Geometry? TypeIconGeometry
    {
        get
        {
            if (System.Windows.Application.Current != null &&
                System.Windows.Application.Current.TryFindResource(TypeIconKey) is System.Windows.Media.Geometry geo)
            {
                return geo;
            }
            return null;
        }
    }

    public string FriendlyTime => FormatFriendlyTime(_model.LastUsedAt);

    public ItemCardViewModel(
        ClipItem model,
        IClipboardService clipboardService,
        IClipRepository repository,
        Action<ItemCardViewModel>? onDeleted = null,
        Action<ItemCardViewModel>? onAddToQueue = null,
        Action<ItemCardViewModel>? onViewDetails = null)
    {
        _model = model;
        _clipboardService = clipboardService;
        _repository = repository;
        _onDeleted = onDeleted;
        _onAddToQueue = onAddToQueue;
        _onViewDetails = onViewDetails;

        _title = model.Title;
        _previewText = model.PreviewText;
        _secondaryInfo = model.SecondaryInfo;
        _isFavorite = model.IsFavorite;
        _lastUsedAt = model.LastUsedAt;
    }

    [RelayCommand]
    public async Task CopyAsync()
    {
        bool success = await _clipboardService.RestoreToClipboardAsync(_model);
        if (success)
        {
            await _repository.UpdateLastUsedAsync(Id);
            LastUsedAt = DateTime.UtcNow;
            _model.LastUsedAt = LastUsedAt;
            OnPropertyChanged(nameof(FriendlyTime));
        }
    }

    [RelayCommand]
    public async Task ToggleFavoriteAsync()
    {
        bool success = await _repository.ToggleFavoriteAsync(Id);
        if (success)
        {
            IsFavorite = !IsFavorite;
            _model.IsFavorite = IsFavorite;
        }
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        bool success = await _repository.DeleteItemAsync(Id);
        if (success)
        {
            _onDeleted?.Invoke(this);
        }
    }

    [RelayCommand]
    public void AddToQueue()
    {
        _onAddToQueue?.Invoke(this);
    }

    [RelayCommand]
    public void ViewDetails()
    {
        _onViewDetails?.Invoke(this);
    }

    public async Task<bool> RenameAsync(string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle)) return false;
        Title = newTitle.Trim();
        _model.Title = Title;
        return await _repository.UpdateItemAsync(_model);
    }

    public static ItemCardViewModel CreateEmptySlot(int slotNumber)
    {
        var dummy = new ClipItem
        {
            Id = Guid.Empty,
            Type = ClipContentType.Text,
            Title = "Empty slot",
            PreviewText = "Copy something",
            SecondaryInfo = $"Slot {slotNumber}",
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        };

        return new ItemCardViewModel(dummy, null!, null!)
        {
            IsEmptySlot = true,
            SlotNumber = slotNumber
        };
    }

    private static string FormatFriendlyTime(DateTime time)
    {
        var span = DateTime.UtcNow - time;
        if (span.TotalSeconds < 60) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return time.ToLocalTime().ToString("MMM dd, yyyy");
    }
}

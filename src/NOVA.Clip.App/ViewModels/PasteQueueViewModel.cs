using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.App.ViewModels;

public partial class PasteQueueViewModel : ObservableObject
{
    private readonly IClipRepository _repository;
    private readonly IClipboardService _clipboardService;
    private readonly IInputSimulator _inputSimulator;

    [ObservableProperty]
    private PasteQueue? _activeQueue;

    [ObservableProperty]
    private int _currentIndex;

    [ObservableProperty]
    private string _queueName = "Default Queue";

    [ObservableProperty]
    private string _selectedSeparator = "\n";

    [ObservableProperty]
    private string _customSeparator = "";

    [ObservableProperty]
    private string _statusFeedback = "Ready";

    [ObservableProperty]
    private bool _isCompatibleForPasteAll;

    [ObservableProperty]
    private string _pasteAllWarning = string.Empty;

    public ObservableCollection<PasteQueue> AvailableQueues { get; } = [];
    public ObservableCollection<PasteQueueItem> QueueItems { get; } = [];

    public event Action<string, string>? NotificationRequested;

    public PasteQueueViewModel(
        IClipRepository repository,
        IClipboardService clipboardService,
        IInputSimulator inputSimulator)
    {
        _repository = repository;
        _clipboardService = clipboardService;
        _inputSimulator = inputSimulator;
    }

    public async Task InitializeAsync()
    {
        await LoadQueuesAsync();
    }

    [RelayCommand]
    public async Task LoadQueuesAsync()
    {
        var queues = await _repository.GetAllQueuesAsync();
        AvailableQueues.Clear();

        foreach (var q in queues)
        {
            AvailableQueues.Add(q);
        }

        if (AvailableQueues.Count == 0)
        {
            var defaultQueue = new PasteQueue
            {
                Id = Guid.NewGuid(),
                Name = "Default Queue",
                CurrentIndex = 0,
                Separator = "\n"
            };
            await _repository.SaveQueueAsync(defaultQueue);
            AvailableQueues.Add(defaultQueue);
        }

        SelectQueue(AvailableQueues[0]);
    }

    public void SelectQueue(PasteQueue queue)
    {
        ActiveQueue = queue;
        QueueName = queue.Name;
        CurrentIndex = queue.CurrentIndex;
        SelectedSeparator = queue.Separator;

        QueueItems.Clear();
        foreach (var item in queue.Items.OrderBy(i => i.OrderIndex))
        {
            QueueItems.Add(item);
        }

        EvaluatePasteAllCompatibility();
    }

    public async Task AddItemToQueueAsync(ClipItem clipItem)
    {
        if (ActiveQueue == null) return;

        // Create safe snapshot
        byte[]? payloadBytes = clipItem.EncryptedPayload;
        if (payloadBytes == null && !string.IsNullOrEmpty(clipItem.StorageAssetPath))
        {
            var p = await _clipboardService.GetPayloadAsync(clipItem);
            if (p != null)
            {
                byte[] raw = PayloadData.Serialize(p);
                payloadBytes = raw;
            }
        }

        var queueItem = new PasteQueueItem
        {
            Id = Guid.NewGuid(),
            QueueId = ActiveQueue.Id,
            SourceClipItemId = clipItem.Id,
            OrderIndex = QueueItems.Count,
            Type = clipItem.Type,
            Title = clipItem.Title,
            PreviewText = clipItem.PreviewText ?? clipItem.Title,
            EncryptedPayload = payloadBytes,
            StorageAssetPath = clipItem.StorageAssetPath,
            IsCompleted = false
        };

        QueueItems.Add(queueItem);
        ActiveQueue.Items.Add(queueItem);
        await SaveCurrentQueueStateAsync();
        EvaluatePasteAllCompatibility();

        StatusFeedback = $"Added '{clipItem.Title}' to {ActiveQueue.Name}";
    }

    [RelayCommand]
    public async Task CreateNewQueueAsync(string? newName = null)
    {
        string name = string.IsNullOrWhiteSpace(newName) ? $"Queue {AvailableQueues.Count + 1}" : newName.Trim();
        var q = new PasteQueue
        {
            Id = Guid.NewGuid(),
            Name = name,
            CurrentIndex = 0,
            Separator = "\n"
        };
        await _repository.SaveQueueAsync(q);
        AvailableQueues.Add(q);
        SelectQueue(q);
    }

    [RelayCommand]
    public async Task DeleteQueueAsync()
    {
        if (ActiveQueue == null) return;
        var toDelete = ActiveQueue;

        await _repository.DeleteQueueAsync(toDelete.Id);
        AvailableQueues.Remove(toDelete);

        if (AvailableQueues.Count > 0)
        {
            SelectQueue(AvailableQueues[0]);
        }
        else
        {
            await CreateNewQueueAsync("Default Queue");
        }
    }

    [RelayCommand]
    public async Task MoveItemUpAsync(PasteQueueItem item)
    {
        int index = QueueItems.IndexOf(item);
        if (index > 0)
        {
            QueueItems.Move(index, index - 1);
            RebuildOrderIndices();
            await SaveCurrentQueueStateAsync();
        }
    }

    [RelayCommand]
    public async Task MoveItemDownAsync(PasteQueueItem item)
    {
        int index = QueueItems.IndexOf(item);
        if (index >= 0 && index < QueueItems.Count - 1)
        {
            QueueItems.Move(index, index + 1);
            RebuildOrderIndices();
            await SaveCurrentQueueStateAsync();
        }
    }

    [RelayCommand]
    public async Task RemoveItemAsync(PasteQueueItem item)
    {
        QueueItems.Remove(item);
        RebuildOrderIndices();
        if (CurrentIndex >= QueueItems.Count)
        {
            CurrentIndex = Math.Max(0, QueueItems.Count - 1);
        }
        await SaveCurrentQueueStateAsync();
        EvaluatePasteAllCompatibility();
    }

    [RelayCommand]
    public async Task ClearItemsAsync()
    {
        QueueItems.Clear();
        CurrentIndex = 0;
        await SaveCurrentQueueStateAsync();
        EvaluatePasteAllCompatibility();
        StatusFeedback = "Queue cleared";
    }

    [RelayCommand]
    public async Task ResetPositionAsync()
    {
        CurrentIndex = 0;
        foreach (var item in QueueItems) item.IsCompleted = false;
        await SaveCurrentQueueStateAsync();
        StatusFeedback = "Queue position reset to start";
    }

    // FEATURE A: COPY NEXT
    [RelayCommand]
    public async Task<bool> CopyNextAsync()
    {
        if (QueueItems.Count == 0 || CurrentIndex < 0 || CurrentIndex >= QueueItems.Count)
        {
            StatusFeedback = "Queue is completed or empty";
            return false;
        }

        var currentItem = QueueItems[CurrentIndex];
        var payload = await _clipboardService.GetPayloadFromQueueItemAsync(currentItem);
        if (payload == null)
        {
            StatusFeedback = $"Unable to read content for '{currentItem.Title}'";
            return false;
        }

        bool copied = await _clipboardService.RestorePayloadToClipboardAsync(payload, currentItem.Type);
        if (copied)
        {
            currentItem.IsCompleted = true;
            CurrentIndex++;
            await SaveCurrentQueueStateAsync();

            StatusFeedback = $"Copied #{CurrentIndex}: {currentItem.Title}. Paste with Ctrl+V.";
            NotificationRequested?.Invoke("Copy Next", $"Copied: {currentItem.Title}");
            return true;
        }

        return false;
    }

    // FEATURE B: PASTE NEXT
    [RelayCommand]
    public async Task<bool> PasteNextAsync(IntPtr targetHwnd)
    {
        if (QueueItems.Count == 0 || CurrentIndex < 0 || CurrentIndex >= QueueItems.Count)
        {
            StatusFeedback = "Queue is completed or empty";
            return false;
        }

        var currentItem = QueueItems[CurrentIndex];
        var payload = await _clipboardService.GetPayloadFromQueueItemAsync(currentItem);
        if (payload == null)
        {
            StatusFeedback = $"Payload missing for '{currentItem.Title}'";
            return false;
        }

        // Restore content to Windows clipboard
        bool copied = await _clipboardService.RestorePayloadToClipboardAsync(payload, currentItem.Type);
        if (!copied)
        {
            StatusFeedback = "Clipboard restore failed";
            return false;
        }

        // SendInput Ctrl+V to target application
        bool pasted = await _inputSimulator.SimulatePasteAsync(targetHwnd);
        if (pasted)
        {
            currentItem.IsCompleted = true;
            CurrentIndex++;
            await SaveCurrentQueueStateAsync();

            StatusFeedback = $"Pasted item #{CurrentIndex}: {currentItem.Title}";
            NotificationRequested?.Invoke("Paste Next", $"Pasted: {currentItem.Title}");
            return true;
        }
        else
        {
            // Graceful fallback to Copy Next without losing position
            StatusFeedback = "Target window paste injection failed. Item remains on clipboard (use Ctrl+V).";
            NotificationRequested?.Invoke("Paste Fallback", "Target window could not accept paste. Copied to clipboard instead.");
            return false;
        }
    }

    // FEATURE C: PASTE ALL
    [RelayCommand]
    public async Task<bool> PasteAllAsync(IntPtr targetHwnd)
    {
        if (QueueItems.Count == 0) return false;

        EvaluatePasteAllCompatibility();
        if (!IsCompatibleForPasteAll)
        {
            StatusFeedback = PasteAllWarning;
            return false;
        }

        // All text / links
        if (QueueItems.All(i => i.Type is ClipContentType.Text or ClipContentType.Link or ClipContentType.RichText))
        {
            var textParts = new List<string>();
            foreach (var item in QueueItems)
            {
                var payload = await _clipboardService.GetPayloadFromQueueItemAsync(item);
                string text = payload?.PlainText ?? item.PreviewText;
                textParts.Add(text);
            }

            string separator = SelectedSeparator == "Custom" ? CustomSeparator : SelectedSeparator;
            if (separator == "\\n") separator = "\n";
            if (separator == "\\t") separator = "\t";

            string combined = string.Join(separator, textParts);
            var combinedPayload = new PayloadData { PlainText = combined };

            await _clipboardService.RestorePayloadToClipboardAsync(combinedPayload, ClipContentType.Text);

            if (targetHwnd != IntPtr.Zero)
            {
                await _inputSimulator.SimulatePasteAsync(targetHwnd);
            }

            foreach (var item in QueueItems) item.IsCompleted = true;
            CurrentIndex = QueueItems.Count;
            await SaveCurrentQueueStateAsync();

            StatusFeedback = $"Pasted all {QueueItems.Count} items combined";
            NotificationRequested?.Invoke("Paste All", $"Combined {QueueItems.Count} items pasted");
            return true;
        }

        // All files
        if (QueueItems.All(i => i.Type == ClipContentType.Files))
        {
            var allFiles = new List<string>();
            foreach (var item in QueueItems)
            {
                var payload = await _clipboardService.GetPayloadFromQueueItemAsync(item);
                if (payload?.FilePaths != null)
                {
                    allFiles.AddRange(payload.FilePaths);
                }
            }

            var combinedPayload = new PayloadData { FilePaths = allFiles.Distinct().ToArray() };
            await _clipboardService.RestorePayloadToClipboardAsync(combinedPayload, ClipContentType.Files);

            if (targetHwnd != IntPtr.Zero)
            {
                await _inputSimulator.SimulatePasteAsync(targetHwnd);
            }

            foreach (var item in QueueItems) item.IsCompleted = true;
            CurrentIndex = QueueItems.Count;
            await SaveCurrentQueueStateAsync();

            StatusFeedback = $"Pasted all {allFiles.Count} file references";
            return true;
        }

        return false;
    }

    private void EvaluatePasteAllCompatibility()
    {
        if (QueueItems.Count == 0)
        {
            IsCompatibleForPasteAll = false;
            PasteAllWarning = "Queue is empty";
            return;
        }

        bool allText = QueueItems.All(i => i.Type is ClipContentType.Text or ClipContentType.Link or ClipContentType.RichText);
        bool allFiles = QueueItems.All(i => i.Type == ClipContentType.Files);
        bool hasImages = QueueItems.Any(i => i.Type == ClipContentType.Image);

        if (allText)
        {
            IsCompatibleForPasteAll = true;
            PasteAllWarning = string.Empty;
        }
        else if (allFiles)
        {
            IsCompatibleForPasteAll = true;
            PasteAllWarning = string.Empty;
        }
        else if (hasImages)
        {
            IsCompatibleForPasteAll = false;
            PasteAllWarning = "Paste All is disabled because images cannot be concatenated into a single paste operation.";
        }
        else
        {
            IsCompatibleForPasteAll = false;
            PasteAllWarning = "Queue contains mixed incompatible content types.";
        }
    }

    private void RebuildOrderIndices()
    {
        for (int i = 0; i < QueueItems.Count; i++)
        {
            QueueItems[i].OrderIndex = i;
        }
    }

    private async Task SaveCurrentQueueStateAsync()
    {
        if (ActiveQueue == null) return;

        ActiveQueue.CurrentIndex = CurrentIndex;
        ActiveQueue.Separator = SelectedSeparator;
        ActiveQueue.Items = QueueItems.ToList();
        ActiveQueue.UpdatedAt = DateTime.UtcNow;

        await _repository.SaveQueueAsync(ActiveQueue);
    }
}

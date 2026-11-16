using NOVA.Clip.Core.Models;

namespace NOVA.Clip.Core.Interfaces;

public interface IClipboardService
{
    event Action<ClipItem>? ItemCaptured;
    Task<ClipItem?> ProcessClipboardChangeAsync(ClipboardChangedEventArgs args);
    Task<bool> RestoreToClipboardAsync(ClipItem item);
    Task<bool> RestorePayloadToClipboardAsync(PayloadData payload, ClipContentType type);
    Task<PayloadData?> GetPayloadAsync(ClipItem item);
    Task<PayloadData?> GetPayloadFromQueueItemAsync(PasteQueueItem queueItem);
    Task<ClipItem?> IngestTextAsync(string text, string sourceApp);
    Task<ClipItem?> IngestFilesAsync(string[] filePaths, string sourceApp);
    bool SetSelfCopySequence();
    bool IsSelfCopySequence();
}

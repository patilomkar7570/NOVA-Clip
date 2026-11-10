using NOVA.Clip.Core.Models;

namespace NOVA.Clip.Core.Interfaces;

public interface IClipboardService
{
    Task<ClipItem?> ProcessClipboardChangeAsync(ClipboardChangedEventArgs args);
    Task<bool> RestoreToClipboardAsync(ClipItem item);
    Task<bool> RestorePayloadToClipboardAsync(PayloadData payload, ClipContentType type);
    Task<PayloadData?> GetPayloadAsync(ClipItem item);
    Task<PayloadData?> GetPayloadFromQueueItemAsync(PasteQueueItem queueItem);
    bool SetSelfCopySequence();
    bool IsSelfCopySequence();
}

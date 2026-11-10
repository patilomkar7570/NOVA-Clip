using NOVA.Clip.Core.Models;

namespace NOVA.Clip.Core.Interfaces;

public interface IClipRepository
{
    Task InitializeAsync();

    // Clipboard Items
    Task<IReadOnlyList<ClipItem>> GetAllItemsAsync(int limit = 200, int offset = 0);
    Task<IReadOnlyList<ClipItem>> GetItemsByTypeAsync(ClipContentType type, int limit = 200, int offset = 0);
    Task<IReadOnlyList<ClipItem>> GetFavoritesAsync(int limit = 200);
    Task<IReadOnlyList<ClipItem>> SearchItemsAsync(string query, ClipContentType? type = null, int limit = 100);
    Task<ClipItem?> GetItemByIdAsync(Guid id);
    Task<ClipItem?> FindDuplicateAsync(string contentHash);
    Task<bool> AddItemAsync(ClipItem item);
    Task<bool> UpdateItemAsync(ClipItem item);
    Task<bool> UpdateLastUsedAsync(Guid id);
    Task<bool> ToggleFavoriteAsync(Guid id);
    Task<bool> DeleteItemAsync(Guid id);
    Task<int> DeleteItemsAsync(IEnumerable<Guid> ids);
    Task<int> ClearAllHistoryAsync();
    Task<int> ClearImagesAsync();
    Task<int> CleanupRetentionAsync(int retentionDays, int maxItems, long maxStorageBytes);

    // Snippets
    Task<IReadOnlyList<SnippetItem>> GetAllSnippetsAsync();
    Task<SnippetItem?> GetSnippetByIdAsync(Guid id);
    Task<bool> AddSnippetAsync(SnippetItem snippet);
    Task<bool> UpdateSnippetAsync(SnippetItem snippet);
    Task<bool> DeleteSnippetAsync(Guid id);

    // Queues
    Task<IReadOnlyList<PasteQueue>> GetAllQueuesAsync();
    Task<PasteQueue?> GetQueueByIdAsync(Guid id);
    Task<bool> SaveQueueAsync(PasteQueue queue);
    Task<bool> DeleteQueueAsync(Guid id);

    // Storage stats
    Task<StorageUsageInfo> GetStorageUsageAsync();
}

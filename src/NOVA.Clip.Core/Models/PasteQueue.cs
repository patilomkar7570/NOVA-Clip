namespace NOVA.Clip.Core.Models;

public class PasteQueueItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QueueId { get; set; }
    public Guid? SourceClipItemId { get; set; }
    public int OrderIndex { get; set; }
    public ClipContentType Type { get; set; } = ClipContentType.Text;
    public string Title { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
    public byte[]? EncryptedPayload { get; set; }
    public string? StorageAssetPath { get; set; }
    public bool IsCompleted { get; set; }
}

public class PasteQueue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default Queue";
    public List<PasteQueueItem> Items { get; set; } = [];
    public int CurrentIndex { get; set; } = 0;
    public string Separator { get; set; } = "\n";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public PasteQueueItem? GetCurrentItem()
    {
        if (Items.Count == 0 || CurrentIndex < 0 || CurrentIndex >= Items.Count)
            return null;

        return Items[CurrentIndex];
    }
}

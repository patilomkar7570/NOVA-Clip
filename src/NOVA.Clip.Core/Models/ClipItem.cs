namespace NOVA.Clip.Core.Models;

public class ClipItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ClipContentType Type { get; set; } = ClipContentType.Text;
    public string Title { get; set; } = string.Empty;
    public string? PreviewText { get; set; }
    public string? SecondaryInfo { get; set; }
    public string? SourceApplication { get; set; }
    public string? SourceWindowTitle { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    public bool IsFavorite { get; set; }
    public long ByteSize { get; set; }
    public string? ContentHash { get; set; }
    public byte[]? EncryptedPayload { get; set; }
    public string? StorageAssetPath { get; set; }

    public ClipItem Clone()
    {
        return new ClipItem
        {
            Id = Id,
            Type = Type,
            Title = Title,
            PreviewText = PreviewText,
            SecondaryInfo = SecondaryInfo,
            SourceApplication = SourceApplication,
            SourceWindowTitle = SourceWindowTitle,
            CreatedAt = CreatedAt,
            LastUsedAt = LastUsedAt,
            IsFavorite = IsFavorite,
            ByteSize = ByteSize,
            ContentHash = ContentHash,
            EncryptedPayload = EncryptedPayload != null ? (byte[])EncryptedPayload.Clone() : null,
            StorageAssetPath = StorageAssetPath
        };
    }
}

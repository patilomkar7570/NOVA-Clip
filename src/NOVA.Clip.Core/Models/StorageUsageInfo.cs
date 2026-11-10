namespace NOVA.Clip.Core.Models;

public class StorageUsageInfo
{
    public long DatabaseSizeBytes { get; set; }
    public long StorageAssetsSizeBytes { get; set; }
    public long TotalSizeBytes => DatabaseSizeBytes + StorageAssetsSizeBytes;
    public int TotalItemsCount { get; set; }
    public int TextItemsCount { get; set; }
    public int LinkItemsCount { get; set; }
    public int RichTextItemsCount { get; set; }
    public int ImageItemsCount { get; set; }
    public int FileItemsCount { get; set; }
    public int SnippetsCount { get; set; }
    public int QueuesCount { get; set; }

    public string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    public string FormattedTotalSize => FormatBytes(TotalSizeBytes);
    public string FormattedDatabaseSize => FormatBytes(DatabaseSizeBytes);
    public string FormattedAssetsSize => FormatBytes(StorageAssetsSizeBytes);
}

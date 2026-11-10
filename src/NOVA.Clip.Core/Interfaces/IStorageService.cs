namespace NOVA.Clip.Core.Interfaces;

public interface IStorageService
{
    string AppDataDirectory { get; }
    string DatabasePath { get; }
    string AssetsDirectory { get; }

    Task<string> SaveAssetAsync(Guid itemId, byte[] encryptedData, string extension);
    Task<byte[]?> LoadAssetAsync(string relativeOrFullPath);
    Task<bool> DeleteAssetAsync(string? relativeOrFullPath);
    Task<int> CleanupOrphanedAssetsAsync(IEnumerable<string> validAssetPaths);
    long GetAssetsDirectorySizeBytes();
}

using System.IO;
using Microsoft.Extensions.Logging;
using NOVA.Clip.Core.Interfaces;

namespace NOVA.Clip.Infrastructure.Storage;

public class StorageService : IStorageService
{
    private readonly ILogger<StorageService> _logger;
    private readonly string _appDataDir;
    private readonly string _databasePath;
    private readonly string _assetsDir;

    public StorageService(ILogger<StorageService> logger, string? customAppDataDir = null)
    {
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(customAppDataDir))
        {
            _appDataDir = customAppDataDir;
        }
        else
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _appDataDir = Path.Combine(localAppData, "NOVA.Clip");
        }

        _databasePath = Path.Combine(_appDataDir, "nova_clip.db");
        _assetsDir = Path.Combine(_appDataDir, "Assets");

        EnsureDirectories();
    }

    private void EnsureDirectories()
    {
        try
        {
            if (!Directory.Exists(_appDataDir))
                Directory.CreateDirectory(_appDataDir);

            if (!Directory.Exists(_assetsDir))
                Directory.CreateDirectory(_assetsDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create application storage directories at {Dir}", _appDataDir);
        }
    }

    public string AppDataDirectory => _appDataDir;
    public string DatabasePath => _databasePath;
    public string AssetsDirectory => _assetsDir;

    public async Task<string> SaveAssetAsync(Guid itemId, byte[] encryptedData, string extension)
    {
        EnsureDirectories();
        string cleanExt = extension.StartsWith('.') ? extension : "." + extension;
        string fileName = $"{itemId:N}{cleanExt}";
        string fullPath = Path.Combine(_assetsDir, fileName);

        await File.WriteAllBytesAsync(fullPath, encryptedData);
        return Path.Combine("Assets", fileName);
    }

    public async Task<byte[]?> LoadAssetAsync(string relativeOrFullPath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrFullPath)) return null;

        string fullPath = Path.IsPathRooted(relativeOrFullPath)
            ? relativeOrFullPath
            : Path.Combine(_appDataDir, relativeOrFullPath);

        if (!File.Exists(fullPath)) return null;

        return await File.ReadAllBytesAsync(fullPath);
    }

    public Task<bool> DeleteAssetAsync(string? relativeOrFullPath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrFullPath)) return Task.FromResult(false);

        string fullPath = Path.IsPathRooted(relativeOrFullPath)
            ? relativeOrFullPath
            : Path.Combine(_appDataDir, relativeOrFullPath);

        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete storage asset at {Path}", fullPath);
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(false);
    }

    public Task<int> CleanupOrphanedAssetsAsync(IEnumerable<string> validAssetPaths)
    {
        int deletedCount = 0;
        if (!Directory.Exists(_assetsDir)) return Task.FromResult(0);

        var validSet = new HashSet<string>(
            validAssetPaths.Where(p => !string.IsNullOrWhiteSpace(p))
                           .Select(p => Path.GetFileName(p).ToLowerInvariant())
        );

        foreach (var file in Directory.EnumerateFiles(_assetsDir))
        {
            string fileName = Path.GetFileName(file).ToLowerInvariant();
            if (!validSet.Contains(fileName))
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete orphaned asset at {Path}", file);
                }
            }
        }

        return Task.FromResult(deletedCount);
    }

    public long GetAssetsDirectorySizeBytes()
    {
        if (!Directory.Exists(_assetsDir)) return 0;
        long totalBytes = 0;
        foreach (var file in Directory.EnumerateFiles(_assetsDir))
        {
            try
            {
                totalBytes += new FileInfo(file).Length;
            }
            catch { }
        }
        return totalBytes;
    }
}

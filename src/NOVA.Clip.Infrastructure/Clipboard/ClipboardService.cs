using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using NOVA.Clip.Core.Configuration;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.Infrastructure.Clipboard;

public class ClipboardService : IClipboardService
{
    public event Action<ClipItem>? ItemCaptured;

    private readonly IClipRepository _repository;
    private readonly IDataProtectionService _dataProtection;
    private readonly IPrivacyFilter _privacyFilter;
    private readonly IStorageService _storageService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ClipboardService> _logger;

    private DateTime _lastSelfCopyTime = DateTime.MinValue;
    private readonly object _lock = new();

    public ClipboardService(
        IClipRepository repository,
        IDataProtectionService dataProtection,
        IPrivacyFilter privacyFilter,
        IStorageService storageService,
        ISettingsService settingsService,
        ILogger<ClipboardService> logger)
    {
        _repository = repository;
        _dataProtection = dataProtection;
        _privacyFilter = privacyFilter;
        _storageService = storageService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public bool SetSelfCopySequence()
    {
        lock (_lock)
        {
            _lastSelfCopyTime = DateTime.UtcNow;
            return true;
        }
    }

    public bool IsSelfCopySequence()
    {
        lock (_lock)
        {
            return (DateTime.UtcNow - _lastSelfCopyTime).TotalMilliseconds < 1200;
        }
    }

    public async Task<ClipItem?> ProcessClipboardChangeAsync(ClipboardChangedEventArgs args)
    {
        var settings = _settingsService.Settings;

        if (!settings.MonitoringEnabled || settings.PauseMonitoring)
        {
            return null;
        }

        if (IsSelfCopySequence())
        {
            _logger.LogDebug("Ignored self-induced clipboard change");
            return null;
        }

        // Check excluded applications
        if (_privacyFilter.IsProcessExcluded(args.ProcessName, settings.ExcludedProcesses))
        {
            _logger.LogInformation("Skipped clipboard capture from excluded application: {App}", args.ProcessName);
            return null;
        }

        // Read clipboard with bounded retry on STA thread
        var snapshot = await ReadClipboardWithRetryAsync();
        if (snapshot == null) return null;

        ClipItem? item = null;
        try
        {
            // 1. Files format
            if (settings.CaptureFiles && snapshot.Files != null && snapshot.Files.Length > 0)
            {
                item = await ProcessFilesAsync(snapshot.Files, args);
            }
            // 2. Image format
            else if (settings.CaptureImages && snapshot.ImageData != null)
            {
                item = await ProcessImageAsync(snapshot.ImageData, snapshot.ImageWidth, snapshot.ImageHeight, args);
            }
            // 3. HTML / RTF Rich text
            else if (settings.CaptureRichText && (snapshot.Html != null || snapshot.Rtf != null))
            {
                item = await ProcessRichTextAsync(snapshot.Text, snapshot.Html, snapshot.Rtf, args);
            }
            // 4. Plain text / Links
            else if (!string.IsNullOrWhiteSpace(snapshot.Text))
            {
                item = await ProcessTextOrLinkAsync(snapshot.Text, args);
            }

            if (item != null)
            {
                ItemCaptured?.Invoke(item);
            }

            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing clipboard content");
        }

        return null;
    }

    private async Task<ClipItem?> ProcessTextOrLinkAsync(string text, ClipboardChangedEventArgs args)
    {
        var settings = _settingsService.Settings;

        // Privacy filter check
        if (settings.SensitiveContentProtectionEnabled && _privacyFilter.IsSensitiveText(text, out string? reason))
        {
            _logger.LogInformation("Skipped capture of sensitive clipboard text: {Reason}", reason);
            return null;
        }

        bool isUrl = IsValidUrl(text.Trim(), out string? domain);
        if (isUrl && !settings.CaptureLinks) return null;
        if (!isUrl && !settings.CaptureText) return null;

        var type = isUrl ? ClipContentType.Link : ClipContentType.Text;
        string hash = ComputeHash(text);

        // Duplicate check
        if (settings.DuplicateDetectionEnabled)
        {
            var existing = await _repository.FindDuplicateAsync(hash);
            if (existing != null)
            {
                await _repository.UpdateLastUsedAsync(existing.Id);
                existing.LastUsedAt = DateTime.UtcNow;
                return existing;
            }
        }

        var payload = new PayloadData { PlainText = text };
        byte[] payloadBytes = PayloadData.Serialize(payload);
        byte[] encrypted = _dataProtection.Encrypt(payloadBytes);

        string title = TruncateSingleLine(text, 80);
        string preview = TruncateMultiLine(text, 300);

        var item = new ClipItem
        {
            Id = Guid.NewGuid(),
            Type = type,
            Title = title,
            PreviewText = preview,
            SecondaryInfo = isUrl ? domain : $"{text.Length} characters",
            SourceApplication = args.ProcessName,
            SourceWindowTitle = args.WindowTitle,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ByteSize = payloadBytes.Length,
            ContentHash = hash,
            EncryptedPayload = encrypted
        };

        await _repository.AddItemAsync(item);
        return item;
    }

    private async Task<ClipItem?> ProcessRichTextAsync(string? plain, string? html, string? rtf, ClipboardChangedEventArgs args)
    {
        var settings = _settingsService.Settings;

        string textToCheck = plain ?? string.Empty;
        if (settings.SensitiveContentProtectionEnabled && _privacyFilter.IsSensitiveText(textToCheck, out string? reason))
        {
            _logger.LogInformation("Skipped capture of sensitive rich text: {Reason}", reason);
            return null;
        }

        string rawContent = (plain ?? html ?? rtf) ?? string.Empty;
        string hash = ComputeHash(rawContent);

        if (settings.DuplicateDetectionEnabled)
        {
            var existing = await _repository.FindDuplicateAsync(hash);
            if (existing != null)
            {
                await _repository.UpdateLastUsedAsync(existing.Id);
                existing.LastUsedAt = DateTime.UtcNow;
                return existing;
            }
        }

        var payload = new PayloadData
        {
            PlainText = plain,
            HtmlText = html,
            RtfText = rtf
        };

        byte[] payloadBytes = PayloadData.Serialize(payload);
        byte[] encrypted = _dataProtection.Encrypt(payloadBytes);

        string title = TruncateSingleLine(plain ?? "Formatted Rich Text", 80);
        string preview = TruncateMultiLine(plain ?? "Rich text snippet", 300);

        var item = new ClipItem
        {
            Id = Guid.NewGuid(),
            Type = ClipContentType.RichText,
            Title = title,
            PreviewText = preview,
            SecondaryInfo = html != null ? "HTML / Rich Text" : "RTF Formatted Text",
            SourceApplication = args.ProcessName,
            SourceWindowTitle = args.WindowTitle,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ByteSize = payloadBytes.Length,
            ContentHash = hash,
            EncryptedPayload = encrypted
        };

        await _repository.AddItemAsync(item);
        return item;
    }

    private async Task<ClipItem?> ProcessImageAsync(byte[] pngBytes, int width, int height, ClipboardChangedEventArgs args)
    {
        var settings = _settingsService.Settings;
        if (!settings.CaptureImages) return null;

        string hash = ComputeHash(pngBytes);

        if (settings.DuplicateDetectionEnabled)
        {
            var existing = await _repository.FindDuplicateAsync(hash);
            if (existing != null)
            {
                await _repository.UpdateLastUsedAsync(existing.Id);
                existing.LastUsedAt = DateTime.UtcNow;
                return existing;
            }
        }

        var payload = new PayloadData
        {
            ImageData = pngBytes,
            ImageWidth = width,
            ImageHeight = height
        };

        byte[] payloadBytes = PayloadData.Serialize(payload);
        byte[] encrypted = _dataProtection.Encrypt(payloadBytes);

        var itemId = Guid.NewGuid();
        // Save as protected asset file to avoid storing huge BLOBs inside SQLite directly
        string assetPath = await _storageService.SaveAssetAsync(itemId, encrypted, ".dat");

        var item = new ClipItem
        {
            Id = itemId,
            Type = ClipContentType.Image,
            Title = $"Image ({width}x{height})",
            PreviewText = $"PNG image - {width} × {height} pixels",
            SecondaryInfo = $"{width} × {height} • {pngBytes.Length / 1024} KB",
            SourceApplication = args.ProcessName,
            SourceWindowTitle = args.WindowTitle,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ByteSize = pngBytes.Length,
            ContentHash = hash,
            StorageAssetPath = assetPath,
            EncryptedPayload = null
        };

        await _repository.AddItemAsync(item);
        return item;
    }

    private async Task<ClipItem?> ProcessFilesAsync(string[] files, ClipboardChangedEventArgs args)
    {
        var settings = _settingsService.Settings;
        if (!settings.CaptureFiles || files.Length == 0) return null;

        string combined = string.Join("|", files.OrderBy(f => f));
        string hash = ComputeHash(combined);

        if (settings.DuplicateDetectionEnabled)
        {
            var existing = await _repository.FindDuplicateAsync(hash);
            if (existing != null)
            {
                await _repository.UpdateLastUsedAsync(existing.Id);
                existing.LastUsedAt = DateTime.UtcNow;
                return existing;
            }
        }

        var payload = new PayloadData
        {
            FilePaths = files
        };

        byte[] payloadBytes = PayloadData.Serialize(payload);
        byte[] encrypted = _dataProtection.Encrypt(payloadBytes);

        string title = files.Length == 1
            ? Path.GetFileName(files[0])
            : $"{Path.GetFileName(files[0])} + {files.Length - 1} more file{(files.Length > 2 ? "s" : "")}";

        string preview = string.Join(Environment.NewLine, files.Take(4));
        if (files.Length > 4) preview += $"{Environment.NewLine}... (+{files.Length - 4} more)";

        string secondary = $"{files.Length} file references";
        if (files.Length == 1 && File.Exists(files[0]))
        {
            try
            {
                var fi = new FileInfo(files[0]);
                string ext = fi.Extension.ToLowerInvariant();
                long size = fi.Length;
                string sizeStr = size > 1024 * 1024
                    ? $"{size / (1024.0 * 1024.0):F1} MB"
                    : $"{Math.Max(1, size / 1024)} KB";
                secondary = $"{sizeStr} • {ext}";
            }
            catch { }
        }

        var item = new ClipItem
        {
            Id = Guid.NewGuid(),
            Type = ClipContentType.Files,
            Title = title,
            PreviewText = preview,
            SecondaryInfo = secondary,
            SourceApplication = args.ProcessName,
            SourceWindowTitle = args.WindowTitle,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ByteSize = payloadBytes.Length,
            ContentHash = hash,
            EncryptedPayload = encrypted
        };

        await _repository.AddItemAsync(item);
        return item;
    }

    public async Task<ClipItem?> IngestTextAsync(string text, string sourceApp)
    {
        var args = new ClipboardChangedEventArgs
        {
            ProcessName = sourceApp,
            WindowTitle = "Phone Sync"
        };
        var item = await ProcessTextOrLinkAsync(text, args);
        if (item != null)
        {
            ItemCaptured?.Invoke(item);
            _ = RestoreToClipboardAsync(item);
        }
        return item;
    }

    public async Task<ClipItem?> IngestFilesAsync(string[] filePaths, string sourceApp)
    {
        var args = new ClipboardChangedEventArgs
        {
            ProcessName = sourceApp,
            WindowTitle = "Phone Sync"
        };
        var item = await ProcessFilesAsync(filePaths, args);
        if (item != null)
        {
            ItemCaptured?.Invoke(item);
            _ = RestoreToClipboardAsync(item);
        }
        return item;
    }

    public async Task<bool> RestoreToClipboardAsync(ClipItem item)
    {
        var payload = await GetPayloadAsync(item);
        if (payload == null) return false;

        return await RestorePayloadToClipboardAsync(payload, item.Type);
    }

    public Task<bool> RestorePayloadToClipboardAsync(PayloadData payload, ClipContentType type)
    {
        SetSelfCopySequence();

        var tcs = new TaskCompletionSource<bool>();

        var thread = new Thread(() =>
        {
            try
            {
                var dataObj = new System.Windows.DataObject();

                switch (type)
                {
                    case ClipContentType.Text:
                    case ClipContentType.Link:
                        if (!string.IsNullOrEmpty(payload.PlainText))
                        {
                            dataObj.SetText(payload.PlainText, TextDataFormat.UnicodeText);
                        }
                        break;

                    case ClipContentType.RichText:
                        if (!string.IsNullOrEmpty(payload.PlainText))
                        {
                            dataObj.SetText(payload.PlainText, TextDataFormat.UnicodeText);
                        }
                        if (!string.IsNullOrEmpty(payload.HtmlText))
                        {
                            dataObj.SetData(DataFormats.Html, payload.HtmlText);
                        }
                        if (!string.IsNullOrEmpty(payload.RtfText))
                        {
                            dataObj.SetData(DataFormats.Rtf, payload.RtfText);
                        }
                        break;

                    case ClipContentType.Image:
                        if (payload.ImageData != null && payload.ImageData.Length > 0)
                        {
                            var bmp = DecodeFromPng(payload.ImageData);
                            dataObj.SetImage(bmp);
                        }
                        break;

                    case ClipContentType.Files:
                        if (payload.FilePaths != null && payload.FilePaths.Length > 0)
                        {
                            var collection = new System.Collections.Specialized.StringCollection();
                            foreach (var f in payload.FilePaths)
                            {
                                if (File.Exists(f) || Directory.Exists(f))
                                {
                                    collection.Add(f);
                                }
                            }
                            if (collection.Count > 0)
                            {
                                dataObj.SetFileDropList(collection);
                            }
                        }
                        break;
                }

                System.Windows.Clipboard.SetDataObject(dataObj, true);
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore content to clipboard");
                tcs.SetResult(false);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return tcs.Task;
    }

    public async Task<PayloadData?> GetPayloadAsync(ClipItem item)
    {
        byte[]? enc = item.EncryptedPayload;
        if (enc == null && !string.IsNullOrEmpty(item.StorageAssetPath))
        {
            enc = await _storageService.LoadAssetAsync(item.StorageAssetPath);
        }

        if (enc == null || enc.Length == 0) return null;

        try
        {
            byte[] decrypted = _dataProtection.Decrypt(enc);
            return PayloadData.Deserialize(decrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt payload for item {Id}", item.Id);
            return null;
        }
    }

    public async Task<PayloadData?> GetPayloadFromQueueItemAsync(PasteQueueItem queueItem)
    {
        byte[]? enc = queueItem.EncryptedPayload;
        if (enc == null && !string.IsNullOrEmpty(queueItem.StorageAssetPath))
        {
            enc = await _storageService.LoadAssetAsync(queueItem.StorageAssetPath);
        }

        if (enc == null || enc.Length == 0) return null;

        try
        {
            byte[] decrypted = _dataProtection.Decrypt(enc);
            return PayloadData.Deserialize(decrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt payload for queue item {Id}", queueItem.Id);
            return null;
        }
    }

    private sealed record ClipboardSnapshot(
        string[]? Files,
        byte[]? ImageData,
        int ImageWidth,
        int ImageHeight,
        string? Html,
        string? Rtf,
        string? Text
    );

    private static async Task<ClipboardSnapshot?> ReadClipboardWithRetryAsync()
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                ClipboardSnapshot? snapshot = null;
                var t = new Thread(() =>
                {
                    try
                    {
                        var obj = System.Windows.Clipboard.GetDataObject();
                        if (obj == null) return;

                        string[]? files = null;
                        try
                        {
                            if (obj.GetDataPresent(DataFormats.FileDrop))
                            {
                                files = obj.GetData(DataFormats.FileDrop) as string[];
                            }
                        }
                        catch { }

                        byte[]? imgBytes = null;
                        int imgW = 0, imgH = 0;
                        try
                        {
                            if (obj.GetDataPresent(DataFormats.Bitmap))
                            {
                                if (obj.GetData(DataFormats.Bitmap) is BitmapSource bmp)
                                {
                                    imgW = bmp.PixelWidth;
                                    imgH = bmp.PixelHeight;
                                    imgBytes = EncodeToPng(bmp);
                                }
                            }
                        }
                        catch { }

                        string? html = null;
                        try { html = obj.GetDataPresent(DataFormats.Html) ? obj.GetData(DataFormats.Html) as string : null; } catch { }

                        string? rtf = null;
                        try { rtf = obj.GetDataPresent(DataFormats.Rtf) ? obj.GetData(DataFormats.Rtf) as string : null; } catch { }

                        string? text = null;
                        try { text = obj.GetDataPresent(DataFormats.UnicodeText) ? obj.GetData(DataFormats.UnicodeText) as string : null; } catch { }

                        if ((files != null && files.Length > 0) || imgBytes != null || html != null || rtf != null || !string.IsNullOrEmpty(text))
                        {
                            snapshot = new ClipboardSnapshot(files, imgBytes, imgW, imgH, html, rtf, text);
                        }
                    }
                    catch { }
                });
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                t.Join(400);

                if (snapshot != null) return snapshot;
            }
            catch { }

            await Task.Delay(25 * (i + 1));
        }

        return null;
    }

    private static bool IsValidUrl(string text, out string? domain)
    {
        domain = null;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
                domain = uri.Host;
                return true;
            }
        }
        return false;
    }

    private static string ComputeHash(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    private static string ComputeHash(byte[] bytes)
    {
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string TruncateSingleLine(string text, int max)
    {
        string single = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return single.Length <= max ? single : single[..max] + "...";
    }

    private static string TruncateMultiLine(string text, int max)
    {
        string trimmed = text.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "...";
    }

    private static byte[] EncodeToPng(BitmapSource bitmap)
    {
        using var ms = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static BitmapSource DecodeFromPng(byte[] pngBytes)
    {
        using var ms = new MemoryStream(pngBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}

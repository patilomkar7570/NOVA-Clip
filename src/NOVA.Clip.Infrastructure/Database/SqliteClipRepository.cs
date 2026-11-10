using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.Infrastructure.Database;

public class SqliteClipRepository : IClipRepository
{
    private readonly string _connectionString;
    private readonly IStorageService _storageService;
    private readonly IDataProtectionService _dataProtection;
    private readonly ILogger<SqliteClipRepository> _logger;
    private bool _initialized;

    public SqliteClipRepository(
        IStorageService storageService,
        IDataProtectionService dataProtection,
        ILogger<SqliteClipRepository> logger)
    {
        _storageService = storageService;
        _dataProtection = dataProtection;
        _logger = logger;
        _connectionString = $"Data Source={_storageService.DatabasePath};Mode=ReadWriteCreate;Cache=Shared;";
    }

    private SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS clip_items (
                id TEXT PRIMARY KEY,
                type INTEGER NOT NULL,
                title TEXT NOT NULL,
                preview_text TEXT,
                secondary_info TEXT,
                source_app TEXT,
                source_title TEXT,
                created_at TEXT NOT NULL,
                last_used_at TEXT NOT NULL,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                byte_size INTEGER NOT NULL DEFAULT 0,
                content_hash TEXT,
                encrypted_payload BLOB,
                storage_asset_path TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_clip_created ON clip_items(created_at DESC);
            CREATE INDEX IF NOT EXISTS idx_clip_last_used ON clip_items(last_used_at DESC);
            CREATE INDEX IF NOT EXISTS idx_clip_favorite ON clip_items(is_favorite);
            CREATE INDEX IF NOT EXISTS idx_clip_type ON clip_items(type);
            CREATE INDEX IF NOT EXISTS idx_clip_hash ON clip_items(content_hash);

            CREATE TABLE IF NOT EXISTS snippets (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                content_encrypted TEXT NOT NULL,
                description TEXT,
                tags TEXT,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS paste_queues (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                current_index INTEGER NOT NULL DEFAULT 0,
                separator TEXT NOT NULL DEFAULT '\n',
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS paste_queue_items (
                id TEXT PRIMARY KEY,
                queue_id TEXT NOT NULL,
                source_clip_id TEXT,
                order_index INTEGER NOT NULL,
                type INTEGER NOT NULL,
                title TEXT NOT NULL,
                preview_text TEXT NOT NULL,
                encrypted_payload BLOB,
                storage_asset_path TEXT,
                is_completed INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (queue_id) REFERENCES paste_queues(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_queue_items_order ON paste_queue_items(queue_id, order_index ASC);
        ";

        await cmd.ExecuteNonQueryAsync();
        _initialized = true;
        _logger.LogInformation("Database initialized successfully at {Path}", _storageService.DatabasePath);
    }

    public async Task<IReadOnlyList<ClipItem>> GetAllItemsAsync(int limit = 200, int offset = 0)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT id, type, title, preview_text, secondary_info, source_app, source_title,
                   created_at, last_used_at, is_favorite, byte_size, content_hash,
                   encrypted_payload, storage_asset_path
            FROM clip_items
            ORDER BY last_used_at DESC
            LIMIT $limit OFFSET $offset;";

        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.Parameters.AddWithValue("$offset", offset);

        var list = new List<ClipItem>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapClipItem(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<ClipItem>> GetItemsByTypeAsync(ClipContentType type, int limit = 200, int offset = 0)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT id, type, title, preview_text, secondary_info, source_app, source_title,
                   created_at, last_used_at, is_favorite, byte_size, content_hash,
                   encrypted_payload, storage_asset_path
            FROM clip_items
            WHERE type = $type
            ORDER BY last_used_at DESC
            LIMIT $limit OFFSET $offset;";

        cmd.Parameters.AddWithValue("$type", (int)type);
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.Parameters.AddWithValue("$offset", offset);

        var list = new List<ClipItem>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapClipItem(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<ClipItem>> GetFavoritesAsync(int limit = 200)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT id, type, title, preview_text, secondary_info, source_app, source_title,
                   created_at, last_used_at, is_favorite, byte_size, content_hash,
                   encrypted_payload, storage_asset_path
            FROM clip_items
            WHERE is_favorite = 1
            ORDER BY last_used_at DESC
            LIMIT $limit;";

        cmd.Parameters.AddWithValue("$limit", limit);

        var list = new List<ClipItem>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapClipItem(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<ClipItem>> SearchItemsAsync(string query, ClipContentType? type = null, int limit = 100)
    {
        await InitializeAsync();
        if (string.IsNullOrWhiteSpace(query))
        {
            return type.HasValue
                ? await GetItemsByTypeAsync(type.Value, limit)
                : await GetAllItemsAsync(limit);
        }

        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        string sql = @"
            SELECT id, type, title, preview_text, secondary_info, source_app, source_title,
                   created_at, last_used_at, is_favorite, byte_size, content_hash,
                   encrypted_payload, storage_asset_path
            FROM clip_items
            WHERE (title LIKE $query OR preview_text LIKE $query OR source_app LIKE $query)";

        if (type.HasValue)
        {
            sql += " AND type = $type";
            cmd.Parameters.AddWithValue("$type", (int)type.Value);
        }

        sql += " ORDER BY last_used_at DESC LIMIT $limit;";
        cmd.CommandText = sql;

        cmd.Parameters.AddWithValue("$query", $"%{query}%");
        cmd.Parameters.AddWithValue("$limit", limit);

        var list = new List<ClipItem>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapClipItem(reader));
        }

        return list;
    }

    public async Task<ClipItem?> GetItemByIdAsync(Guid id)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT id, type, title, preview_text, secondary_info, source_app, source_title,
                   created_at, last_used_at, is_favorite, byte_size, content_hash,
                   encrypted_payload, storage_asset_path
            FROM clip_items
            WHERE id = $id LIMIT 1;";

        cmd.Parameters.AddWithValue("$id", id.ToString());

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapClipItem(reader);
        }

        return null;
    }

    public async Task<ClipItem?> FindDuplicateAsync(string contentHash)
    {
        await InitializeAsync();
        if (string.IsNullOrWhiteSpace(contentHash)) return null;

        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT id, type, title, preview_text, secondary_info, source_app, source_title,
                   created_at, last_used_at, is_favorite, byte_size, content_hash,
                   encrypted_payload, storage_asset_path
            FROM clip_items
            WHERE content_hash = $hash LIMIT 1;";

        cmd.Parameters.AddWithValue("$hash", contentHash);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapClipItem(reader);
        }

        return null;
    }

    public async Task<bool> AddItemAsync(ClipItem item)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO clip_items (
                id, type, title, preview_text, secondary_info, source_app, source_title,
                created_at, last_used_at, is_favorite, byte_size, content_hash,
                encrypted_payload, storage_asset_path
            ) VALUES (
                $id, $type, $title, $preview_text, $secondary_info, $source_app, $source_title,
                $created_at, $last_used_at, $is_favorite, $byte_size, $content_hash,
                $encrypted_payload, $storage_asset_path
            );";

        BindClipItemParams(cmd, item);

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> UpdateItemAsync(ClipItem item)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            UPDATE clip_items SET
                title = $title,
                preview_text = $preview_text,
                secondary_info = $secondary_info,
                is_favorite = $is_favorite,
                last_used_at = $last_used_at
            WHERE id = $id;";

        cmd.Parameters.AddWithValue("$id", item.Id.ToString());
        cmd.Parameters.AddWithValue("$title", item.Title);
        cmd.Parameters.AddWithValue("$preview_text", (object?)item.PreviewText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$secondary_info", (object?)item.SecondaryInfo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$is_favorite", item.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$last_used_at", item.LastUsedAt.ToString("O"));

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> UpdateLastUsedAsync(Guid id)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            UPDATE clip_items
            SET last_used_at = $now
            WHERE id = $id;";

        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> ToggleFavoriteAsync(Guid id)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            UPDATE clip_items
            SET is_favorite = CASE WHEN is_favorite = 1 THEN 0 ELSE 1 END
            WHERE id = $id;";

        cmd.Parameters.AddWithValue("$id", id.ToString());

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> DeleteItemAsync(Guid id)
    {
        await InitializeAsync();

        var item = await GetItemByIdAsync(id);
        if (item != null && !string.IsNullOrEmpty(item.StorageAssetPath))
        {
            await _storageService.DeleteAssetAsync(item.StorageAssetPath);
        }

        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM clip_items WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString());

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<int> DeleteItemsAsync(IEnumerable<Guid> ids)
    {
        int deleted = 0;
        foreach (var id in ids)
        {
            if (await DeleteItemAsync(id))
            {
                deleted++;
            }
        }
        return deleted;
    }

    public async Task<int> ClearAllHistoryAsync()
    {
        await InitializeAsync();
        using var conn = CreateConnection();

        // Find all assets to delete
        using var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT storage_asset_path FROM clip_items WHERE storage_asset_path IS NOT NULL;";
        using var reader = await selectCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                string asset = reader.GetString(0);
                await _storageService.DeleteAssetAsync(asset);
            }
        }
        reader.Close();

        using var delCmd = conn.CreateCommand();
        delCmd.CommandText = "DELETE FROM clip_items WHERE is_favorite = 0;";
        int rows = await delCmd.ExecuteNonQueryAsync();
        return rows;
    }

    public async Task<int> ClearImagesAsync()
    {
        await InitializeAsync();
        using var conn = CreateConnection();

        using var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT storage_asset_path FROM clip_items WHERE type = $imgType AND storage_asset_path IS NOT NULL;";
        selectCmd.Parameters.AddWithValue("$imgType", (int)ClipContentType.Image);
        using var reader = await selectCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                string asset = reader.GetString(0);
                await _storageService.DeleteAssetAsync(asset);
            }
        }
        reader.Close();

        using var delCmd = conn.CreateCommand();
        delCmd.CommandText = "DELETE FROM clip_items WHERE type = $imgType AND is_favorite = 0;";
        delCmd.Parameters.AddWithValue("$imgType", (int)ClipContentType.Image);
        int rows = await delCmd.ExecuteNonQueryAsync();
        return rows;
    }

    public async Task<int> CleanupRetentionAsync(int retentionDays, int maxItems, long maxStorageBytes)
    {
        await InitializeAsync();
        int totalDeleted = 0;

        using var conn = CreateConnection();

        // 1. Expiration by retention days (never delete favorites)
        if (retentionDays > 0)
        {
            DateTime cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            using var cmdDays = conn.CreateCommand();
            cmdDays.CommandText = @"
                SELECT id, storage_asset_path FROM clip_items
                WHERE is_favorite = 0 AND created_at < $cutoff;";
            cmdDays.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));

            var idsToDelete = new List<Guid>();
            using (var reader = await cmdDays.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    idsToDelete.Add(Guid.Parse(reader.GetString(0)));
                    if (!reader.IsDBNull(1))
                    {
                        await _storageService.DeleteAssetAsync(reader.GetString(1));
                    }
                }
            }

            foreach (var id in idsToDelete)
            {
                using var del = conn.CreateCommand();
                del.CommandText = "DELETE FROM clip_items WHERE id = $id;";
                del.Parameters.AddWithValue("$id", id.ToString());
                totalDeleted += await del.ExecuteNonQueryAsync();
            }
        }

        // 2. Expiration by maxItems
        if (maxItems > 0)
        {
            using var cmdCount = conn.CreateCommand();
            cmdCount.CommandText = @"
                SELECT id, storage_asset_path FROM clip_items
                WHERE is_favorite = 0
                ORDER BY last_used_at DESC
                LIMIT -1 OFFSET $maxItems;";
            cmdCount.Parameters.AddWithValue("$maxItems", maxItems);

            var excessIds = new List<Guid>();
            using (var reader = await cmdCount.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    excessIds.Add(Guid.Parse(reader.GetString(0)));
                    if (!reader.IsDBNull(1))
                    {
                        await _storageService.DeleteAssetAsync(reader.GetString(1));
                    }
                }
            }

            foreach (var id in excessIds)
            {
                using var del = conn.CreateCommand();
                del.CommandText = "DELETE FROM clip_items WHERE id = $id;";
                del.Parameters.AddWithValue("$id", id.ToString());
                totalDeleted += await del.ExecuteNonQueryAsync();
            }
        }

        // 3. Expiration by maxStorageBytes
        if (maxStorageBytes > 0)
        {
            var usage = await GetStorageUsageAsync();
            if (usage.TotalSizeBytes > maxStorageBytes)
            {
                using var cmdStorage = conn.CreateCommand();
                cmdStorage.CommandText = @"
                    SELECT id, storage_asset_path, byte_size FROM clip_items
                    WHERE is_favorite = 0
                    ORDER BY last_used_at ASC;";

                using var reader = await cmdStorage.ExecuteReaderAsync();
                long currentTotal = usage.TotalSizeBytes;
                var storageExcessIds = new List<(Guid Id, string? AssetPath)>();

                while (await reader.ReadAsync() && currentTotal > maxStorageBytes)
                {
                    var id = Guid.Parse(reader.GetString(0));
                    string? asset = reader.IsDBNull(1) ? null : reader.GetString(1);
                    long itemBytes = reader.GetInt64(2);

                    storageExcessIds.Add((id, asset));
                    currentTotal -= itemBytes;
                }
                reader.Close();

                foreach (var (id, asset) in storageExcessIds)
                {
                    if (asset != null) await _storageService.DeleteAssetAsync(asset);
                    using var del = conn.CreateCommand();
                    del.CommandText = "DELETE FROM clip_items WHERE id = $id;";
                    del.Parameters.AddWithValue("$id", id.ToString());
                    totalDeleted += await del.ExecuteNonQueryAsync();
                }
            }
        }

        return totalDeleted;
    }

    // Snippets
    public async Task<IReadOnlyList<SnippetItem>> GetAllSnippetsAsync()
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT id, title, content_encrypted, description, tags, is_favorite, created_at, updated_at
            FROM snippets
            ORDER BY title ASC;";

        var list = new List<SnippetItem>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string enc = reader.GetString(2);
            string content = string.Empty;
            try { content = _dataProtection.DecryptString(enc); } catch { }

            list.Add(new SnippetItem
            {
                Id = Guid.Parse(reader.GetString(0)),
                Title = reader.GetString(1),
                Content = content,
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                Tags = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsFavorite = reader.GetInt32(5) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                UpdatedAt = DateTime.Parse(reader.GetString(7))
            });
        }

        return list;
    }

    public async Task<SnippetItem?> GetSnippetByIdAsync(Guid id)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT id, title, content_encrypted, description, tags, is_favorite, created_at, updated_at
            FROM snippets
            WHERE id = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", id.ToString());

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            string enc = reader.GetString(2);
            string content = string.Empty;
            try { content = _dataProtection.DecryptString(enc); } catch { }

            return new SnippetItem
            {
                Id = Guid.Parse(reader.GetString(0)),
                Title = reader.GetString(1),
                Content = content,
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                Tags = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsFavorite = reader.GetInt32(5) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                UpdatedAt = DateTime.Parse(reader.GetString(7))
            };
        }

        return null;
    }

    public async Task<bool> AddSnippetAsync(SnippetItem snippet)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        string enc = _dataProtection.EncryptString(snippet.Content);

        cmd.CommandText = @"
            INSERT INTO snippets (id, title, content_encrypted, description, tags, is_favorite, created_at, updated_at)
            VALUES ($id, $title, $content, $description, $tags, $is_favorite, $created_at, $updated_at);";

        cmd.Parameters.AddWithValue("$id", snippet.Id.ToString());
        cmd.Parameters.AddWithValue("$title", snippet.Title);
        cmd.Parameters.AddWithValue("$content", enc);
        cmd.Parameters.AddWithValue("$description", (object?)snippet.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tags", (object?)snippet.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$is_favorite", snippet.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$created_at", snippet.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$updated_at", snippet.UpdatedAt.ToString("O"));

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> UpdateSnippetAsync(SnippetItem snippet)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        string enc = _dataProtection.EncryptString(snippet.Content);

        cmd.CommandText = @"
            UPDATE snippets SET
                title = $title,
                content_encrypted = $content,
                description = $description,
                tags = $tags,
                is_favorite = $is_favorite,
                updated_at = $updated_at
            WHERE id = $id;";

        cmd.Parameters.AddWithValue("$id", snippet.Id.ToString());
        cmd.Parameters.AddWithValue("$title", snippet.Title);
        cmd.Parameters.AddWithValue("$content", enc);
        cmd.Parameters.AddWithValue("$description", (object?)snippet.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tags", (object?)snippet.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$is_favorite", snippet.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("O"));

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> DeleteSnippetAsync(Guid id)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM snippets WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString());

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    // Queues
    public async Task<IReadOnlyList<PasteQueue>> GetAllQueuesAsync()
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT id, name, current_index, separator, created_at, updated_at FROM paste_queues ORDER BY updated_at DESC;";

        var queues = new List<PasteQueue>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                queues.Add(new PasteQueue
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    CurrentIndex = reader.GetInt32(2),
                    Separator = reader.GetString(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    UpdatedAt = DateTime.Parse(reader.GetString(5)),
                    Items = []
                });
            }
        }

        // Load items for each queue
        foreach (var q in queues)
        {
            q.Items = (await GetQueueItemsAsync(conn, q.Id)).ToList();
        }

        return queues;
    }

    private async Task<List<PasteQueueItem>> GetQueueItemsAsync(SqliteConnection conn, Guid queueId)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, queue_id, source_clip_id, order_index, type, title, preview_text,
                   encrypted_payload, storage_asset_path, is_completed
            FROM paste_queue_items
            WHERE queue_id = $queueId
            ORDER BY order_index ASC;";
        cmd.Parameters.AddWithValue("$queueId", queueId.ToString());

        var items = new List<PasteQueueItem>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new PasteQueueItem
            {
                Id = Guid.Parse(reader.GetString(0)),
                QueueId = Guid.Parse(reader.GetString(1)),
                SourceClipItemId = reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
                OrderIndex = reader.GetInt32(3),
                Type = (ClipContentType)reader.GetInt32(4),
                Title = reader.GetString(5),
                PreviewText = reader.GetString(6),
                EncryptedPayload = reader.IsDBNull(7) ? null : (byte[])reader.GetValue(7),
                StorageAssetPath = reader.IsDBNull(8) ? null : reader.GetString(8),
                IsCompleted = reader.GetInt32(9) == 1
            });
        }

        return items;
    }

    public async Task<PasteQueue?> GetQueueByIdAsync(Guid id)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT id, name, current_index, separator, created_at, updated_at FROM paste_queues WHERE id = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", id.ToString());

        PasteQueue? queue = null;
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                queue = new PasteQueue
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    CurrentIndex = reader.GetInt32(2),
                    Separator = reader.GetString(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    UpdatedAt = DateTime.Parse(reader.GetString(5)),
                    Items = []
                };
            }
        }

        if (queue != null)
        {
            queue.Items = (await GetQueueItemsAsync(conn, queue.Id)).ToList();
        }

        return queue;
    }

    public async Task<bool> SaveQueueAsync(PasteQueue queue)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var tx = conn.BeginTransaction();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO paste_queues (id, name, current_index, separator, created_at, updated_at)
                VALUES ($id, $name, $current_index, $separator, $created_at, $updated_at)
                ON CONFLICT(id) DO UPDATE SET
                    name = $name,
                    current_index = $current_index,
                    separator = $separator,
                    updated_at = $updated_at;";

            cmd.Parameters.AddWithValue("$id", queue.Id.ToString());
            cmd.Parameters.AddWithValue("$name", queue.Name);
            cmd.Parameters.AddWithValue("$current_index", queue.CurrentIndex);
            cmd.Parameters.AddWithValue("$separator", queue.Separator);
            cmd.Parameters.AddWithValue("$created_at", queue.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("O"));

            await cmd.ExecuteNonQueryAsync();

            // Clear previous items and reinsert
            using var delCmd = conn.CreateCommand();
            delCmd.Transaction = tx;
            delCmd.CommandText = "DELETE FROM paste_queue_items WHERE queue_id = $qid;";
            delCmd.Parameters.AddWithValue("$qid", queue.Id.ToString());
            await delCmd.ExecuteNonQueryAsync();

            for (int i = 0; i < queue.Items.Count; i++)
            {
                var item = queue.Items[i];
                item.OrderIndex = i;

                using var itemCmd = conn.CreateCommand();
                itemCmd.Transaction = tx;
                itemCmd.CommandText = @"
                    INSERT INTO paste_queue_items (
                        id, queue_id, source_clip_id, order_index, type, title,
                        preview_text, encrypted_payload, storage_asset_path, is_completed
                    ) VALUES (
                        $id, $queue_id, $source_clip_id, $order_index, $type, $title,
                        $preview_text, $encrypted_payload, $storage_asset_path, $is_completed
                    );";

                itemCmd.Parameters.AddWithValue("$id", item.Id.ToString());
                itemCmd.Parameters.AddWithValue("$queue_id", queue.Id.ToString());
                itemCmd.Parameters.AddWithValue("$source_clip_id", (object?)item.SourceClipItemId?.ToString() ?? DBNull.Value);
                itemCmd.Parameters.AddWithValue("$order_index", item.OrderIndex);
                itemCmd.Parameters.AddWithValue("$type", (int)item.Type);
                itemCmd.Parameters.AddWithValue("$title", item.Title);
                itemCmd.Parameters.AddWithValue("$preview_text", item.PreviewText);
                itemCmd.Parameters.AddWithValue("$encrypted_payload", (object?)item.EncryptedPayload ?? DBNull.Value);
                itemCmd.Parameters.AddWithValue("$storage_asset_path", (object?)item.StorageAssetPath ?? DBNull.Value);
                itemCmd.Parameters.AddWithValue("$is_completed", item.IsCompleted ? 1 : 0);

                await itemCmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
            return true;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "Failed to save queue {Name}", queue.Name);
            return false;
        }
    }

    public async Task<bool> DeleteQueueAsync(Guid id)
    {
        await InitializeAsync();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM paste_queues WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.ToString());

        int rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    // Storage Usage
    public async Task<StorageUsageInfo> GetStorageUsageAsync()
    {
        await InitializeAsync();
        var usage = new StorageUsageInfo();

        try
        {
            if (File.Exists(_storageService.DatabasePath))
            {
                usage.DatabaseSizeBytes = new FileInfo(_storageService.DatabasePath).Length;
            }

            usage.StorageAssetsSizeBytes = _storageService.GetAssetsDirectorySizeBytes();

            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT COUNT(*) FROM clip_items;
                SELECT type, COUNT(*) FROM clip_items GROUP BY type;
                SELECT COUNT(*) FROM snippets;
                SELECT COUNT(*) FROM paste_queues;
            ";

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                usage.TotalItemsCount = reader.GetInt32(0);
            }

            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    int type = reader.GetInt32(0);
                    int count = reader.GetInt32(1);
                    switch ((ClipContentType)type)
                    {
                        case ClipContentType.Text: usage.TextItemsCount = count; break;
                        case ClipContentType.Link: usage.LinkItemsCount = count; break;
                        case ClipContentType.RichText: usage.RichTextItemsCount = count; break;
                        case ClipContentType.Image: usage.ImageItemsCount = count; break;
                        case ClipContentType.Files: usage.FileItemsCount = count; break;
                    }
                }
            }

            if (await reader.NextResultAsync() && await reader.ReadAsync())
            {
                usage.SnippetsCount = reader.GetInt32(0);
            }

            if (await reader.NextResultAsync() && await reader.ReadAsync())
            {
                usage.QueuesCount = reader.GetInt32(0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compute storage usage info");
        }

        return usage;
    }

    private static ClipItem MapClipItem(SqliteDataReader reader)
    {
        return new ClipItem
        {
            Id = Guid.Parse(reader.GetString(0)),
            Type = (ClipContentType)reader.GetInt32(1),
            Title = reader.GetString(2),
            PreviewText = reader.IsDBNull(3) ? null : reader.GetString(3),
            SecondaryInfo = reader.IsDBNull(4) ? null : reader.GetString(4),
            SourceApplication = reader.IsDBNull(5) ? null : reader.GetString(5),
            SourceWindowTitle = reader.IsDBNull(6) ? null : reader.GetString(6),
            CreatedAt = DateTime.Parse(reader.GetString(7)),
            LastUsedAt = DateTime.Parse(reader.GetString(8)),
            IsFavorite = reader.GetInt32(9) == 1,
            ByteSize = reader.GetInt64(10),
            ContentHash = reader.IsDBNull(11) ? null : reader.GetString(11),
            EncryptedPayload = reader.IsDBNull(12) ? null : (byte[])reader.GetValue(12),
            StorageAssetPath = reader.IsDBNull(13) ? null : reader.GetString(13)
        };
    }

    private static void BindClipItemParams(SqliteCommand cmd, ClipItem item)
    {
        cmd.Parameters.AddWithValue("$id", item.Id.ToString());
        cmd.Parameters.AddWithValue("$type", (int)item.Type);
        cmd.Parameters.AddWithValue("$title", item.Title);
        cmd.Parameters.AddWithValue("$preview_text", (object?)item.PreviewText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$secondary_info", (object?)item.SecondaryInfo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$source_app", (object?)item.SourceApplication ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$source_title", (object?)item.SourceWindowTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$created_at", item.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$last_used_at", item.LastUsedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$is_favorite", item.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$byte_size", item.ByteSize);
        cmd.Parameters.AddWithValue("$content_hash", (object?)item.ContentHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$encrypted_payload", (object?)item.EncryptedPayload ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$storage_asset_path", (object?)item.StorageAssetPath ?? DBNull.Value);
    }
}

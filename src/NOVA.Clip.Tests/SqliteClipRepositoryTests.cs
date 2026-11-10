using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NOVA.Clip.Core.Models;
using NOVA.Clip.Infrastructure.Database;
using NOVA.Clip.Infrastructure.Security;
using NOVA.Clip.Infrastructure.Storage;
using Xunit;

namespace NOVA.Clip.Tests;

public class SqliteClipRepositoryTests : IDisposable
{
    private readonly string _testDir;
    private readonly StorageService _storageService;
    private readonly DpapiDataProtectionService _dataProtection;
    private readonly SqliteClipRepository _repository;

    public SqliteClipRepositoryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "NovaClipTests_" + Guid.NewGuid().ToString("N"));
        _storageService = new StorageService(NullLogger<StorageService>.Instance, _testDir);
        _dataProtection = new DpapiDataProtectionService(NullLogger<DpapiDataProtectionService>.Instance);
        _repository = new SqliteClipRepository(
            _storageService,
            _dataProtection,
            NullLogger<SqliteClipRepository>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task AddItemAndGetById_Succeeds()
    {
        var item = new ClipItem
        {
            Id = Guid.NewGuid(),
            Type = ClipContentType.Text,
            Title = "Test Item",
            PreviewText = "Test preview content",
            SourceApplication = "notepad.exe",
            SourceWindowTitle = "Untitled - Notepad",
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ContentHash = "HASH123",
            ByteSize = 100
        };

        bool added = await _repository.AddItemAsync(item);
        added.Should().BeTrue();

        var retrieved = await _repository.GetItemByIdAsync(item.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Test Item");
        retrieved.PreviewText.Should().Be("Test preview content");
        retrieved.SourceApplication.Should().Be("notepad.exe");
        retrieved.Type.Should().Be(ClipContentType.Text);
    }

    [Fact]
    public async Task ToggleFavorite_UpdatesFavoriteState()
    {
        var item = new ClipItem
        {
            Id = Guid.NewGuid(),
            Title = "Favorite Test",
            IsFavorite = false
        };
        await _repository.AddItemAsync(item);

        await _repository.ToggleFavoriteAsync(item.Id);
        var favorited = await _repository.GetItemByIdAsync(item.Id);
        favorited!.IsFavorite.Should().BeTrue();

        await _repository.ToggleFavoriteAsync(item.Id);
        var unfavorited = await _repository.GetItemByIdAsync(item.Id);
        unfavorited!.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public async Task SearchItems_FindsMatchingEntries()
    {
        var item1 = new ClipItem { Id = Guid.NewGuid(), Title = "Alpha Document", PreviewText = "Some contents" };
        var item2 = new ClipItem { Id = Guid.NewGuid(), Title = "Beta Spreadsheet", PreviewText = "Financial details" };
        var item3 = new ClipItem { Id = Guid.NewGuid(), Title = "Gamma Notes", PreviewText = "Alpha notes summary" };

        await _repository.AddItemAsync(item1);
        await _repository.AddItemAsync(item2);
        await _repository.AddItemAsync(item3);

        var results = await _repository.SearchItemsAsync("Alpha");
        results.Should().HaveCount(2);
        results.Select(r => r.Id).Should().Contain([item1.Id, item3.Id]);
    }

    [Fact]
    public async Task FindDuplicate_ReturnsExistingItemByHash()
    {
        var item = new ClipItem
        {
            Id = Guid.NewGuid(),
            Title = "Unique Hash Test",
            ContentHash = "HASH_ABC_123"
        };
        await _repository.AddItemAsync(item);

        var found = await _repository.FindDuplicateAsync("HASH_ABC_123");
        found.Should().NotBeNull();
        found!.Id.Should().Be(item.Id);

        var notFound = await _repository.FindDuplicateAsync("NON_EXISTENT_HASH");
        notFound.Should().BeNull();
    }

    [Fact]
    public async Task RetentionCleanup_PreservesFavoritesAndCleansOldNonFavorites()
    {
        var oldItem = new ClipItem
        {
            Id = Guid.NewGuid(),
            Title = "Old Regular Item",
            CreatedAt = DateTime.UtcNow.AddDays(-40),
            IsFavorite = false
        };

        var oldFavorite = new ClipItem
        {
            Id = Guid.NewGuid(),
            Title = "Old Favorite Item",
            CreatedAt = DateTime.UtcNow.AddDays(-40),
            IsFavorite = true
        };

        var newItem = new ClipItem
        {
            Id = Guid.NewGuid(),
            Title = "New Item",
            CreatedAt = DateTime.UtcNow,
            IsFavorite = false
        };

        await _repository.AddItemAsync(oldItem);
        await _repository.AddItemAsync(oldFavorite);
        await _repository.AddItemAsync(newItem);

        int deleted = await _repository.CleanupRetentionAsync(retentionDays: 30, maxItems: 100, maxStorageBytes: 100_000_000);
        deleted.Should().Be(1);

        (await _repository.GetItemByIdAsync(oldItem.Id)).Should().BeNull();
        (await _repository.GetItemByIdAsync(oldFavorite.Id)).Should().NotBeNull();
        (await _repository.GetItemByIdAsync(newItem.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Snippets_CrudOperations_WorkWithEncryption()
    {
        var snippet = new SnippetItem
        {
            Id = Guid.NewGuid(),
            Title = "Sql Connection String",
            Content = "Server=myServerAddress;Database=myDataBase;Uid=myUsername;Pwd=myPassword;",
            Description = "Production DB template",
            Tags = "sql,db,prod",
            IsFavorite = true
        };

        await _repository.AddSnippetAsync(snippet);

        var loaded = await _repository.GetSnippetByIdAsync(snippet.Id);
        loaded.Should().NotBeNull();
        loaded!.Title.Should().Be("Sql Connection String");
        loaded.Content.Should().Be(snippet.Content); // Decrypted seamlessly
        loaded.Tags.Should().Be("sql,db,prod");

        loaded.Title = "Updated DB String";
        await _repository.UpdateSnippetAsync(loaded);

        var updated = await _repository.GetSnippetByIdAsync(snippet.Id);
        updated!.Title.Should().Be("Updated DB String");

        await _repository.DeleteSnippetAsync(snippet.Id);
        (await _repository.GetSnippetByIdAsync(snippet.Id)).Should().BeNull();
    }

    [Fact]
    public async Task PasteQueue_SaveAndLoad_PreservesItemsAndOrder()
    {
        var queue = new PasteQueue
        {
            Id = Guid.NewGuid(),
            Name = "Email Follow-up Queue",
            CurrentIndex = 0,
            Separator = "\n\n"
        };

        queue.Items.Add(new PasteQueueItem
        {
            Id = Guid.NewGuid(),
            QueueId = queue.Id,
            OrderIndex = 0,
            Type = ClipContentType.Text,
            Title = "Greeting",
            PreviewText = "Hi team,"
        });

        queue.Items.Add(new PasteQueueItem
        {
            Id = Guid.NewGuid(),
            QueueId = queue.Id,
            OrderIndex = 1,
            Type = ClipContentType.Link,
            Title = "Documentation link",
            PreviewText = "https://example.com/docs"
        });

        bool saved = await _repository.SaveQueueAsync(queue);
        saved.Should().BeTrue();

        var loaded = await _repository.GetQueueByIdAsync(queue.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Email Follow-up Queue");
        loaded.Items.Should().HaveCount(2);
        loaded.Items[0].Title.Should().Be("Greeting");
        loaded.Items[1].Title.Should().Be("Documentation link");
        loaded.Separator.Should().Be("\n\n");
    }
}

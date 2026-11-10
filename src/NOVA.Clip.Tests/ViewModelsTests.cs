using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NOVA.Clip.App.ViewModels;
using NOVA.Clip.Core.Configuration;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;
using NOVA.Clip.Infrastructure.Configuration;
using NOVA.Clip.Infrastructure.Database;
using NOVA.Clip.Infrastructure.Security;
using NOVA.Clip.Infrastructure.Storage;
using Xunit;

namespace NOVA.Clip.Tests;

public class ViewModelsTests : IDisposable
{
    private readonly string _testDir;
    private readonly StorageService _storageService;
    private readonly DpapiDataProtectionService _dataProtection;
    private readonly SqliteClipRepository _repository;
    private readonly SettingsService _settingsService;
    private readonly Mock<IClipboardService> _clipboardServiceMock;
    private readonly Mock<IInputSimulator> _inputSimulatorMock;
    private readonly Mock<IThemeService> _themeServiceMock;

    public ViewModelsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "NovaClipVmTests_" + Guid.NewGuid().ToString("N"));
        _storageService = new StorageService(NullLogger<StorageService>.Instance, _testDir);
        _dataProtection = new DpapiDataProtectionService(NullLogger<DpapiDataProtectionService>.Instance);
        _repository = new SqliteClipRepository(
            _storageService,
            _dataProtection,
            NullLogger<SqliteClipRepository>.Instance);
        _settingsService = new SettingsService(_storageService, NullLogger<SettingsService>.Instance);

        _clipboardServiceMock = new Mock<IClipboardService>();
        _inputSimulatorMock = new Mock<IInputSimulator>();
        _themeServiceMock = new Mock<IThemeService>();
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
    public async Task ItemCardViewModel_Copy_RestoresClipboardAndUpdatesTimestamp()
    {
        var item = new ClipItem
        {
            Id = Guid.NewGuid(),
            Title = "Copy test item",
            LastUsedAt = DateTime.UtcNow.AddHours(-2)
        };
        await _repository.AddItemAsync(item);

        _clipboardServiceMock.Setup(c => c.RestoreToClipboardAsync(It.IsAny<ClipItem>()))
                             .ReturnsAsync(true);

        var vm = new ItemCardViewModel(item, _clipboardServiceMock.Object, _repository);
        await vm.CopyAsync();

        _clipboardServiceMock.Verify(c => c.RestoreToClipboardAsync(item), Times.Once);
        vm.FriendlyTime.Should().Be("Just now");
    }

    [Fact]
    public async Task ItemCardViewModel_Rename_UpdatesTitleInRepository()
    {
        var item = new ClipItem
        {
            Id = Guid.NewGuid(),
            Title = "Original Name"
        };
        await _repository.AddItemAsync(item);

        var vm = new ItemCardViewModel(item, _clipboardServiceMock.Object, _repository);
        bool renamed = await vm.RenameAsync("Updated Name");

        renamed.Should().BeTrue();
        vm.Title.Should().Be("Updated Name");

        var fromDb = await _repository.GetItemByIdAsync(item.Id);
        fromDb!.Title.Should().Be("Updated Name");
    }

    [Fact]
    public async Task OverviewViewModel_Refresh_LoadsCountsAndRecentItems()
    {
        var item1 = new ClipItem { Id = Guid.NewGuid(), Title = "Item 1", Type = ClipContentType.Text };
        var item2 = new ClipItem { Id = Guid.NewGuid(), Title = "Item 2", Type = ClipContentType.Link, IsFavorite = true };
        await _repository.AddItemAsync(item1);
        await _repository.AddItemAsync(item2);

        var vm = new OverviewViewModel(
            _repository,
            _clipboardServiceMock.Object,
            _settingsService,
            openFloatingPanel: () => { });

        await vm.RefreshAsync();

        vm.TotalItemsCount.Should().Be(2);
        vm.TextCount.Should().Be(1);
        vm.LinkCount.Should().Be(1);
        vm.RecentItems.Should().HaveCount(2);
        vm.FavoriteItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task ClipboardListViewModel_FiltersByType()
    {
        var textItem = new ClipItem { Id = Guid.NewGuid(), Title = "Text Item", Type = ClipContentType.Text };
        var imageItem = new ClipItem { Id = Guid.NewGuid(), Title = "Image Item", Type = ClipContentType.Image };
        await _repository.AddItemAsync(textItem);
        await _repository.AddItemAsync(imageItem);

        var vm = new ClipboardListViewModel(_repository, _clipboardServiceMock.Object);

        await vm.InitializeAsync("Text Only", ClipContentType.Text);
        vm.Items.Should().HaveCount(1);
        vm.Items[0].Type.Should().Be(ClipContentType.Text);

        await vm.InitializeAsync("Images Only", ClipContentType.Image);
        vm.Items.Should().HaveCount(1);
        vm.Items[0].Type.Should().Be(ClipContentType.Image);
    }

    [Fact]
    public async Task SnippetsViewModel_CreateAndEditWorkflow()
    {
        var vm = new SnippetsViewModel(_repository, _clipboardServiceMock.Object);

        vm.StartNewSnippet();
        vm.EditorTitle = "My Snippet";
        vm.EditorContent = "console.log('hello');";
        vm.EditorTags = "javascript,js";

        await vm.SaveSnippetAsync();

        vm.Snippets.Should().HaveCount(1);
        vm.Snippets[0].Title.Should().Be("My Snippet");
        vm.Snippets[0].Content.Should().Be("console.log('hello');");

        // Edit
        vm.EditSnippet(vm.Snippets[0]);
        vm.EditorContent = "console.log('updated');";
        await vm.SaveSnippetAsync();

        vm.Snippets[0].Content.Should().Be("console.log('updated');");
    }

    [Fact]
    public async Task SettingsViewModel_AddAndRemoveExcludedProcesses()
    {
        await _settingsService.LoadSettingsAsync();

        var vm = new SettingsViewModel(_settingsService, _themeServiceMock.Object, _repository);

        vm.NewExcludedProcess = "TestApp";
        vm.AddExcludedProcess();

        vm.ExcludedProcesses.Should().Contain("TestApp.exe");

        vm.RemoveExcludedProcess("TestApp.exe");
        vm.ExcludedProcesses.Should().NotContain("TestApp.exe");
    }

    [Fact]
    public async Task FloatingPanelViewModel_NavigationAndSelection()
    {
        var item1 = new ClipItem { Id = Guid.NewGuid(), Title = "First Item" };
        var item2 = new ClipItem { Id = Guid.NewGuid(), Title = "Second Item" };
        await _repository.AddItemAsync(item1);
        await _repository.AddItemAsync(item2);

        var vm = new FloatingPanelViewModel(
            _repository,
            _clipboardServiceMock.Object,
            _inputSimulatorMock.Object,
            openMainWindow: () => { },
            addToQueue: card => { },
            openSettings: () => { });

        await vm.LoadItemsAsync();
        vm.Items.Should().HaveCount(2);
        vm.SelectedIndex.Should().Be(0);

        vm.MoveSelectionDown();
        vm.SelectedIndex.Should().Be(1);

        vm.MoveSelectionUp();
        vm.SelectedIndex.Should().Be(0);
    }
}

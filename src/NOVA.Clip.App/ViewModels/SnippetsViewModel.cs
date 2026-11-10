using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.App.ViewModels;

public partial class SnippetsViewModel : ObservableObject
{
    private readonly IClipRepository _repository;
    private readonly IClipboardService _clipboardService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private SnippetItem? _selectedSnippet;

    [ObservableProperty]
    private bool _isEditing;

    // Editor fields
    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private string _editorContent = string.Empty;

    [ObservableProperty]
    private string _editorDescription = string.Empty;

    [ObservableProperty]
    private string _editorTags = string.Empty;

    private Guid? _editingId;

    public ObservableCollection<SnippetItem> Snippets { get; } = [];

    public SnippetsViewModel(IClipRepository repository, IClipboardService clipboardService)
    {
        _repository = repository;
        _clipboardService = clipboardService;
    }

    [RelayCommand]
    public async Task LoadSnippetsAsync()
    {
        var all = await _repository.GetAllSnippetsAsync();
        Snippets.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchQuery)
            ? all
            : all.Where(s => s.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                             s.Content.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                             (s.Tags != null && s.Tags.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)));

        foreach (var s in filtered)
        {
            Snippets.Add(s);
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        _ = LoadSnippetsAsync();
    }

    [RelayCommand]
    public void StartNewSnippet()
    {
        _editingId = null;
        EditorTitle = string.Empty;
        EditorContent = string.Empty;
        EditorDescription = string.Empty;
        EditorTags = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    public void EditSnippet(SnippetItem snippet)
    {
        _editingId = snippet.Id;
        EditorTitle = snippet.Title;
        EditorContent = snippet.Content;
        EditorDescription = snippet.Description ?? string.Empty;
        EditorTags = snippet.Tags ?? string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    public void CancelEdit()
    {
        IsEditing = false;
        _editingId = null;
    }

    [RelayCommand]
    public async Task SaveSnippetAsync()
    {
        if (string.IsNullOrWhiteSpace(EditorTitle) || string.IsNullOrWhiteSpace(EditorContent))
        {
            return;
        }

        if (_editingId.HasValue)
        {
            var snippet = await _repository.GetSnippetByIdAsync(_editingId.Value);
            if (snippet != null)
            {
                snippet.Title = EditorTitle.Trim();
                snippet.Content = EditorContent;
                snippet.Description = string.IsNullOrWhiteSpace(EditorDescription) ? null : EditorDescription.Trim();
                snippet.Tags = string.IsNullOrWhiteSpace(EditorTags) ? null : EditorTags.Trim();
                await _repository.UpdateSnippetAsync(snippet);
            }
        }
        else
        {
            var snippet = new SnippetItem
            {
                Title = EditorTitle.Trim(),
                Content = EditorContent,
                Description = string.IsNullOrWhiteSpace(EditorDescription) ? null : EditorDescription.Trim(),
                Tags = string.IsNullOrWhiteSpace(EditorTags) ? null : EditorTags.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _repository.AddSnippetAsync(snippet);
        }

        IsEditing = false;
        await LoadSnippetsAsync();
    }

    [RelayCommand]
    public async Task DeleteSnippetAsync(SnippetItem snippet)
    {
        await _repository.DeleteSnippetAsync(snippet.Id);
        Snippets.Remove(snippet);
    }

    [RelayCommand]
    public async Task CopySnippetAsync(SnippetItem snippet)
    {
        var payload = new PayloadData { PlainText = snippet.Content };
        await _clipboardService.RestorePayloadToClipboardAsync(payload, ClipContentType.Text);
    }
}

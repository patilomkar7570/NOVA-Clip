using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NOVA.Clip.App.ViewModels;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.App.Views;

public partial class ItemDetailsDialog : Window
{
    private readonly ItemCardViewModel _card;

    public ItemDetailsDialog(ItemCardViewModel card)
    {
        _card = card;
        InitializeComponent();

        Loaded += async (s, e) => await LoadDetailsAsync();
    }

    private async Task LoadDetailsAsync()
    {
        ItemTitleText.Text = _card.Title;
        ItemMetaText.Text = $"{_card.Type} • {_card.FriendlyTime}";
        SourceAppText.Text = string.IsNullOrEmpty(_card.SourceApplication) ? "Unknown" : _card.SourceApplication;
        WindowTitleText.Text = string.IsNullOrEmpty(_card.SourceWindowTitle) ? "None" : _card.SourceWindowTitle;
        CreatedText.Text = _card.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        SizeText.Text = FormatBytes(_card.ByteSize);

        RawContentTextBox.Text = "Loading content...";

        var clipboardService = App.ServiceProvider?.GetService<IClipboardService>();
        if (clipboardService != null)
        {
            var payload = await clipboardService.GetPayloadAsync(_card.Model);
            if (payload != null)
            {
                if (!string.IsNullOrEmpty(payload.PlainText))
                {
                    RawContentTextBox.Text = payload.PlainText;
                }
                else if (payload.FilePaths != null && payload.FilePaths.Length > 0)
                {
                    RawContentTextBox.Text = "Files:\n" + string.Join("\n", payload.FilePaths);
                }
                else if (payload.ImageData != null)
                {
                    RawContentTextBox.Text = $"Image binary data ({payload.ImageWidth}x{payload.ImageHeight} pixels, {payload.ImageData.Length} bytes)";
                }
                else if (!string.IsNullOrEmpty(payload.HtmlText))
                {
                    RawContentTextBox.Text = payload.HtmlText;
                }
                else
                {
                    RawContentTextBox.Text = _card.PreviewText ?? "No text preview available";
                }
            }
            else
            {
                RawContentTextBox.Text = _card.PreviewText ?? "Content could not be decrypted";
            }
        }
    }

    private async void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        await _card.CopyAsync();
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}

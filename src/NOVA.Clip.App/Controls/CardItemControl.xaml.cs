using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using NOVA.Clip.App.ViewModels;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.App.Controls;

public partial class CardItemControl : UserControl
{
    private Point _dragStartPoint;

    public CardItemControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is ItemCardViewModel vm)
        {
            UpdateVisuals(vm);
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(ItemCardViewModel.IsFavorite))
                {
                    UpdateFavoriteVisual(vm.IsFavorite);
                }
            };
        }
    }

    private void UpdateVisuals(ItemCardViewModel vm)
    {
        // Update type icon
        string iconKey = vm.Type switch
        {
            ClipContentType.Link => "IconLink",
            ClipContentType.RichText => "IconRichText",
            ClipContentType.Image => "IconImage",
            ClipContentType.Files => "IconFiles",
            _ => "IconText"
        };

        if (TryFindResource(iconKey) is Geometry geo)
        {
            TypeIconPath.Data = geo;
        }

        UpdateFavoriteVisual(vm.IsFavorite);
    }

    private void UpdateFavoriteVisual(bool isFavorite)
    {
        if (isFavorite)
        {
            FavoriteStarPath.Fill = (Brush)FindResource("BrushFavorite");
            FavoriteStarPath.Stroke = (Brush)FindResource("BrushFavorite");
        }
        else
        {
            FavoriteStarPath.Fill = Brushes.Transparent;
            FavoriteStarPath.Stroke = (Brush)FindResource("BrushTextMuted");
        }
    }

    private void DragHandle_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _dragStartPoint = e.GetPosition(null);
        }
    }

    private async void DragHandle_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && DataContext is ItemCardViewModel vm)
        {
            Point currentPoint = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPoint;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var clipboardService = App.ServiceProvider?.GetService<IClipboardService>();
                if (clipboardService == null) return;

                var payload = await clipboardService.GetPayloadAsync(vm.Model);
                if (payload == null) return;

                var dataObj = new System.Windows.DataObject();

                switch (vm.Type)
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
                            using var ms = new MemoryStream(payload.ImageData);
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.StreamSource = ms;
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            bmp.Freeze();
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

                try
                {
                    DragDrop.DoDragDrop(this, dataObj, DragDropEffects.Copy | DragDropEffects.Move);
                }
                catch { }
            }
        }
    }
}

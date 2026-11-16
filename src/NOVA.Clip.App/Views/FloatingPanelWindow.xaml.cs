using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using NOVA.Clip.App.ViewModels;
using NOVA.Clip.Core.Configuration;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.App.Views;

public partial class FloatingPanelWindow : Window
{
    private readonly FloatingPanelViewModel _viewModel;
    private readonly AppSettings _settings;
    private IntPtr _targetWindowHwnd = IntPtr.Zero;

    private Point _dragStartPoint;
    private ItemCardViewModel? _draggedCard;
    private bool _hasDragged;

    public FloatingPanelWindow(FloatingPanelViewModel viewModel, AppSettings settings)
    {
        _viewModel = viewModel;
        _settings = settings;
        DataContext = _viewModel;
        InitializeComponent();

        _viewModel.CloseRequested += () => Hide();

        Loaded += (s, e) => RestoreWindowPosition();
        Closing += (s, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    public void ShowPanel(IntPtr previousForegroundHwnd)
    {
        _targetWindowHwnd = previousForegroundHwnd;

        RestoreWindowPosition();
        Show();
        Activate();

        _ = _viewModel.LoadItemsAsync();
    }

    private void RestoreWindowPosition()
    {
        var workArea = SystemParameters.WorkArea;

        // Cover ~40% of the screen width, centered horizontally
        double targetWidth = Math.Round(Math.Clamp(workArea.Width * 0.40, 640, 940));
        Width = targetWidth;
        Height = 68;
        Opacity = 0.98;

        // Position directly at the top center, right below the camera notch
        Left = workArea.Left + (workArea.Width - Width) / 2.0;
        Top = workArea.Top + 8;
    }

    private void Island_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && e.OriginalSource is not Button && e.OriginalSource is not System.Windows.Shapes.Path)
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }

    private void Slot_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedCard = (sender as FrameworkElement)?.DataContext as ItemCardViewModel;
            _hasDragged = false;
        }
    }

    private async void Slot_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _draggedCard != null && !_draggedCard.IsEmptySlot && !_hasDragged)
        {
            Point currentPoint = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPoint;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _hasDragged = true;

                var clipboardService = App.ServiceProvider?.GetService<IClipboardService>();
                if (clipboardService == null) return;

                var payload = await clipboardService.GetPayloadAsync(_draggedCard.Model);
                if (payload == null) return;

                var dataObj = new System.Windows.DataObject();

                switch (_draggedCard.Type)
                {
                    case ClipContentType.Files:
                        if (payload.FilePaths != null && payload.FilePaths.Length > 0)
                        {
                            var collection = new System.Collections.Specialized.StringCollection();
                            collection.AddRange(payload.FilePaths);
                            dataObj.SetFileDropList(collection);
                        }
                        break;

                    case ClipContentType.Image:
                        if (!string.IsNullOrEmpty(_draggedCard.StorageAssetPath) && File.Exists(_draggedCard.StorageAssetPath))
                        {
                            try
                            {
                                var bmp = new BitmapImage(new Uri(_draggedCard.StorageAssetPath));
                                dataObj.SetImage(bmp);
                            }
                            catch { }
                        }
                        break;

                    default:
                        if (!string.IsNullOrEmpty(payload.PlainText))
                        {
                            dataObj.SetText(payload.PlainText);
                        }
                        break;
                }

                if (sender is DependencyObject dragSource)
                {
                    DragDrop.DoDragDrop(dragSource, dataObj, DragDropEffects.Copy);
                }
            }
        }
    }

    private void Slot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_hasDragged && _draggedCard != null && !_draggedCard.IsEmptySlot)
        {
            _ = _viewModel.SlotClickedAsync(_draggedCard);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        // Quick numeric slot triggers: 1, 2, 3, 4
        int slotIndex = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            _ => -1
        };

        if (slotIndex >= 0 && slotIndex < _viewModel.RecentSlots.Count)
        {
            var card = _viewModel.RecentSlots[slotIndex];
            if (!card.IsEmptySlot)
            {
                _ = _viewModel.SlotClickedAsync(card);
                e.Handled = true;
            }
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Hide();
    }
}

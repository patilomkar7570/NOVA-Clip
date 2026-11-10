using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NOVA.Clip.App.ViewModels;
using NOVA.Clip.Core.Models;

namespace NOVA.Clip.App.Controls;

public partial class QueueItemControl : UserControl
{
    public QueueItemControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        MoveUpBtn.Click += (s, e) =>
        {
            if (DataContext is PasteQueueItem item && FindParentPasteQueueViewModel() is { } vm)
            {
                _ = vm.MoveItemUpAsync(item);
            }
        };

        MoveDownBtn.Click += (s, e) =>
        {
            if (DataContext is PasteQueueItem item && FindParentPasteQueueViewModel() is { } vm)
            {
                _ = vm.MoveItemDownAsync(item);
            }
        };

        RemoveBtn.Click += (s, e) =>
        {
            if (DataContext is PasteQueueItem item && FindParentPasteQueueViewModel() is { } vm)
            {
                _ = vm.RemoveItemAsync(item);
            }
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is PasteQueueItem item)
        {
            OrderIndexText.Text = (item.OrderIndex + 1).ToString();
            CompletedCheckPath.Visibility = item.IsCompleted ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private PasteQueueViewModel? FindParentPasteQueueViewModel()
    {
        DependencyObject current = this;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is PasteQueueViewModel vm)
            {
                return vm;
            }
            if (current is FrameworkElement feMain && feMain.DataContext is MainViewModel mainVm)
            {
                return mainVm.QueueVm;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NOVA.Clip.App.ViewModels;
using NOVA.Clip.Core.Configuration;
using NOVA.Clip.Infrastructure.Windows;

namespace NOVA.Clip.App.Views;

public partial class FloatingPanelWindow : Window
{
    private readonly FloatingPanelViewModel _viewModel;
    private readonly AppSettings _settings;
    private IntPtr _targetWindowHwnd = IntPtr.Zero;

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
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void RestoreWindowPosition()
    {
        double width = _settings.FloatingPanelWidth > 200 ? _settings.FloatingPanelWidth : 380;
        double height = _settings.FloatingPanelHeight > 300 ? _settings.FloatingPanelHeight : 520;
        Width = width;
        Height = height;
        Opacity = _settings.FloatingPanelOpacity;

        if (_settings.FloatingPanelLeft > 0 && _settings.FloatingPanelTop > 0)
        {
            Left = _settings.FloatingPanelLeft;
            Top = _settings.FloatingPanelTop;
        }
        else
        {
            // Center in screen
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
        }

        EnsureVisibleOnScreen();
    }

    private void EnsureVisibleOnScreen()
    {
        var workArea = SystemParameters.WorkArea;
        if (Left < workArea.Left) Left = workArea.Left + 10;
        if (Top < workArea.Top) Top = workArea.Top + 10;
        if (Left + Width > workArea.Right) Left = workArea.Right - Width - 10;
        if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height - 10;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
            _settings.FloatingPanelLeft = Left;
            _settings.FloatingPanelTop = Top;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void FilterChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string filter)
        {
            _viewModel.SelectedFilter = filter;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            _viewModel.MoveSelectionUp();
            ItemsListBox.ScrollIntoView(_viewModel.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            _viewModel.MoveSelectionDown();
            ItemsListBox.ScrollIntoView(_viewModel.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (_viewModel.SelectedItem != null)
            {
                _ = _viewModel.PasteItemAsync(_viewModel.SelectedItem, _targetWindowHwnd);
            }
            e.Handled = true;
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        Hide();
    }

    private void ItemsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedItem != null)
        {
            _ = _viewModel.PasteItemAsync(_viewModel.SelectedItem, _targetWindowHwnd);
        }
    }
}

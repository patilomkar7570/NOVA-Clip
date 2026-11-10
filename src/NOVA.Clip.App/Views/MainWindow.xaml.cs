using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NOVA.Clip.App.Services;
using NOVA.Clip.App.ViewModels;
using NOVA.Clip.Core.Configuration;
using NOVA.Clip.Core.Interfaces;

namespace NOVA.Clip.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly TrayIconService _trayIconService;
    private bool _isExplicitExit;

    public MainWindow(
        MainViewModel viewModel,
        ISettingsService settingsService,
        TrayIconService trayIconService)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _trayIconService = trayIconService;
        DataContext = _viewModel;

        InitializeComponent();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.NotificationRequested += (title, msg) => _trayIconService.ShowNotification(title, msg);

        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDetailsOpen))
        {
            if (_viewModel.IsDetailsOpen && _viewModel.SelectedItemForDetails != null)
            {
                var dialog = new ItemDetailsDialog(_viewModel.SelectedItemForDetails)
                {
                    Owner = this
                };
                dialog.ShowDialog();
                _viewModel.CloseDetails();
            }
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string section)
        {
            _viewModel.Navigate(section);
            HighlightNavButton(btn);
        }
    }

    private void HighlightNavButton(Button activeButton)
    {
        // Reset sidebar button styles
        var parent = VisualTreeHelper.GetParent(activeButton) as Panel;
        if (parent != null)
        {
            foreach (var child in parent.Children)
            {
                if (child is Button b)
                {
                    b.Style = (Style)FindResource("NavButtonStyle");
                }
            }
            activeButton.Style = (Style)FindResource("ActiveNavButtonStyle");
        }
    }

    private async void PasteNextButton_Click(object sender, RoutedEventArgs e)
    {
        // Get foreground window of another app
        Hide();
        await Task.Delay(150);
        await _viewModel.QueueVm.PasteNextAsync(IntPtr.Zero);
    }

    private async void PasteAllButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        await Task.Delay(150);
        await _viewModel.QueueVm.PasteAllAsync(IntPtr.Zero);
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settingsService.Settings.MinimizeToTray)
        {
            Hide();
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExplicitExit && _settingsService.Settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _trayIconService.ShowNotification("NOVA Clip", "Running in the background. Press Ctrl+Shift+Space to open floating clipboard.");
        }
    }

    public void ForceClose()
    {
        _isExplicitExit = true;
        Close();
    }
}

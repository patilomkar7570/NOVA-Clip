using System.Windows;
using System.Windows.Input;
using NOVA.Clip.Core.Configuration;

namespace NOVA.Clip.App.Views;

public partial class CornerLauncherWindow : Window
{
    private readonly Action _openFloatingPanel;
    private readonly AppSettings _settings;

    public CornerLauncherWindow(AppSettings settings, Action openFloatingPanel)
    {
        _settings = settings;
        _openFloatingPanel = openFloatingPanel;
        InitializeComponent();

        Loaded += (s, e) => PositionLauncher();
    }

    public void PositionLauncher()
    {
        var workArea = SystemParameters.WorkArea;
        double size = _settings.CornerLauncherSize > 24 ? _settings.CornerLauncherSize : 44;
        Width = size + 6;
        Height = size + 6;
        LauncherBorder.Width = size;
        LauncherBorder.Height = size;
        LauncherBorder.CornerRadius = new CornerRadius(size / 2);
        Opacity = _settings.CornerLauncherOpacity;

        switch (_settings.CornerLauncherPosition)
        {
            case "TopLeft":
                Left = workArea.Left + 20;
                Top = workArea.Top + 20;
                break;
            case "TopRight":
                Left = workArea.Right - Width - 20;
                Top = workArea.Top + 20;
                break;
            case "BottomLeft":
                Left = workArea.Left + 20;
                Top = workArea.Bottom - Height - 20;
                break;
            default: // BottomRight
                Left = workArea.Right - Width - 24;
                Top = workArea.Bottom - Height - 24;
                break;
        }
    }

    private void LauncherBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _openFloatingPanel();
    }
}

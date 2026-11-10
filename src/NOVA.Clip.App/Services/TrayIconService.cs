using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Extensions.Logging;
using NOVA.Clip.Core.Configuration;
using NOVA.Clip.Core.Interfaces;

namespace NOVA.Clip.App.Services;

public class TrayIconService : IDisposable
{
    private const int WM_USER = 0x0400;
    public const int WM_TRAYICON = WM_USER + 100;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;

    private const uint NIIF_INFO = 0x00000001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private readonly ISettingsService _settingsService;
    private readonly ILogger<TrayIconService> _logger;
    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isAdded;
    private ContextMenu? _contextMenu;
    private MenuItem? _pauseResumeMenuItem;

    public event EventHandler? OpenMainAppRequested;
    public event EventHandler? OpenFloatingPanelRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService(ISettingsService settingsService, ILogger<TrayIconService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public void Initialize(IntPtr hwnd)
    {
        if (_isAdded) return;
        _hwnd = hwnd;

        try
        {
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(icoPath))
            {
                _hIcon = LoadImage(IntPtr.Zero, icoPath, 1 /* IMAGE_ICON */, 16, 16, 0x00000010 /* LR_LOADFROMFILE */);
            }
        }
        catch { }

        var data = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = $"{AppSettings.ApplicationName} v{AppSettings.Version}"
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref data);
        if (_isAdded)
        {
            _logger.LogInformation("Native Shell_NotifyIcon added successfully");
            BuildContextMenu();
        }
        else
        {
            _logger.LogWarning("Failed to add Shell_NotifyIcon");
        }
    }

    private void BuildContextMenu()
    {
        _contextMenu = new ContextMenu();

        var openApp = new MenuItem { Header = "Open NOVA Clip", FontWeight = FontWeights.Bold };
        openApp.Click += (s, e) => OpenMainAppRequested?.Invoke(this, EventArgs.Empty);

        var openFloating = new MenuItem { Header = "Floating Quick Panel (Ctrl+Shift+Space)" };
        openFloating.Click += (s, e) => OpenFloatingPanelRequested?.Invoke(this, EventArgs.Empty);

        bool isPaused = _settingsService.Settings.PauseMonitoring;
        _pauseResumeMenuItem = new MenuItem { Header = isPaused ? "Resume Monitoring" : "Pause Monitoring" };
        _pauseResumeMenuItem.Click += (s, e) => ToggleMonitoring();

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.Add(openApp);
        _contextMenu.Items.Add(openFloating);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(_pauseResumeMenuItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(exitItem);
    }

    public void ProcessWindowMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != WM_TRAYICON) return;

        int eventType = lParam.ToInt32();
        if (eventType is WM_LBUTTONUP or WM_LBUTTONDBLCLK)
        {
            OpenFloatingPanelRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (eventType == WM_RBUTTONUP)
        {
            ShowContextMenu();
        }
    }

    private void ShowContextMenu()
    {
        if (_contextMenu == null) return;

        GetCursorPos(out var pt);
        _contextMenu.Placement = PlacementMode.AbsolutePoint;
        _contextMenu.HorizontalOffset = pt.X;
        _contextMenu.VerticalOffset = pt.Y;
        _contextMenu.IsOpen = true;
    }

    private void ToggleMonitoring()
    {
        var s = _settingsService.Settings;
        s.PauseMonitoring = !s.PauseMonitoring;
        _ = _settingsService.SaveSettingsAsync();

        if (_pauseResumeMenuItem != null)
        {
            _pauseResumeMenuItem.Header = s.PauseMonitoring ? "Resume Monitoring" : "Pause Monitoring";
        }

        string status = s.PauseMonitoring ? "paused" : "resumed";
        ShowNotification("NOVA Clip", $"Clipboard monitoring is now {status}.");
    }

    public void ShowNotification(string title, string text)
    {
        if (!_isAdded || _hwnd == IntPtr.Zero) return;

        var data = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_INFO,
            szInfo = text.Length > 250 ? text[..250] : text,
            szInfoTitle = title.Length > 60 ? title[..60] : title,
            dwInfoFlags = NIIF_INFO
        };

        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_isAdded && _hwnd != IntPtr.Zero)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1001
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _isAdded = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}

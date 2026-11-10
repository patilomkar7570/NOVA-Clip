using Microsoft.Extensions.Logging;
using NOVA.Clip.Core.Interfaces;
using NOVA.Clip.Infrastructure.Windows;

namespace NOVA.Clip.Infrastructure.Clipboard;

public class ClipboardMonitor : IClipboardMonitor
{
    private readonly IInputSimulator _inputSimulator;
    private readonly ILogger<ClipboardMonitor> _logger;
    private IntPtr _windowHandle = IntPtr.Zero;
    private bool _isMonitoring;

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    public bool IsMonitoring => _isMonitoring;

    public ClipboardMonitor(IInputSimulator inputSimulator, ILogger<ClipboardMonitor> logger)
    {
        _inputSimulator = inputSimulator;
        _logger = logger;
    }

    public void Start(IntPtr windowHandle)
    {
        if (_isMonitoring) return;
        if (windowHandle == IntPtr.Zero)
        {
            _logger.LogWarning("Cannot start clipboard monitor without valid window handle");
            return;
        }

        _windowHandle = windowHandle;
        bool added = NativeMethods.AddClipboardFormatListener(_windowHandle);
        if (added)
        {
            _isMonitoring = true;
            _logger.LogInformation("AddClipboardFormatListener succeeded on HWND {Hwnd}", _windowHandle);
        }
        else
        {
            _logger.LogError("Failed to add clipboard format listener on HWND {Hwnd}", _windowHandle);
        }
    }

    public void HandleWindowMessage(int msg)
    {
        if (!_isMonitoring || msg != NativeMethods.WM_CLIPBOARDUPDATE) return;

        IntPtr foregroundHwnd = _inputSimulator.GetForegroundWindow();
        string? processName = _inputSimulator.GetProcessName(foregroundHwnd);
        string? windowTitle = _inputSimulator.GetWindowTitle(foregroundHwnd);

        ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs
        {
            ForegroundWindowHwnd = foregroundHwnd,
            ProcessName = processName,
            WindowTitle = windowTitle,
            Timestamp = DateTime.UtcNow
        });
    }

    public void Stop()
    {
        if (!_isMonitoring) return;

        if (_windowHandle != IntPtr.Zero)
        {
            NativeMethods.RemoveClipboardFormatListener(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }

        _isMonitoring = false;
        _logger.LogInformation("Clipboard monitor stopped");
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}

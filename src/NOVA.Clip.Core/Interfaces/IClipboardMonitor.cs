namespace NOVA.Clip.Core.Interfaces;

public class ClipboardChangedEventArgs : EventArgs
{
    public IntPtr ForegroundWindowHwnd { get; set; }
    public string? ProcessName { get; set; }
    public string? WindowTitle { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public interface IClipboardMonitor : IDisposable
{
    event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
    bool IsMonitoring { get; }
    void Start(IntPtr windowHandle);
    void Stop();
}

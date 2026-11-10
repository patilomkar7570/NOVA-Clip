namespace NOVA.Clip.Core.Interfaces;

public interface IInputSimulator
{
    IntPtr GetForegroundWindow();
    string? GetProcessName(IntPtr hwnd);
    string? GetWindowTitle(IntPtr hwnd);
    bool SetForegroundWindow(IntPtr hwnd);
    Task<bool> SimulatePasteAsync(IntPtr targetHwnd);
}

namespace NOVA.Clip.Core.Interfaces;

public enum HotKeyAction
{
    ToggleFloatingPanel,
    CopyNext,
    PasteNext
}

public class HotKeyEventArgs : EventArgs
{
    public HotKeyAction Action { get; set; }
}

public interface IHotKeyService : IDisposable
{
    event EventHandler<HotKeyEventArgs>? HotKeyTriggered;
    bool Register(HotKeyAction action, string modifierString, string keyString, IntPtr hwnd);
    void Unregister(HotKeyAction action);
    void UnregisterAll();
    bool ProcessHotKeyMessage(int msg, IntPtr wParam, IntPtr lParam);
}

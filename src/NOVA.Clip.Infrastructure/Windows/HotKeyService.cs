using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NOVA.Clip.Core.Interfaces;

namespace NOVA.Clip.Infrastructure.Windows;

public class HotKeyService : IHotKeyService
{
    private readonly ILogger<HotKeyService> _logger;
    private readonly Dictionary<HotKeyAction, (int Id, IntPtr Hwnd)> _registeredHotKeys = [];
    private int _currentHotKeyId = 9000;

    public event EventHandler<HotKeyEventArgs>? HotKeyTriggered;

    public HotKeyService(ILogger<HotKeyService> logger)
    {
        _logger = logger;
    }

    public bool Register(HotKeyAction action, string modifierString, string keyString, IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            _logger.LogWarning("Cannot register hotkey without valid window handle");
            return false;
        }

        Unregister(action);

        uint modifiers = ParseModifiers(modifierString) | NativeMethods.MOD_NOREPEAT;
        uint vk = ParseVirtualKey(keyString);

        if (vk == 0)
        {
            _logger.LogWarning("Invalid virtual key for {Key}", keyString);
            return false;
        }

        int id = Interlocked.Increment(ref _currentHotKeyId);
        bool success = NativeMethods.RegisterHotKey(hwnd, id, modifiers, vk);

        if (success)
        {
            _registeredHotKeys[action] = (id, hwnd);
            _logger.LogInformation("Registered global hotkey for {Action}: {Mod}+{Key} (ID {Id})", action, modifierString, keyString, id);
            return true;
        }
        else
        {
            _logger.LogWarning("Failed to register hotkey {Mod}+{Key} for {Action} (conflict with another application)", modifierString, keyString, action);
            return false;
        }
    }

    public void Unregister(HotKeyAction action)
    {
        if (_registeredHotKeys.TryGetValue(action, out var entry))
        {
            NativeMethods.UnregisterHotKey(entry.Hwnd, entry.Id);
            _registeredHotKeys.Remove(action);
            _logger.LogInformation("Unregistered hotkey for {Action}", action);
        }
    }

    public void UnregisterAll()
    {
        foreach (var entry in _registeredHotKeys.Values)
        {
            NativeMethods.UnregisterHotKey(entry.Hwnd, entry.Id);
        }
        _registeredHotKeys.Clear();
    }

    public bool ProcessHotKeyMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != NativeMethods.WM_HOTKEY) return false;

        int hotKeyId = wParam.ToInt32();
        foreach (var pair in _registeredHotKeys)
        {
            if (pair.Value.Id == hotKeyId)
            {
                HotKeyTriggered?.Invoke(this, new HotKeyEventArgs { Action = pair.Key });
                return true;
            }
        }

        return false;
    }

    private static uint ParseModifiers(string modifierString)
    {
        uint mod = 0;
        if (string.IsNullOrWhiteSpace(modifierString)) return mod;

        var parts = modifierString.Split(['+', ' ', ','], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            string p = part.Trim().ToLowerInvariant();
            if (p is "ctrl" or "control") mod |= NativeMethods.MOD_CONTROL;
            else if (p is "alt") mod |= NativeMethods.MOD_ALT;
            else if (p is "shift") mod |= NativeMethods.MOD_SHIFT;
            else if (p is "win" or "windows") mod |= NativeMethods.MOD_WIN;
        }
        return mod;
    }

    private static uint ParseVirtualKey(string keyString)
    {
        if (string.IsNullOrWhiteSpace(keyString)) return 0;

        string k = keyString.Trim();
        if (Enum.TryParse<Key>(k, true, out var wpfKey))
        {
            return (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
        }

        if (k.Equals("Space", StringComparison.OrdinalIgnoreCase)) return NativeMethods.VK_SPACE;

        return 0;
    }

    public void Dispose()
    {
        UnregisterAll();
        GC.SuppressFinalize(this);
    }
}

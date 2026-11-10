using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NOVA.Clip.Core.Interfaces;

namespace NOVA.Clip.Infrastructure.Windows;

public class WindowsInputSimulator : IInputSimulator
{
    private readonly ILogger<WindowsInputSimulator> _logger;

    public WindowsInputSimulator(ILogger<WindowsInputSimulator> logger)
    {
        _logger = logger;
    }

    public IntPtr GetForegroundWindow()
    {
        return NativeMethods.GetForegroundWindow();
    }

    public string? GetProcessName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return null;

        try
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;

            string? fullPath = NativeMethods.GetProcessImageName(pid);
            if (!string.IsNullOrEmpty(fullPath))
            {
                return Path.GetFileName(fullPath);
            }

            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName + ".exe";
        }
        catch
        {
            return null;
        }
    }

    public string? GetWindowTitle(IntPtr hwnd)
    {
        return NativeMethods.GetWindowTextSafe(hwnd);
    }

    public bool SetForegroundWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;

        IntPtr currentForeground = NativeMethods.GetForegroundWindow();
        if (currentForeground == hwnd) return true;

        uint currentThread = NativeMethods.GetCurrentThreadId();
        uint targetThread = NativeMethods.GetWindowThreadProcessId(hwnd, out _);

        bool attached = false;
        if (currentThread != targetThread && targetThread != 0)
        {
            attached = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
        }

        try
        {
            NativeMethods.BringWindowToTop(hwnd);
            return NativeMethods.SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThread, targetThread, false);
            }
        }
    }

    public async Task<bool> SimulatePasteAsync(IntPtr targetHwnd)
    {
        try
        {
            if (targetHwnd != IntPtr.Zero)
            {
                SetForegroundWindow(targetHwnd);
                await Task.Delay(60); // Allow Windows message queue to complete focus change
            }

            var inputs = new NativeMethods.INPUT[4];

            // 1. Ctrl Down
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = NativeMethods.VK_CONTROL;

            // 2. V Down
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = NativeMethods.VK_V;

            // 3. V Up
            inputs[2].type = NativeMethods.INPUT_KEYBOARD;
            inputs[2].u.ki.wVk = NativeMethods.VK_V;
            inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            // 4. Ctrl Up
            inputs[3].type = NativeMethods.INPUT_KEYBOARD;
            inputs[3].u.ki.wVk = NativeMethods.VK_CONTROL;
            inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            int structSize = Marshal.SizeOf<NativeMethods.INPUT>();
            uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, structSize);

            if (sent != inputs.Length)
            {
                int error = Marshal.GetLastWin32Error();
                _logger.LogWarning("SendInput partially or fully failed. Sent: {Sent}, Win32Error: {Error}", sent, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate paste via SendInput");
            return false;
        }
    }
}

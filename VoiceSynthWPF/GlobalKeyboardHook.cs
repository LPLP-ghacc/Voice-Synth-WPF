using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace VoiceSynthWPF;

public sealed class GlobalKeyboardHook
{
    private static IntPtr _hookId = IntPtr.Zero;
    private static readonly LowLevelKeyboardProc Proc = HookCallback;

    public static event Action<Key>? KeyPressed;

    public static void Start()
    {
        if (_hookId != IntPtr.Zero)
            return;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;

        if (module != null)
            _hookId = SetWindowsHookEx(
                13, // WH_KEYBOARD_LL
                Proc,
                GetModuleHandle(module.ModuleName),
                0);
    }

    public static void Stop()
    {
        if (_hookId == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || wParam != 0x0100) return CallNextHookEx(_hookId, nCode, wParam, lParam); // WM_KEYDOWN
        var vkCode = Marshal.ReadInt32(lParam);
        var key = KeyInterop.KeyFromVirtualKey(vkCode);

        KeyPressed?.Invoke(key);

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook,
        LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk,
        int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace WinTrans.Services;

/// <summary>
/// Регистрирует глобальный хоткей через RegisterHotKey на HWND окна
/// и слушает WM_HOTKEY через SetWindowSubclass.
/// </summary>
public class HotkeyManager : IDisposable
{
    public event Action? HotkeyPressed;

    private const int HOTKEY_ID = 0xB00B;

    private readonly IntPtr _hwnd;
    private readonly DispatcherQueue _dispatcher;
    private readonly SUBCLASSPROC _subclassProc;
    private bool _registered;
    private bool _subclassed;

    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("Comctl32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass,
        UIntPtr uIdSubclass, UIntPtr dwRefData);

    [DllImport("Comctl32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass,
        UIntPtr uIdSubclass);

    [DllImport("Comctl32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    public HotkeyManager(Window window)
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _dispatcher = window.DispatcherQueue;
        _subclassProc = WndProc;
        _subclassed = SetWindowSubclass(_hwnd, _subclassProc, UIntPtr.Zero, UIntPtr.Zero);
    }

    public bool Register(uint modifiers, uint vk)
    {
        _registered = Win32.RegisterHotKey(_hwnd, HOTKEY_ID, modifiers, vk);
        if (!_registered)
        {
            int err = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"RegisterHotKey failed. Win32 error = {err}");
        }
        return _registered;
    }

    private IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (uMsg == Win32.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            // Возвращаемся на UI-поток, чтобы безопасно трогать XAML
            _dispatcher.TryEnqueue(() => HotkeyPressed?.Invoke());
            return IntPtr.Zero;
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_registered)
        {
            Win32.UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
        if (_subclassed)
        {
            RemoveWindowSubclass(_hwnd, _subclassProc, UIntPtr.Zero);
            _subclassed = false;
        }
    }
}

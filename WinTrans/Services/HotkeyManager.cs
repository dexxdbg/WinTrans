using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace WinTrans.Services;

/// <summary>
/// Субклассирует HWND окна WinUI3, слушает WM_HOTKEY и WM_TRAYICON,
/// даёт управление RegisterHotKey и (опционально) трею.
/// </summary>
public class HotkeyManager : IDisposable
{
    public event Action? HotkeyPressed;
    /// <summary>Вызывается ДО диспатча — в момент прихода системного сообщения трея.</summary>
    public Func<IntPtr, IntPtr, bool>? TrayMessageHandler;

    private const int HOTKEY_ID = 0xB00B;

    public IntPtr Hwnd { get; }
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
        Hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        _dispatcher = window.DispatcherQueue;
        _subclassProc = WndProc;
        _subclassed = SetWindowSubclass(Hwnd, _subclassProc, UIntPtr.Zero, UIntPtr.Zero);
    }

    public bool Register(uint modifiers, uint vk)
    {
        _registered = Win32.RegisterHotKey(Hwnd, HOTKEY_ID, modifiers, vk);
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
            _dispatcher.TryEnqueue(() => HotkeyPressed?.Invoke());
            return IntPtr.Zero;
        }

        if (uMsg == Win32.WM_TRAYICON && TrayMessageHandler is not null)
        {
            var handler = TrayMessageHandler;
            _dispatcher.TryEnqueue(() => handler(wParam, lParam));
            return IntPtr.Zero;
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_registered)
        {
            Win32.UnregisterHotKey(Hwnd, HOTKEY_ID);
            _registered = false;
        }
        if (_subclassed)
        {
            RemoveWindowSubclass(Hwnd, _subclassProc, UIntPtr.Zero);
            _subclassed = false;
        }
    }
}

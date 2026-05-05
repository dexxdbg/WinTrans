using System;

namespace WinTrans.Services;

public class TrayIcon : IDisposable
{
    private const uint TRAY_ID = 1;

    // Команды контекстного меню
    public const uint CMD_OPEN = 1001;
    public const uint CMD_EXIT = 1002;

    private readonly IntPtr _hwnd;
    private bool _added;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayIcon(IntPtr hwnd, string tooltip)
    {
        _hwnd = hwnd;

        // Берём иконку из самого exe, если не вышло — системную по умолчанию
        // Environment.ProcessPath — быстрее, чем Process.GetCurrentProcess().MainModule?.FileName
        var hInst = Win32.GetModuleHandle(null);
        IntPtr hIcon = IntPtr.Zero;
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                hIcon = Win32.ExtractIcon(hInst, exe, 0);
        }
        catch { }
        if (hIcon == IntPtr.Zero)
            hIcon = Win32.LoadIcon(IntPtr.Zero, Win32.IDI_APPLICATION);

        var data = new Win32.NOTIFYICONDATA
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = TRAY_ID,
            uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP,
            uCallbackMessage = (uint)Win32.WM_TRAYICON,
            hIcon = hIcon,
            szTip = tooltip,
        };

        _added = Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref data);
        if (_added)
        {
            data.uVersion = Win32.NOTIFYICON_VERSION_4;
            Win32.Shell_NotifyIcon(Win32.NIM_SETVERSION, ref data);
        }
    }

    /// <summary>
    /// Должен вызываться из WndProc при uMsg == WM_TRAYICON.
    /// Возвращает true если сообщение обработано.
    /// </summary>
    public bool HandleMessage(IntPtr wParam, IntPtr lParam)
    {
        // Нам прилетает LOWORD(lParam) = событие мыши
        uint mouseMsg = (uint)(lParam.ToInt64() & 0xFFFF);

        switch (mouseMsg)
        {
            case Win32.WM_LBUTTONUP:
            case Win32.WM_LBUTTONDBLCLK:
                OpenRequested?.Invoke();
                return true;

            case Win32.WM_RBUTTONUP:
                ShowContextMenu();
                return true;
        }
        return false;
    }

    private void ShowContextMenu()
    {
        var menu = Win32.CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        try
        {
            Win32.AppendMenu(menu, Win32.MF_STRING, new UIntPtr(CMD_OPEN), "Открыть WinTrans");
            Win32.AppendMenu(menu, Win32.MF_SEPARATOR, UIntPtr.Zero, string.Empty);
            Win32.AppendMenu(menu, Win32.MF_STRING, new UIntPtr(CMD_EXIT), "Выход");

            Win32.GetCursorPos(out var pt);

            // Обязательно для popup-меню из notify icon: окно должно быть в форграунде,
            // иначе меню не закроется по клику вне.
            Win32.SetForegroundWindow(_hwnd);

            int cmd = Win32.TrackPopupMenuEx(
                menu,
                Win32.TPM_RETURNCMD | Win32.TPM_RIGHTBUTTON | Win32.TPM_NONOTIFY,
                pt.X, pt.Y, _hwnd, IntPtr.Zero);

            if (cmd == (int)CMD_OPEN) OpenRequested?.Invoke();
            else if (cmd == (int)CMD_EXIT) ExitRequested?.Invoke();
        }
        finally
        {
            Win32.DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = new Win32.NOTIFYICONDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = TRAY_ID,
            };
            Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref data);
            _added = false;
        }
    }
}

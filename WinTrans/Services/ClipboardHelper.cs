using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace WinTrans.Services;

public static class ClipboardHelper
{
    // call this only when our window is hidden and the previous app still has focus
    public static async Task<string?> GetSelectedTextAsync()
    {
        string? previous = null;
        try
        {
            var old = Clipboard.GetContent();
            if (old.Contains(StandardDataFormats.Text))
                previous = await old.GetTextAsync();
        }
        catch { }

        // clear so we can tell if ctrl+c actually copied something
        try { Clipboard.Clear(); } catch { }

        // wait for focus to return to the target window and for hotkey modifiers to release
        await Task.Delay(120);

        Win32.SendCtrlCombo(Win32.VK_C);

        // give the target app time to write to clipboard
        await Task.Delay(200);

        string? selected = null;
        try
        {
            var dp = Clipboard.GetContent();
            if (dp.Contains(StandardDataFormats.Text))
                selected = await dp.GetTextAsync();
        }
        catch { }

        // put back whatever was in the clipboard before
        if (!string.IsNullOrEmpty(previous))
        {
            try
            {
                var restore = new DataPackage();
                restore.SetText(previous);
                Clipboard.SetContent(restore);
            }
            catch { }
        }

        return selected;
    }

    public static async Task SetTextAndPasteBackAsync(string text, Window ourWindow,
        bool restoreOurWindow = true)
    {
        var dp = new DataPackage();
        dp.SetText(text);
        Clipboard.SetContent(dp);

        // hide so ctrl+v goes to the app behind us
        var ourHwnd = WinRT.Interop.WindowNative.GetWindowHandle(ourWindow);
        Win32.ShowWindow(ourHwnd, Win32.SW_HIDE);

        await Task.Delay(150);

        Win32.SendCtrlCombo(Win32.VK_V);

        if (restoreOurWindow)
        {
            await Task.Delay(150);
            Win32.ShowWindow(ourHwnd, Win32.SW_SHOW);
            Win32.SetForegroundWindow(ourHwnd);
        }
    }
}

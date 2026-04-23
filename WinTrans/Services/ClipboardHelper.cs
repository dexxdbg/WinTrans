using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace WinTrans.Services;

public static class ClipboardHelper
{
    /// <summary>
    /// ВАЖНО: вызывать, когда фокус ещё у исходного окна (наше окно скрыто).
    /// Сохраняет текущее содержимое буфера, шлёт Ctrl+C,
    /// читает выделенный текст, возвращает буфер обратно.
    /// </summary>
    public static async Task<string?> GetSelectedTextAsync()
    {
        string? previous = null;
        try
        {
            var old = Clipboard.GetContent();
            if (old.Contains(StandardDataFormats.Text))
                previous = await old.GetTextAsync();
        }
        catch { /* ignore */ }

        // Чистим, чтобы отличить «ничего не выделено» от старого содержимого
        try { Clipboard.Clear(); } catch { }

        // Даём фокус успеть вернуться исходному окну и модификаторам хоткея —
        // отпуститься естественным путём
        await Task.Delay(120);

        // Эмулируем Ctrl+C (SendCtrlCombo сам отпустит залипшие Shift/Alt/Win/Ctrl)
        Win32.SendCtrlCombo(Win32.VK_C);

        // Ждём пока буфер обновится
        await Task.Delay(200);

        string? selected = null;
        try
        {
            var dp = Clipboard.GetContent();
            if (dp.Contains(StandardDataFormats.Text))
                selected = await dp.GetTextAsync();
        }
        catch { }

        // Восстанавливаем предыдущее содержимое буфера (с небольшой задержкой,
        // чтобы не перебить наш только что прочитанный selected)
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

    /// <summary>
    /// Кладёт переведённый текст в буфер, прячет наше окно, возвращает фокус
    /// предыдущему окну и шлёт Ctrl+V, чтобы заменить выделение.
    /// </summary>
    public static async Task SetTextAndPasteBackAsync(string text, Window ourWindow)
    {
        // 1. В буфер
        var dp = new DataPackage();
        dp.SetText(text);
        Clipboard.SetContent(dp);

        // 2. Прячем своё окно
        var ourHwnd = WinRT.Interop.WindowNative.GetWindowHandle(ourWindow);
        Win32.ShowWindow(ourHwnd, Win32.SW_HIDE);

        // 3. Небольшая задержка — система сама вернёт фокус предыдущему окну
        await Task.Delay(150);

        // 4. Эмулируем Ctrl+V
        Win32.SendCtrlCombo(Win32.VK_V);
    }
}

using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinTrans.Services;

namespace WinTrans;

public sealed partial class MainWindow : Window
{
    private HotkeyManager? _hotkeyManager;
    private TrayIcon? _tray;
    private readonly ClaudeApiClient _claude = new();
    private readonly SettingsStore _settings = new();

    // Ctrl+Shift+T -> MOD_CONTROL (0x0002) | MOD_SHIFT (0x0004), VK_T = 0x54
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_T = 0x54;

    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "WinTrans";

        // Подгружаем сохранённые настройки
        var saved = _settings.Load();
        if (!string.IsNullOrEmpty(saved.ApiKey))
            ApiKeyBox.Password = saved.ApiKey;
        if (!string.IsNullOrWhiteSpace(saved.BaseUrl))
            BaseUrlBox.Text = saved.BaseUrl;
        if (saved.LanguageIndex >= 0 && saved.LanguageIndex < LanguageBox.Items.Count)
            LanguageBox.SelectedIndex = saved.LanguageIndex;
        if (saved.StyleIndex >= 0 && saved.StyleIndex < StyleBox.Items.Count)
            StyleBox.SelectedIndex = saved.StyleIndex;

        // Перехватываем «Закрыть» (крестик) → прячем в трей вместо выхода
        this.AppWindow.Closing += AppWindow_Closing;

        this.Closed += (s, e) =>
        {
            _tray?.Dispose();
            _hotkeyManager?.Dispose();
        };
    }

    private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender,
        Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        // Не даём окну закрыться — вместо этого скрываем в трей
        args.Cancel = true;
        HideWindow();
    }

    public void InitializeHotkey()
    {
        _hotkeyManager = new HotkeyManager(this);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressedAsync;

        bool ok = _hotkeyManager.Register(MOD_CONTROL | MOD_SHIFT, VK_T);
        if (ok)
        {
            StatusText.Text = "Готово. Горячая клавиша: Ctrl+Shift+T";
        }
        else
        {
            StatusText.Text = "НЕ удалось зарегистрировать Ctrl+Shift+T — " +
                              "комбинацию уже держит другое приложение.";
        }

        // Создаём трей и подключаем к общему WndProc
        _tray = new TrayIcon(_hotkeyManager.Hwnd, "WinTrans — Ctrl+Shift+T");
        _tray.OpenRequested += ShowWindow;
        _tray.ExitRequested += ExitApp;
        _hotkeyManager.TrayMessageHandler = _tray.HandleMessage;

        // Стартуем скрыто в трей
        HideWindow();
    }

    private void ExitApp()
    {
        _tray?.Dispose();
        _hotkeyManager?.Dispose();
        Microsoft.UI.Xaml.Application.Current.Exit();
    }

    private async void OnHotkeyPressedAsync()
    {
        // 1. Прячем наше окно, если оно видимо — иначе Ctrl+C уйдёт в него,
        //    а не в исходное приложение
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        bool wasVisible = Win32.IsWindowVisible(hwnd);
        if (wasVisible)
        {
            HideWindow();
            await Task.Delay(120); // дать фокусу вернуться предыдущему окну
        }

        // 2. Пробуем получить выделенный текст из активного окна
        string? selected = await ClipboardHelper.GetSelectedTextAsync();

        // 3. Показываем наше окно
        ShowWindow();

        if (!string.IsNullOrWhiteSpace(selected))
        {
            SourceBox.Text = selected;
            StatusText.Text = "Получен выделенный текст, переводим...";
            // 4. Автоматически запускаем перевод и вставку назад
            await TranslateAsync(autoPasteBack: true);
        }
        else
        {
            StatusText.Text = "Нет выделения — введи текст вручную";
        }
    }

    private void ShowWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Win32.ShowWindow(hwnd, Win32.SW_SHOW);
        Win32.SetForegroundWindow(hwnd);
        this.Activate();
    }

    private void HideWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Win32.ShowWindow(hwnd, Win32.SW_HIDE);
    }

    private string GetSelectedLanguage() =>
        (LanguageBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "English";

    private string GetSelectedStyle() =>
        (StyleBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Нейтральный";

    private async Task TranslateAsync(bool autoPasteBack)
    {
        var apiKey = ApiKeyBox.Password?.Trim() ?? "";
        var text = SourceBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(apiKey))
        {
            StatusText.Text = "Введите API-ключ Claude";
            return;
        }
        if (string.IsNullOrEmpty(text))
        {
            StatusText.Text = "Нет текста для перевода";
            return;
        }

        try
        {
            TranslateButton.IsEnabled = false;
            StatusText.Text = "Переводим...";
            ResultBox.Text = "";

            var language = GetSelectedLanguage();
            var style = GetSelectedStyle();

            var baseUrl = BaseUrlBox.Text?.Trim() ?? "";
            var translation = await _claude.TranslateAsync(apiKey, text, language, style, baseUrl);

            ResultBox.Text = translation;
            StatusText.Text = "Готово";

            if (autoPasteBack && !string.IsNullOrWhiteSpace(translation))
            {
                await ClipboardHelper.SetTextAndPasteBackAsync(translation, this);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Ошибка: " + ex.Message;
        }
        finally
        {
            TranslateButton.IsEnabled = true;
        }
    }

    // ===== UI handlers =====

    private void SaveKeyButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.Save(new AppSettings
        {
            ApiKey = ApiKeyBox.Password ?? "",
            BaseUrl = (BaseUrlBox.Text ?? "").Trim(),
            LanguageIndex = LanguageBox.SelectedIndex,
            StyleIndex = StyleBox.SelectedIndex
        });
        StatusText.Text = "Настройки сохранены";
    }

    private async void TranslateButton_Click(object sender, RoutedEventArgs e)
    {
        await TranslateAsync(autoPasteBack: false);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ResultBox.Text)) return;
        var dp = new DataPackage();
        dp.SetText(ResultBox.Text);
        Clipboard.SetContent(dp);
        StatusText.Text = "Скопировано";
    }

    private async void PasteReplaceButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ResultBox.Text)) return;
        await ClipboardHelper.SetTextAndPasteBackAsync(ResultBox.Text, this);
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideWindow();
    }
}

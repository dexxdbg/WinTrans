using System;
using System.Threading.Tasks;
using WinTrans.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace WinTrans;

public sealed partial class MainWindow : Window
{
    private HotkeyManager? _hotkeyManager;
    private readonly ClaudeApiClient _claude = new();
    private readonly SettingsStore _settings = new();

    // Ctrl+Alt+T -> MOD_CONTROL (0x0002) | MOD_ALT (0x0001), VK_T = 0x54
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint VK_T = 0x54;

    public MainWindow()
    {
        this.InitializeComponent();
        this.Title = "WinTrans";

        // Подгружаем сохранённые настройки
        var saved = _settings.Load();
        if (!string.IsNullOrEmpty(saved.ApiKey))
            ApiKeyBox.Password = saved.ApiKey;
        if (saved.LanguageIndex >= 0 && saved.LanguageIndex < LanguageBox.Items.Count)
            LanguageBox.SelectedIndex = saved.LanguageIndex;
        if (saved.StyleIndex >= 0 && saved.StyleIndex < StyleBox.Items.Count)
            StyleBox.SelectedIndex = saved.StyleIndex;

        // По умолчанию окно скрыто, его откроет хоткей
        this.Closed += (s, e) =>
        {
            _hotkeyManager?.Dispose();
        };
    }

    public void InitializeHotkey()
    {
        _hotkeyManager = new HotkeyManager(this);
        _hotkeyManager.HotkeyPressed += OnHotkeyPressedAsync;

        bool ok = _hotkeyManager.Register(MOD_CONTROL | MOD_ALT, VK_T);
        if (ok)
        {
            StatusText.Text = "Готово. Горячая клавиша: Ctrl+Alt+T";
        }
        else
        {
            StatusText.Text = "НЕ удалось зарегистрировать Ctrl+Alt+T — " +
                              "комбинацию уже держит другое приложение. " +
                              "Закрой его или поменяй хоткей в коде.";
        }

        // окно оставляем открытым при старте, чтобы пользователь видел статус
    }

    private async void OnHotkeyPressedAsync()
    {
        // 1. Пробуем получить выделенный текст: шлём Ctrl+C активному окну
        string? selected = await ClipboardHelper.GetSelectedTextAsync();

        // 2. Показываем и активируем окно
        ShowWindow();

        if (!string.IsNullOrWhiteSpace(selected))
        {
            SourceBox.Text = selected;
            // 3. Автоматически запускаем перевод
            await TranslateAsync(autoPasteBack: true);
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

            var translation = await _claude.TranslateAsync(apiKey, text, language, style);

            ResultBox.Text = translation;
            StatusText.Text = "Готово";

            if (autoPasteBack && !string.IsNullOrWhiteSpace(translation))
            {
                // Кладём в буфер и вставляем на место выделения в исходном окне
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

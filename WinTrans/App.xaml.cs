using Microsoft.UI.Xaml;

namespace WinTrans;

public partial class App : Application
{
    public static MainWindow? MainAppWindow { get; private set; }

    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += (s, e) =>
        {
            // чтобы приложение не падало молча
            System.Diagnostics.Debug.WriteLine("UNHANDLED: " + e.Exception);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainAppWindow = new MainWindow();
        // ВАЖНО: Activate() обязателен, иначе окно не создаёт HWND
        // полностью и не получает сообщения (в т.ч. WM_HOTKEY).
        MainAppWindow.Activate();
        MainAppWindow.InitializeHotkey();
    }
}

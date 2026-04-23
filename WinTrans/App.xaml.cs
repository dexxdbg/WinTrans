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
            System.Diagnostics.Debug.WriteLine("UNHANDLED: " + e.Exception);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainAppWindow = new MainWindow();

        // Активируем и тут же прячем — иначе HWND не готов ловить WM_HOTKEY.
        // Вспышки не должно быть т.к. Activate + SW_HIDE идут подряд до первого рендера.
        MainAppWindow.Activate();
        MainAppWindow.InitializeHotkey(); // регистрирует хоткей, трей и HideWindow()
    }
}

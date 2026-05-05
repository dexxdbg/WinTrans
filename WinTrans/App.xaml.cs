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

        // activate first so the hwnd exists, then init hotkey which hides the window
        MainAppWindow.Activate();
        MainAppWindow.InitializeHotkey();
    }
}

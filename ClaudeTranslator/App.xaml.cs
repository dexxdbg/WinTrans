using Microsoft.UI.Xaml;

namespace ClaudeTranslator;

public partial class App : Application
{
    public static MainWindow? MainAppWindow { get; private set; }

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainAppWindow = new MainWindow();
        // Не показываем окно сразу — ждём хоткей. Но сам Window нужно создать,
        // чтобы жил MessagePump и работал RegisterHotKey.
        MainAppWindow.InitializeHotkey();
    }
}

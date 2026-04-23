using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace WinTrans;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Нужно для self-contained single-file сборки Windows App SDK —
        // рантайм ищет свои нативки относительно этого пути
        Environment.SetEnvironmentVariable(
            "MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
            AppContext.BaseDirectory);

        WinRT.ComWrappersSupport.InitializeComWrappers();

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }
}

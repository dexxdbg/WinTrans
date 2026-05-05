using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace WinTrans;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // needed so the sdk can find its native dlls when running as single-file exe
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

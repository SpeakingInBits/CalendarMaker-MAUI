using Microsoft.UI.Dispatching;
using Velopack;

namespace CalendarMaker_MAUI.WinUI;

/// <summary>
/// Replaces the Main that WinUI generates from App.xaml (turned off with DISABLE_XAML_GENERATED_MAIN in the .csproj),
/// so Velopack runs before anything else.
/// </summary>
public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Must run first. The installer and updater start the app with special arguments to finish their work
        // (shortcuts, uninstalling), and Run() handles those and exits. It also installs an update downloaded last time.
        VelopackApp.Build().Run();

        // The rest is what the generated Main does.
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}

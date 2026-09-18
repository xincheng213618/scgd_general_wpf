using System.Windows;
using System.Windows.Threading;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ColorVision.Themes.Tests;

public partial class ThemeResourceTests
{
    private static readonly Lazy<Dispatcher> Dispatcher = new(() =>
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            ready.Set();
            System.Windows.Threading.Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    });

    private static void Run(Action<Application, ThemeManager> action) => Dispatcher.Value.Invoke(() =>
    {
        Application app = Application.Current;
        ResourceDictionary saved = app.Resources;
        app.Resources = new ResourceDictionary();
        using var manager = new ThemeManager();
        try { action(app, manager); }
        finally { app.Resources = saved; }
    });
}

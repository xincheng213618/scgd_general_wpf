using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Threading;

namespace ProjectLUX.Tests;

internal static class WpfTestHost
{
    private static readonly Lazy<Dispatcher> HostDispatcher = new(CreateDispatcher);

    internal static void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher dispatcher = HostDispatcher.Value;
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private static Dispatcher CreateDispatcher()
    {
        Dispatcher? dispatcher = null;
        Exception? startupFailure = null;
        using ManualResetEventSlim ready = new();
        Thread thread = new(() =>
        {
            try
            {
                Application application = Application.Current ?? new Application();
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Authorization.Instance ??= new Authorization();
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex)
            {
                startupFailure = ex;
            }
            finally
            {
                ready.Set();
            }

            if (dispatcher != null)
                Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "ProjectLUX Tests WPF Host",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();

        if (startupFailure != null)
            throw new InvalidOperationException("Unable to start the WPF test host.", startupFailure);
        return dispatcher ?? throw new InvalidOperationException("The WPF test host did not create a dispatcher.");
    }
}

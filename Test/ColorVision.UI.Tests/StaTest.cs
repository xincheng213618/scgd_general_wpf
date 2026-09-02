using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

internal static class StaTest
{
    // Use a fresh STA for tests that do not need the shared Application/Dispatcher host.
    internal static void Run(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

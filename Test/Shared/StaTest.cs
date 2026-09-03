using System.Runtime.ExceptionServices;

namespace ColorVision.Testing;

internal static class StaTest
{
    // Use a fresh STA for tests that do not need the shared Application/Dispatcher host.
    internal static void Run(Action action, TimeSpan? timeout = null, string? timeoutMessage = null)
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
        }) { IsBackground = timeout.HasValue };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (timeout.HasValue)
            Assert.True(thread.Join(timeout.Value), timeoutMessage);
        else
            thread.Join();
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

#pragma warning disable CA1707
using ColorVision.Copilot;
using System.Threading;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CopilotUiDispatcherTests
{
    [Fact]
    public async Task Invoke_MarshalsToTargetDispatcherAndFallsBackAfterShutdown()
    {
        var dispatcherReady = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var uiThreadId = 0;
        var uiThread = new Thread(() =>
        {
            uiThreadId = Environment.CurrentManagedThreadId;
            dispatcherReady.TrySetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        var dispatcher = await dispatcherReady.Task.WaitAsync(TimeSpan.FromSeconds(5));

        try
        {
            var callbackThreadId = await Task.Run(() => CopilotUiDispatcher.Invoke(
                dispatcher,
                () => Environment.CurrentManagedThreadId,
                fallback: -1)).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(uiThreadId, callbackThreadId);
        }
        finally
        {
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Assert.True(uiThread.Join(TimeSpan.FromSeconds(5)));
        }

        var executed = false;
        var fallbackResult = CopilotUiDispatcher.Invoke(dispatcher, () =>
        {
            executed = true;
            return 42;
        }, fallback: -1);
        Assert.Equal(-1, fallbackResult);
        Assert.False(executed);
    }

    [Fact]
    public void Invoke_WithoutDispatcherExecutesInline()
    {
        var callingThreadId = Environment.CurrentManagedThreadId;

        var callbackThreadId = CopilotUiDispatcher.Invoke(
            dispatcher: null,
            () => Environment.CurrentManagedThreadId,
            fallback: -1);

        Assert.Equal(callingThreadId, callbackThreadId);
    }
}

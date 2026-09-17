using ColorVision.Engine.Services.RC;

namespace ColorVision.UI.Tests;

public class RCConnectionWaiterTests
{
    [Fact]
    public async Task WaitAsync_CompletesWhenConnectionStateChanges()
    {
        bool connected = false;
        EventHandler? stateChanged = null;

        Task<bool> wait = RCConnectionWaiter.WaitAsync(
            () => connected,
            handler => stateChanged += handler,
            handler => stateChanged -= handler,
            TimeSpan.FromSeconds(30));

        Assert.NotNull(stateChanged);
        connected = true;
        stateChanged?.Invoke(this, EventArgs.Empty);

        Assert.True(await wait);
        Assert.Null(stateChanged);
    }

    [Fact]
    public async Task WaitAsync_CancellationStopsWaitingAndUnsubscribes()
    {
        bool connected = false;
        EventHandler? stateChanged = null;
        using CancellationTokenSource cancellation = new();

        Task<bool> wait = RCConnectionWaiter.WaitAsync(
            () => connected,
            handler => stateChanged += handler,
            handler => stateChanged -= handler,
            TimeSpan.FromSeconds(30),
            cancellation.Token);

        Assert.NotNull(stateChanged);
        cancellation.Cancel();

        Assert.False(await wait);
        Assert.Null(stateChanged);
    }

    [Fact]
    public async Task WaitAsync_WhenAlreadyConnectedDoesNotSubscribe()
    {
        int subscriptions = 0;

        bool connected = await RCConnectionWaiter.WaitAsync(
            () => true,
            _ => subscriptions++,
            _ => subscriptions--,
            TimeSpan.FromSeconds(30));

        Assert.True(connected);
        Assert.Equal(0, subscriptions);
    }
}

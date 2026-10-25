namespace ColorVision.Copilot.Tests;

public sealed class CopilotToolDispatchWindowTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void ClosingBeforeEntryPreventsTheToolBodyFromStarting()
    {
        var window = new CopilotToolDispatchWindow();

        Assert.True(window.TryClose());

        Assert.False(window.TryEnter());
        Assert.False(window.HasEntered);
    }

    [Fact]
    public void EntryBeforeClosingRetainsTheUnknownOutcomeBoundary()
    {
        var window = new CopilotToolDispatchWindow();

        Assert.True(window.TryEnter());

        Assert.False(window.TryClose());
        Assert.True(window.HasEntered);
    }

    [Fact]
    public async Task ConcurrentEntryAndClosingCommitExactlyOneOutcome()
    {
        var window = new CopilotToolDispatchWindow();
        var release = NewSignal();
        var entryReady = NewSignal();
        var closeReady = NewSignal();
        var entry = Task.Run(async () =>
        {
            entryReady.TrySetResult();
            await release.Task;
            return window.TryEnter();
        });
        var closing = Task.Run(async () =>
        {
            closeReady.TrySetResult();
            await release.Task;
            return window.TryClose();
        });
        await Task.WhenAll(entryReady.Task, closeReady.Task).WaitAsync(TestTimeout);

        release.TrySetResult();
        var outcomes = await Task.WhenAll(entry, closing).WaitAsync(TestTimeout);

        Assert.NotEqual(outcomes[0], outcomes[1]);
        Assert.Equal(outcomes[0], window.HasEntered);
        Assert.False(window.TryEnter());
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

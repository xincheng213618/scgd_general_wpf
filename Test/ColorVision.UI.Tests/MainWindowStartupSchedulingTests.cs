using ColorVision.Startup;
using ColorVision.Update;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class MainWindowStartupSchedulingTests
{
    [Fact]
    public async Task PendingOptionalUpdateCannotDelayRequiredFileOpening()
    {
        var updateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUpdate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool opened = false;
        IMainWindowInitialized[] initializers =
        [
            new PendingUpdate(async () => { updateStarted.SetResult(); await releaseUpdate.Task; }),
            new OpenFile(() => opened = true),
        ];
        var results = await WpfTestHost.Invoke(() => MainWindowInitializerRunner.RunRequiredAsync(initializers));
        Assert.True(opened);
        Assert.Single(results);
        Assert.False(updateStarted.Task.IsCompleted);
        Task background = WpfTestHost.Invoke(() => MainWindowInitializerRunner.RunBackgroundAsync(initializers));
        try { await updateStarted.Task.WaitAsync(TimeSpan.FromSeconds(10)); Assert.False(background.IsCompleted); }
        finally { releaseUpdate.TrySetResult(); }
        await background.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class PendingUpdate(Func<Task> initialize) : AutoUpdateService
    {
        public override Task Initialize() => initialize();
    }
    private sealed class OpenFile(Action open) : MainWindowInitializedBase
    {
        public override int Order { get; set; } = 100;
        public override Task Initialize() { open(); return Task.CompletedTask; }
    }
}

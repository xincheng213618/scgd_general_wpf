using ColorVision.Copilot;
using System.Diagnostics;

namespace ColorVision.UI.Tests;

public sealed class CopilotChatStateSaveSchedulerTests
{
    [Fact]
    public async Task ContinuousChangesCannotStarveStatePersistence()
    {
        var firstSave = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();
        using var scheduler = new CopilotChatStateSaveScheduler(
            _ =>
            {
                firstSave.TrySetResult(stopwatch.Elapsed);
                return Task.CompletedTask;
            },
            debounceDelay: TimeSpan.FromMilliseconds(250),
            maximumDebounceDelay: TimeSpan.FromMilliseconds(400));

        var producer = Task.Run(async () =>
        {
            var duration = Stopwatch.StartNew();
            while (duration.Elapsed < TimeSpan.FromMilliseconds(1_200))
            {
                scheduler.RequestSave();
                await Task.Delay(20);
            }
        });

        var saveElapsed = await firstSave.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(producer.IsCompleted);
        Assert.True(
            saveElapsed < TimeSpan.FromMilliseconds(900),
            $"The first recovery point took {saveElapsed.TotalMilliseconds:N0} ms.");

        await producer;
        await scheduler.FlushAsync();
    }

    [Fact]
    public async Task QuietChangesStillCoalesceIntoOneSave()
    {
        var saveCount = 0;
        var firstSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scheduler = new CopilotChatStateSaveScheduler(
            _ =>
            {
                Interlocked.Increment(ref saveCount);
                firstSave.TrySetResult();
                return Task.CompletedTask;
            },
            debounceDelay: TimeSpan.FromMilliseconds(80),
            maximumDebounceDelay: TimeSpan.FromMilliseconds(500));

        scheduler.RequestSave();
        await Task.Delay(20);
        scheduler.RequestSave();
        await Task.Delay(20);
        scheduler.RequestSave();

        await firstSave.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Task.Delay(160);

        Assert.Equal(1, Volatile.Read(ref saveCount));
    }

    [Fact]
    public async Task ImmediateSaveBypassesBothDebounceLimits()
    {
        var saved = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();
        using var scheduler = new CopilotChatStateSaveScheduler(
            _ =>
            {
                saved.TrySetResult(stopwatch.Elapsed);
                return Task.CompletedTask;
            },
            debounceDelay: TimeSpan.FromSeconds(2),
            maximumDebounceDelay: TimeSpan.FromSeconds(3));

        scheduler.RequestSave(immediate: true);

        var saveElapsed = await saved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(
            saveElapsed < TimeSpan.FromMilliseconds(500),
            $"The immediate save took {saveElapsed.TotalMilliseconds:N0} ms.");
    }
}

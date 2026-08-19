using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatStateSaveSchedulerTests
{
    [Fact]
    public async Task FlushAfterDisposeDoesNotReportPersistenceSuccess()
    {
        var scheduler = new CopilotChatStateSaveScheduler(
            _ => Task.CompletedTask,
            debounceDelay: TimeSpan.Zero);
        scheduler.Dispose();

        var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
            () => scheduler.FlushAsync());

        Assert.Equal(nameof(CopilotChatStateSaveScheduler), exception.ObjectName);
    }

    [Fact]
    public async Task DisposingWhileFlushIsPendingDoesNotReportPersistenceSuccess()
    {
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduler = new CopilotChatStateSaveScheduler(
            async _ =>
            {
                saveStarted.TrySetResult();
                await releaseSave.Task.ConfigureAwait(false);
            },
            debounceDelay: TimeSpan.Zero);

        try
        {
            scheduler.RequestSave(immediate: true);
            var flush = scheduler.FlushAsync();
            await saveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            scheduler.Dispose();

            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
                () => flush.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(nameof(CopilotChatStateSaveScheduler), exception.ObjectName);
        }
        finally
        {
            scheduler.Dispose();
            releaseSave.TrySetResult();
        }
    }
}

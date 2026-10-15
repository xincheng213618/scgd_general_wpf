using ColorVision.Engine.Services.Devices.Camera.Local;

namespace ColorVision.UI.Tests;

public class CameraPreviewParameterQueueTests
{
    [Fact]
    public async Task BlockedCameraKeepsDispatcherAvailableAndAppliesOnlyLatestPendingValues()
    {
        using ManualResetEventSlim release = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<(bool Exposure, float Value)> applied = [];
        CameraPreviewParameterQueue? queue = null;
        int workerThread = 0;
        using var updates = queue = new CameraPreviewParameterQueue(change =>
        {
            if (change.Value == 1)
            {
                workerThread = Environment.CurrentManagedThreadId;
                started.SetResult();
                release.Wait();
            }
            if (queue!.IsCurrent(change)) applied.Add((change.Exposure, change.Value));
        }, ex => throw new InvalidOperationException("Unexpected setting failure", ex));
        int dispatcherThread = WpfTestHost.Invoke(() =>
        {
            updates.Enqueue(true, 1);
            return Environment.CurrentManagedThreadId;
        });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                updates.Enqueue(true, 2);
                updates.Enqueue(false, 3);
                updates.Enqueue(true, 4);
                updates.Enqueue(false, 5);
            });
        }
        finally { release.Set(); }
        await updates.WhenIdle;
        Assert.NotEqual(dispatcherThread, workerThread);
        Assert.Equal([(true, 4f), (false, 5f)], applied);
    }

    [Fact]
    public async Task StoppedOrDisposedPreviewCannotApplySettingsToTheNextSession()
    {
        using ManualResetEventSlim release = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<float> applied = [];
        CameraPreviewParameterQueue? queue = null;
        using var updates = queue = new CameraPreviewParameterQueue(change =>
        {
            if (change.Value == 1) { started.SetResult(); release.Wait(); }
            if (queue!.IsCurrent(change)) applied.Add(change.Value);
        }, ex => throw new InvalidOperationException("Unexpected setting failure", ex));
        updates.Enqueue(true, 1);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            updates.Enqueue(false, 2);
            updates.Clear();
            updates.Enqueue(true, 3);
        }
        finally { release.Set(); }
        await updates.WhenIdle;
        Assert.Equal([3f], applied);
        updates.Dispose();
        updates.Enqueue(true, 4);
        await updates.WhenIdle;
        Assert.Equal([3f], applied);
    }
}

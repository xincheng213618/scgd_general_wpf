using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.PhyCameras;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CalibrationUploadRunnerTests
{
    [Fact]
    public async Task UploadCommandIsDisabledWhileConcurrentAttemptIsRejectedBeforeSideEffects()
    {
        var runner = new CalibrationUploadRunner();
        var uploadCommand = new RelayCommand(_ => { }, _ => !runner.IsRunning);
        using var firstUploadEntered = new Barrier(2);
        var releaseFirstUpload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int fileEffects = 0;
        int databaseEffects = 0;
        int uiEffects = 0;
        int activeUploads = 0;
        int maxConcurrentUploads = 0;

        Task<bool> first = Task.Run(() => runner.TryRunAsync(async () =>
        {
            Interlocked.Increment(ref fileEffects);
            Interlocked.Increment(ref databaseEffects);
            Interlocked.Increment(ref uiEffects);
            int active = Interlocked.Increment(ref activeUploads);
            UpdateMaximum(ref maxConcurrentUploads, active);
            firstUploadEntered.SignalAndWait();
            await releaseFirstUpload.Task;
            Interlocked.Decrement(ref activeUploads);
        }));

        Assert.True(firstUploadEntered.SignalAndWait(TimeSpan.FromSeconds(5)));
        Assert.False(uploadCommand.CanExecute(null));

        bool second = await runner.TryRunAsync(() =>
        {
            Interlocked.Increment(ref fileEffects);
            Interlocked.Increment(ref databaseEffects);
            Interlocked.Increment(ref uiEffects);
            int active = Interlocked.Increment(ref activeUploads);
            UpdateMaximum(ref maxConcurrentUploads, active);
            Interlocked.Decrement(ref activeUploads);
            return Task.CompletedTask;
        });

        Assert.False(second);
        Assert.Equal(1, fileEffects);
        Assert.Equal(1, databaseEffects);
        Assert.Equal(1, uiEffects);
        Assert.Equal(1, maxConcurrentUploads);

        releaseFirstUpload.SetResult();
        Assert.True(await first);
        Assert.False(runner.IsRunning);
        Assert.True(uploadCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedUploadReleasesGateForRetry()
    {
        var runner = new CalibrationUploadRunner();
        var uploadCommand = new RelayCommand(_ => { }, _ => !runner.IsRunning);
        var observedRunningStates = new List<bool>();
        runner.RunningStateChanged += (_, _) => observedRunningStates.Add(runner.IsRunning);

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.TryRunAsync(
            () => Task.FromException(new InvalidOperationException("upload failed"))));

        Assert.Equal([true, false], observedRunningStates);
        Assert.False(runner.IsRunning);
        Assert.True(uploadCommand.CanExecute(null));
        Assert.True(await runner.TryRunAsync(() => Task.CompletedTask));
        Assert.False(runner.IsRunning);
        Assert.True(uploadCommand.CanExecute(null));
    }

    [Fact]
    public async Task UploadCommandStateNotificationIsRaisedOnUiThread()
    {
        int uiThreadId = WpfTestHost.Invoke(() => Environment.CurrentManagedThreadId);
        var notification = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        await Task.Run(() => PhyCamera.RaiseCanExecuteChangedOnUiThread(
            () => notification.TrySetResult(Environment.CurrentManagedThreadId)));
        int notificationThreadId = await notification.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(uiThreadId, notificationThreadId);
    }

    [Fact]
    public async Task DifferentCameraRunnersDoNotSerializeEachOther()
    {
        var firstCameraRunner = new CalibrationUploadRunner();
        var secondCameraRunner = new CalibrationUploadRunner();
        using var bothUploadsEntered = new Barrier(3);

        Task<bool> first = Task.Run(() => firstCameraRunner.TryRunAsync(() => WaitAtBarrierAsync(bothUploadsEntered)));
        Task<bool> second = Task.Run(() => secondCameraRunner.TryRunAsync(() => WaitAtBarrierAsync(bothUploadsEntered)));

        Assert.True(bothUploadsEntered.SignalAndWait(TimeSpan.FromSeconds(5)));
        Assert.True(await first);
        Assert.True(await second);
    }

    private static Task WaitAtBarrierAsync(Barrier barrier)
    {
        barrier.SignalAndWait();
        return Task.CompletedTask;
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        int current;
        do
        {
            current = Volatile.Read(ref maximum);
            if (candidate <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref maximum, candidate, current) != current);
    }
}

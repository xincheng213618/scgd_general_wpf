using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms;
using ColorVision.Themes.Controls.Uploads;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class ThirdPartyAlgorithmUploadLifecycleTests
{
    [Fact]
    public async Task ProductionBoundaryAwaitsUploadCompletion()
    {
        var uploadEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUpload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? observedFailure = null;

        Task execution = DeviceThirdPartyAlgorithms.RunUploadAsync(async () =>
        {
            uploadEntered.SetResult();
            await releaseUpload.Task;
        }, ex => observedFailure = ex);

        await uploadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(execution.IsCompleted);

        releaseUpload.SetResult();
        await execution;

        Assert.Null(observedFailure);
    }

    [Fact]
    public async Task ProductionBoundaryObservesUploadFailure()
    {
        var expected = new InvalidOperationException("upload failed");
        Exception? observedFailure = null;

        await DeviceThirdPartyAlgorithms.RunUploadAsync(
            () => Task.FromException(expected),
            ex => observedFailure = ex);

        Assert.Same(expected, observedFailure);
    }

    [Fact]
    public void UploadCoreIsTaskBased()
    {
        MethodInfo uploadAsync = typeof(DeviceThirdPartyAlgorithms)
            .GetMethod(nameof(DeviceThirdPartyAlgorithms.UploadPluginDataAsync))!;

        Assert.Equal(typeof(Task), uploadAsync.ReturnType);
        Assert.Null(typeof(DeviceThirdPartyAlgorithms).GetMethod("UploadPluginData"));
    }

    [Fact]
    public async Task CompletionNotificationFromWorkerRunsOnUiThread()
    {
        int uiThreadId = WpfTestHost.Invoke(() => Environment.CurrentManagedThreadId);
        var manager = new UploadMsgManager();
        var notification = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.UploadClosed += (_, _) => notification.TrySetResult(Environment.CurrentManagedThreadId);

        await Task.Run(manager.Close);
        int notificationThreadId = await notification.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(uiThreadId, notificationThreadId);
    }

    [Fact]
    public void CompletionWithoutSubscriberIsSafe()
    {
        WpfTestHost.Invoke(() =>
        {
            var manager = new UploadMsgManager();
            Assert.Null(Record.Exception(manager.Close));
        });
    }
}

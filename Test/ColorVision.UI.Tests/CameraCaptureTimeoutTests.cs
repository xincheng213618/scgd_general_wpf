using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.Camera;
using MQTTnet;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed partial class CameraResultSelectionTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(-19, false)]
    [InlineData(0, true)]
    public Task CaptureTimeoutTellsTheUserEvenWithNewerRowsFromTheSameCamera(int unrelatedResultCode, bool switchDatabase) =>
        WpfTestHost.Invoke(async () =>
        {
            var client = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
            var service = new MQTTControl(client);
            var timeoutShown = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var historyStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseHistory = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool current = true;
            int directReads = 0;
            var endpoint = new MySqlConnectedEndpoint(_ => Volatile.Read(ref current), () =>
            {
                Interlocked.Increment(ref directReads);
                throw new InvalidOperationException("A timeout does not look up a capture result by its ID.");
            });
            using var fixture = new ResultViewFixture(service, readHistory: (database, _, _) =>
            {
                Assert.Same(endpoint, database);
                historyStarted.TrySetResult(timeoutShown.Task.IsCompletedSuccessfully);
                releaseHistory.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
                return [new MeasureResultImgModel { Id = 2, DeviceCode = FixtureCameraCode, ResultCode = unrelatedResultCode }];
            }, endpoint: endpoint);

            var capture = new MsgRecord(completeWithoutDispatcher: true);
            int failurePrompts = 0;
            bool OwnsFeedback() => fixture.Device.OwnsResultDatabase(endpoint);
            DisplayCamera.TrackRemoteCaptureFailure(fixture.Device.DService, capture, OwnsFeedback,
                () => fixture.View.PrepareCaptureHistoryRefresh(endpoint, OwnsFeedback),
                message => timeoutShown.SetResult(message), (_, _) =>
                {
                    failurePrompts++;
                    return Task.CompletedTask;
                });

            capture.MsgRecordState = MsgRecordState.Timeout;
            try
            {
                Assert.Equal(EngineLocalization.Get("Camera_CaptureTimedOutUnconfirmed"), await timeoutShown.Task.WaitAsync(Deadline));
                Assert.True(await historyStarted.Task.WaitAsync(Deadline));
                if (switchDatabase)
                {
                    Volatile.Write(ref current, false);
                    fixture.Device.ResultDatabaseEndpoint = CreateEndpoint(_ => true);
                }
            }
            finally { releaseHistory.TrySetResult(); }

            var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Device.DService.QueuePanelFeedback(serviceOnly: true, () =>
            {
                drained.SetResult();
                return Task.CompletedTask;
            });
            await drained.Task.WaitAsync(Deadline);

            Assert.Equal(MsgRecordState.Timeout, capture.MsgRecordState);
            Assert.Equal(0, failurePrompts);
            Assert.Equal(0, directReads);
            if (switchDatabase) Assert.Empty(fixture.View.ViewResults);
            else Assert.Equal(2, Assert.Single(fixture.View.ViewResults).Id);
        });

    [Fact]
    public Task ExplicitCaptureFailureKeepsTheReplyAndRestartOwnershipCheck() => WpfTestHost.Invoke(async () =>
    {
        var client = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        using var fixture = new ResultViewFixture(new MQTTControl(client));
        var presented = new TaskCompletionSource<(string? Message, Func<bool> CanRestart)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture = new MsgRecord(completeWithoutDispatcher: true);
        bool current = true;
        int timeoutPrompts = 0;
        DisplayCamera.TrackRemoteCaptureFailure(fixture.Device.DService, capture, () => current,
            () => null, _ => timeoutPrompts++, (message, canRestart) =>
            {
                presented.SetResult((message, canRestart));
                return Task.CompletedTask;
            });

        capture.CompleteWithResponse(new MsgReturn { Code = -1, Message = "camera service error" });
        var failure = await presented.Task.WaitAsync(Deadline);
        Assert.Equal("camera service error", failure.Message);
        Assert.Equal(0, timeoutPrompts);
        Assert.True(failure.CanRestart());
        current = false;
        Assert.False(failure.CanRestart());
    });
}

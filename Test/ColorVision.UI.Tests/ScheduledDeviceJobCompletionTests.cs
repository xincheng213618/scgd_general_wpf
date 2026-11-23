using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Job;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Job;
using ColorVision.Scheduler;
using MQTTnet;
using Quartz;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public partial class ScheduledDeviceJobHelperTests
{
    private static readonly TimeSpan CaptureDeadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ScheduledWaitCompletesAfterWorkerCleanupWhileUiNotificationIsBlocked()
    {
        var transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        using var service = new MQTTServiceBase(control) { SendTopic = "scheduler/test/cmd", SubscribeTopic = "scheduler/test/status" };
        var cleanupStarted = new TaskCompletionSource<MsgRecordState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var uiBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowUi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var uiNotified = new TaskCompletionSource<MsgRecordState>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task blocker = Task.CompletedTask;
        int cleanupFinished = 0;
        try
        {
            Assert.True(await control.Connect().WaitAsync(CaptureDeadline));
            MsgRecord record = WpfTestHost.Invoke(() => service.PublishAsyncClient(new MsgSend
            {
                EventName = "GetData", CompleteWithoutDispatcher = true,
            }));
            await client.Commands.Reader.ReadAsync().AsTask().WaitAsync(CaptureDeadline);
            Assert.Equal(MsgRecordState.Sended, record.MsgRecordState);
            record.BeforeWorkerCompletion(state =>
            {
                cleanupStarted.TrySetResult(state);
                allowCleanup.Task.GetAwaiter().GetResult();
                Volatile.Write(ref cleanupFinished, 1);
            });
            record.MsgRecordStateChanged += (_, state) => uiNotified.TrySetResult(state);

            Assert.Same(record.WorkerCompletion, ScheduledDeviceJobHelper.GetTerminalCompletion(record));
            Task<MsgRecordState> waiting = ScheduledDeviceJobHelper.WaitForTerminalStateAsync(record, CaptureDeadline, CancellationToken.None);
            blocker = BlockDispatcher(uiBlocked, allowUi.Task);
            await uiBlocked.Task.WaitAsync(CaptureDeadline);

            await Task.Run(() => client.ReplyAsync(record.SubscribeTopic, new MsgReturn
            {
                MsgID = record.MsgID, EventName = "GetData", Code = 0,
            })).WaitAsync(CaptureDeadline);
            Assert.Equal(MsgRecordState.Success, await cleanupStarted.Task.WaitAsync(CaptureDeadline));
            Assert.False(waiting.IsCompleted);
            Assert.False(uiNotified.Task.IsCompleted);
            await Assert.ThrowsAsync<TimeoutException>(() => ScheduledDeviceJobHelper.WaitForTerminalStateAsync(
                record, TimeSpan.Zero, CancellationToken.None));

            allowCleanup.TrySetResult();
            Assert.Equal(MsgRecordState.Success, await waiting);
            Assert.Equal(1, Volatile.Read(ref cleanupFinished));
            Assert.False(uiNotified.Task.IsCompleted);

            allowUi.TrySetResult();
            Assert.Equal(MsgRecordState.Success, await uiNotified.Task.WaitAsync(CaptureDeadline));
        }
        finally
        {
            allowCleanup.TrySetResult();
            allowUi.TrySetResult();
            await blocker.WaitAsync(CaptureDeadline);
            await control.DisconnectAsyncClient().WaitAsync(CaptureDeadline);
        }
    }

    [Fact]
    public async Task LegacyDeadlineReturnsCommittedResultWhileCaptureCompletionWaitsForUiNotification()
    {
        MsgRecord record = WpfTestHost.Invoke(() => new MsgRecord { MsgRecordState = MsgRecordState.Sended });
        var committed = new TaskCompletionSource<MsgRecordState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var uiBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowUi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        record.TerminalStateCommitted += state => committed.TrySetResult(state);
        Task<MsgRecordState> captureCompletion = ScheduledDeviceJobHelper.GetTerminalCompletion(record);
        Task blocker = BlockDispatcher(uiBlocked, allowUi.Task);
        Task reply = Task.CompletedTask;
        try
        {
            await uiBlocked.Task.WaitAsync(CaptureDeadline);
            reply = Task.Run(() => record.CompleteWithResponse(new MsgReturn { Code = 0 }));
            Assert.Equal(MsgRecordState.Success, await committed.Task.WaitAsync(CaptureDeadline));

            Assert.Equal(MsgRecordState.Success, await ScheduledDeviceJobHelper.WaitForTerminalStateAsync(
                record, TimeSpan.Zero, CancellationToken.None));
            Assert.False(captureCompletion.IsCompleted);
        }
        finally
        {
            allowUi.TrySetResult();
            await Task.WhenAll(blocker, reply).WaitAsync(CaptureDeadline);
        }
        Assert.Equal(MsgRecordState.Success, await captureCompletion.WaitAsync(CaptureDeadline));
    }

    [Fact]
    public async Task FaultingLegacyReplyObserversDoNotPreventDeviceCleanupOrTheNextCapture()
    {
        var transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        using var service = new MQTTServiceBase(control) { SendTopic = "scheduler/test/cmd", SubscribeTopic = "scheduler/test/status" };
        using var cancellation = new CancellationTokenSource();
        var camera = CreateDevice<DeviceCamera, ConfigCamera>($"scheduler-test-{Guid.NewGuid():N}", "device");
        var config = new CameraCaptureJobConfig { DeviceCameraCode = camera.Code };
        var sent = Channel.CreateUnbounded<MsgRecord>();
        var propertyFaults = new ConcurrentQueue<string>();
        var stateFault = new TaskCompletionSource<MsgRecordState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource<MsgRecordState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = new CameraCaptureJob(() => [camera], _ =>
        {
            MsgRecord record = service.PublishAsyncClient(new MsgSend
            {
                EventName = "GetData", DeviceCode = camera.Code,
            });
            record.PropertyChanged += (_, change) =>
            {
                if (change.PropertyName is nameof(MsgRecord.ReciveTime) or nameof(MsgRecord.MsgRecordState))
                {
                    propertyFaults.Enqueue(change.PropertyName);
                    throw new InvalidOperationException("Faulting property observer.");
                }
            };
            record.MsgRecordStateChanged += (_, state) =>
            {
                stateFault.TrySetResult(state);
                throw new InvalidOperationException("Faulting state observer.");
            };
            record.MsgRecordStateChanged += (_, state) => cleanup.TrySetResult(state);
            sent.Writer.TryWrite(record);
            return record;
        });

        try
        {
            Assert.True(await control.Connect().WaitAsync(CaptureDeadline));
            WpfTestHost.Invoke(() => { });
            MsgRecord first = await CompleteCaptureAsync("legacy-first-job");
            Assert.True(stateFault.Task.IsCompletedSuccessfully);
            Assert.Equal(MsgRecordState.Success, await stateFault.Task);
            Assert.True(cleanup.Task.IsCompletedSuccessfully);
            Assert.Equal(MsgRecordState.Success, await cleanup.Task);
            Assert.Contains(nameof(MsgRecord.ReciveTime), propertyFaults);
            Assert.Contains(nameof(MsgRecord.MsgRecordState), propertyFaults);
            Assert.NotEqual(default, first.ReciveTime);

            MsgRecord next = await CompleteCaptureAsync("legacy-next-job");
            Assert.NotEqual(first.MsgID, next.MsgID);
        }
        finally
        {
            cancellation.Cancel();
            await control.DisconnectAsyncClient().WaitAsync(CaptureDeadline);
        }

        async Task<MsgRecord> CompleteCaptureAsync(string key)
        {
            Task execution = job.Execute(CreateJobContext(job, key, config), cancellation.Token).AsTask();
            MsgRecord record = await sent.Reader.ReadAsync().AsTask().WaitAsync(CaptureDeadline);
            await client.Commands.Reader.ReadAsync().AsTask().WaitAsync(CaptureDeadline);
            Assert.Null(record.WorkerCompletion);
            Task<MsgRecordState> completion = ScheduledDeviceJobHelper.GetTerminalCompletion(record);
            Assert.False(completion.IsCompleted);
            await client.ReplyAsync(record.SubscribeTopic, new MsgReturn
            {
                MsgID = record.MsgID, EventName = "GetData", DeviceCode = camera.Code, Code = 0,
            }).WaitAsync(CaptureDeadline);
            Assert.Equal(MsgRecordState.Success, await completion.WaitAsync(CaptureDeadline));
            await execution.WaitAsync(CaptureDeadline);
            return record;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ScheduledCaptureRemainsExclusiveUntilTheDeviceRecordFinishes(bool spectrum, bool cancelWait) => spectrum
        ? VerifyCaptureAdmissionAsync<DeviceSpectrum, ConfigSpectrum>((devices, capture) => new SpectrumGetDataJob(devices, capture),
            code => new SpectrumGetDataJobConfig { DeviceSpectrumCode = code }, cancelWait)
        : VerifyCaptureAdmissionAsync<DeviceCamera, ConfigCamera>((devices, capture) => new CameraCaptureJob(devices, capture),
            code => new CameraCaptureJobConfig { DeviceCameraCode = code }, cancelWait);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousCaptureFailureAndCompletionReleaseTheDevice(bool throws)
    {
        WpfTestHost.Invoke(() => { });
        var camera = CreateDevice<DeviceCamera, ConfigCamera>($"scheduler-test-{Guid.NewGuid():N}", "device");
        IJobConfig config = new CameraCaptureJobConfig { DeviceCameraCode = camera.Code };
        var fault = new InvalidOperationException("Synthetic send failure");
        var failure = new CameraCaptureJob(() => [camera], _ => throws ? throw fault : null);
        var success = new CameraCaptureJob(() => [camera], _ => new MsgRecord { MsgRecordState = MsgRecordState.Success });
        Task failedExecution = Run(failure, "failed-capture");
        if (throws) Assert.Same(fault, await Assert.ThrowsAsync<InvalidOperationException>(() => failedExecution));
        else await Assert.ThrowsAsync<JobExecutionException>(() => failedExecution);

        await Run(success, "synchronous-capture");
        await Run(success, "capture-after-synchronous-success");

        Task Run(IJob job, string key) => job.Execute(CreateJobContext(job, key, config)).AsTask().WaitAsync(CaptureDeadline);
    }

    private static async Task VerifyCaptureAdmissionAsync<TDevice, TConfig>(
        Func<Func<IEnumerable<TDevice>>, Func<TDevice, MsgRecord?>, IJob> createJob, Func<string, IJobConfig> config, bool cancelWait)
        where TDevice : DeviceService<TConfig>
        where TConfig : DeviceServiceConfig, new()
    {
        WpfTestHost.Invoke(() => { });
        string code = $"scheduler-test-{Guid.NewGuid():N}";
        TDevice original = CreateDevice<TDevice, TConfig>(code, "original");
        TDevice replacement = CreateDevice<TDevice, TConfig>(code, "reloaded");
        TDevice independent = CreateDevice<TDevice, TConfig>(code + "-other", "independent");
        TDevice current = original;
        var firstDispatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sentTo = new ConcurrentQueue<TDevice>();
        var pending = new MsgRecord(completeWithoutDispatcher: true) { MsgRecordState = MsgRecordState.Sended };
        using var cancellation = new CancellationTokenSource();
        try
        {
            Task first = Run("first-job", code, timeout: cancelWait ? 0 : 1, cancellationToken: cancellation.Token);
            await firstDispatch.Task.WaitAsync(CaptureDeadline);
            if (cancelWait)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            }
            else
            {
                var timeout = await Assert.ThrowsAsync<JobExecutionException>(() => first);
                Assert.IsType<TimeoutException>(timeout.InnerException);
            }

            Assert.Equal(MsgRecordState.Sended, pending.MsgRecordState);
            await AssertBusy("second-job");
            current = replacement;
            await AssertBusy("job-after-inventory-reload");
            Assert.Same(original, Assert.Single(sentTo));

            await Run("independent-job", independent.Code);
            Assert.Equal(MsgRecordState.Sended, pending.MsgRecordState);
            Assert.Equal(new[] { original, independent }, sentTo.ToArray());

            pending.CompleteWithResponse(new MsgReturn { Code = 0 });
            Assert.Equal(MsgRecordState.Success, await pending.WorkerCompletion!.WaitAsync(CaptureDeadline));
            await Run("retry-after-device-reply", code);
            Assert.Equal(new[] { original, independent, replacement }, sentTo.ToArray());
        }
        finally
        {
            cancellation.Cancel();
            if (!MsgRecord.IsTerminal(pending.MsgRecordState)) pending.MsgRecordState = MsgRecordState.Fail;
        }

        async Task AssertBusy(string key)
        {
            var busy = await Assert.ThrowsAsync<JobExecutionException>(() => Run(key, code, timeout: 1));
            Assert.Equal(EngineLocalization.Get("Scheduler_DeviceCaptureInProgress"), busy.Message);
        }

        Task Run(string key, string selectedCode, int timeout = 0, CancellationToken cancellationToken = default)
        {
            IJob job = createJob(() => [current, independent], device =>
            {
                sentTo.Enqueue(device);
                if (!ReferenceEquals(device, original)) return new MsgRecord { MsgRecordState = MsgRecordState.Success };
                firstDispatch.TrySetResult();
                return pending;
            });
            IJobExecutionContext context = CreateJobContext(job, key, config(selectedCode), timeout);
            return job.Execute(context, cancellationToken).AsTask().WaitAsync(CaptureDeadline);
        }
    }

    private static Task BlockDispatcher(TaskCompletionSource blocked, Task allowUi)
    {
        Dispatcher dispatcher = WpfTestHost.Invoke(() => Application.Current.Dispatcher);
        return dispatcher.InvokeAsync(() =>
        {
            blocked.TrySetResult();
            allowUi.GetAwaiter().GetResult();
        }, DispatcherPriority.Send).Task;
    }
}

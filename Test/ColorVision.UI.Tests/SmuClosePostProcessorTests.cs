using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.FlowProcessing.PostProcess.Smu;
using MQTTnet;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class SmuClosePostProcessorTests
{
    [Fact]
    public Task WaitsForBothRepliesAndClosesOnlyTheChannelSelectedAtStart() => WpfTestHost.Invoke(async () =>
    {
        await using var fixture = await Fixture.CreateAsync();
        DeviceSMU device = fixture.AddDevice("meter", SMUChannelType.A);
        Task<bool> close = fixture.StartClose();

        var first = await fixture.ReadCommandAsync();
        AssertClose(first, device, SMUChannelType.A);
        Assert.False(close.IsCompleted);
        await fixture.ReplyAsync(device, first.Request, code: 102);
        Assert.False(close.IsCompleted);
        Assert.False(fixture.Transport.Commands.Reader.TryRead(out _));
        AssertReadings(device, 1, 2, 3, 4);

        device.DisplayConfig.Channel = SMUChannelType.B;
        await fixture.ReplyAsync(device, first.Request);
        var second = await fixture.ReadCommandAsync();
        AssertClose(second, device, SMUChannelType.A);
        Assert.NotEqual(first.Request.MsgID, second.Request.MsgID);
        Assert.False(close.IsCompleted);
        AssertReadings(device, 1, 2, 3, 4);

        await fixture.ReplyAsync(device, second.Request);
        Assert.True(await close.WaitAsync(fixture.TimeoutToken));
        AssertReadings(device, null, null, 3, 4);
        Assert.Equal(3d, device.DisplayConfig.V);
        Assert.Equal(4d, device.DisplayConfig.I);
        Assert.False(fixture.Transport.Commands.Reader.TryRead(out _));
    });

    [Fact]
    public Task FailureStopsThatDeviceAndTheRunnerStillWaitsForOtherDevices() => WpfTestHost.Invoke(async () =>
    {
        await using var fixture = await Fixture.CreateAsync();
        DeviceSMU failed = fixture.AddDevice("failed", SMUChannelType.A);
        DeviceSMU succeeded = fixture.AddDevice("healthy", SMUChannelType.B);
        var meta = new PostProcessMeta
        {
            Name = "Close source meters",
            TemplateName = "test flow",
            FailurePolicy = PostProcessFailurePolicy.Required,
            PostProcessor = fixture.CreateProcessor(),
        };
        Task<IReadOnlyList<PostProcessExecutionResult>> operation = PostProcessExecutionRunner.ExecuteAsync([meta], CreateContext());

        int failedRequests = 0, successfulRequests = 0;
        for (int i = 0; i < 3; i++)
        {
            var command = await fixture.ReadCommandAsync();
            if (command.Request.DeviceCode == failed.Code)
            {
                AssertClose(command, failed, SMUChannelType.A);
                Assert.Equal(1, ++failedRequests);
                await fixture.ReplyAsync(failed, command.Request, code: -1);
            }
            else
            {
                AssertClose(command, succeeded, SMUChannelType.B);
                successfulRequests++;
                Assert.False(operation.IsCompleted);
                await fixture.ReplyAsync(succeeded, command.Request);
            }
        }

        PostProcessExecutionResult result = Assert.Single(await operation.WaitAsync(fixture.TimeoutToken));
        Assert.Equal(PostProcessExecutionStatus.ThrewException, result.Status);
        Assert.Equal(1, failedRequests);
        Assert.Equal(2, successfulRequests);
        AssertReadings(failed, 1, 2, 3, 4);
        AssertReadings(succeeded, 1, 2, null, null);
        Assert.False(fixture.Transport.Commands.Reader.TryRead(out _));
    });

    [Fact]
    public Task AChangedMqttRoutePreventsASecondCloseAndPreservesReadings() => WpfTestHost.Invoke(async () =>
    {
        await using var fixture = await Fixture.CreateAsync();
        DeviceSMU device = fixture.AddDevice("meter", SMUChannelType.B);
        Task<bool> close = fixture.StartClose();

        var first = await fixture.ReadCommandAsync();
        AssertClose(first, device, SMUChannelType.B);
        device.Config.SendTopic += "/replacement";
        await fixture.ReplyAsync(device, first.Request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => close.WaitAsync(fixture.TimeoutToken));
        Assert.False(fixture.Transport.Commands.Reader.TryRead(out _));
        AssertReadings(device, 1, 2, 3, 4);
    });

    [Fact]
    public Task NoReplyTimesOutWithoutSendingAgainOrClearingReadings() => WpfTestHost.Invoke(async () =>
    {
        await using var fixture = await Fixture.CreateAsync();
        DeviceSMU device = fixture.AddDevice("timeout-meter", SMUChannelType.B);
        Task<bool> close = fixture.StartClose(TimeSpan.FromSeconds(1));

        var first = await fixture.ReadCommandAsync();
        AssertClose(first, device, SMUChannelType.B);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => close.WaitAsync(fixture.TimeoutToken));
        Assert.IsType<TimeoutException>(failure.InnerException);
        Assert.Contains(device.Code, failure.Message);
        Assert.Contains("关闭输出超时", failure.Message);
        Assert.False(fixture.Transport.Commands.Reader.TryRead(out _));
        AssertReadings(device, 1, 2, 3, 4);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task QueuedPublishChecksTheRouteAndStartsUnderSourceAdmissionLock(bool changeRoute) => WpfTestHost.Invoke(async () =>
    {
        await using var fixture = await Fixture.CreateAsync();
        DeviceSMU device = fixture.AddDevice("queued-meter", SMUChannelType.A);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Transport.PublishGate = gate.Task;
        int securedWrites = 0;
        fixture.Transport.PublishStarted = request =>
        {
            if (request.EventName == "CloseOutput" && Monitor.IsEntered(device.SmuBackend.Sync))
                Interlocked.Increment(ref securedWrites);
        };
        Task<bool> occupying = fixture.OccupyPublishGateAsync();
        try
        {
            Assert.Equal("GateProbe", (await fixture.ReadCommandAsync()).Request.EventName);
            Task<bool> close = fixture.StartClose();
            Assert.False(close.IsCompleted);

            if (changeRoute) device.Config.SendTopic += "/replacement";
            gate.SetResult();
            Assert.True(await occupying.WaitAsync(fixture.TimeoutToken));
            if (changeRoute)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => close.WaitAsync(fixture.TimeoutToken));
                Assert.Equal(0, Volatile.Read(ref securedWrites));
                AssertReadings(device, 1, 2, 3, 4);
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    var command = await fixture.ReadCommandAsync();
                    AssertClose(command, device, SMUChannelType.A);
                    await fixture.ReplyAsync(device, command.Request);
                }
                Assert.True(await close.WaitAsync(fixture.TimeoutToken));
                Assert.Equal(2, Volatile.Read(ref securedWrites));
            }
            Assert.False(fixture.Transport.Commands.Reader.TryRead(out _));
        }
        finally { gate.TrySetResult(); }
    });

    [Fact]
    public Task PanelScanClosesTheOriginalChannelOnceAfterBothReplies() => WpfTestHost.Invoke(async () =>
    {
        await using var fixture = await Fixture.CreateAsync();
        DeviceSMU device = fixture.AddDevice("panel", SMUChannelType.A);
        var delay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (record, completion) = device.DService.ScanFromPanel(NewScan(), SMUChannelType.A, () =>
        {
            waiting.SetResult();
            return delay.Task;
        });
        Assert.NotNull(record);
        Assert.NotNull(completion);

        var scan = await fixture.ReadCommandAsync();
        Assert.Equal("Scan", scan.Request.EventName);
        var scanParameters = Assert.IsType<JObject>(Assert.IsType<JObject>(scan.Request.Params)["DeviceParam"]);
        Assert.Equal((int)SMUChannelType.A, scanParameters.Value<int>("Channel"));
        await fixture.ReplyAsync(device, scan.Request, code: 102);
        Assert.False(waiting.Task.IsCompleted);

        await fixture.ReplyAsync(device, scan.Request);
        await waiting.Task.WaitAsync(fixture.TimeoutToken);
        Assert.False(fixture.Transport.Commands.Reader.TryRead(out _));
        device.DisplayConfig.Channel = SMUChannelType.B;
        delay.SetResult();

        var close = await fixture.ReadCommandAsync();
        AssertClose(close, device, SMUChannelType.A);
        Assert.False(completion!.IsCompleted);
        AssertReadings(device, 1, 2, 3, 4);
        await fixture.ReplyAsync(device, close.Request);
        await completion.WaitAsync(fixture.TimeoutToken);

        AssertReadings(device, null, null, 3, 4);
        Assert.False(fixture.Transport.Commands.Reader.TryRead(out _));
    });

    [Theory]
    [InlineData("measurement")]
    [InlineData("reload")]
    [InlineData("reconnect")]
    public Task PanelScanSkipsDelayedCloseWhenTheOriginalOperationIsReplaced(string change) => WpfTestHost.Invoke(async () =>
    {
        await using var fixture = await Fixture.CreateAsync();
        DeviceSMU device = fixture.AddDevice("panel-replaced", SMUChannelType.A);
        var delay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (_, completion) = device.DService.ScanFromPanel(NewScan(), SMUChannelType.A, () =>
        {
            waiting.SetResult();
            return delay.Task;
        });
        Assert.NotNull(completion);
        var scan = await fixture.ReadCommandAsync();
        await fixture.ReplyAsync(device, scan.Request);
        await waiting.Task.WaitAsync(fixture.TimeoutToken);

        switch (change)
        {
            case "measurement":
                MsgRecord? measurement = device.DService.GetData(true, 1, 2, SMUChannelType.A);
                Assert.NotNull(measurement);
                var command = await fixture.ReadCommandAsync();
                Assert.Equal("GetData", command.Request.EventName);
                await fixture.ReplyAsync(device, command.Request);
                Assert.Equal(MsgRecordState.Success, await measurement!.WorkerCompletion!.WaitAsync(fixture.TimeoutToken));
                break;
            case "reload":
                device.SysResourceModel = new SysResourceModel { Id = device.SysResourceModel.Id, Code = device.Code };
                break;
            case "reconnect":
                await fixture.ReconnectAsync();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change));
        }

        delay.SetResult();
        await completion!.WaitAsync(fixture.TimeoutToken);
        Assert.False(fixture.Transport.Commands.Reader.TryRead(out _));
        AssertReadings(device, 1, 2, 3, 4);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task PanelScanOrCloseRejectionLeavesTheReadingsAndReportsUnconfirmedOutput(bool failScan) => WpfTestHost.Invoke(async () =>
    {
        await using var fixture = await Fixture.CreateAsync();
        DeviceSMU device = fixture.AddDevice("panel-failure", SMUChannelType.B);
        var (_, completion) = device.DService.ScanFromPanel(NewScan(), SMUChannelType.B, static () => Task.CompletedTask);
        Assert.NotNull(completion);
        var scan = await fixture.ReadCommandAsync();
        await fixture.ReplyAsync(device, scan.Request, code: failScan ? -1 : 0);
        if (!failScan)
        {
            var close = await fixture.ReadCommandAsync();
            AssertClose(close, device, SMUChannelType.B);
            await fixture.ReplyAsync(device, close.Request, code: -1);
        }

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => completion!.WaitAsync(fixture.TimeoutToken));
        Assert.Contains(failScan ? "扫描失败" : "关闭输出失败", failure.Message);
        Assert.Contains("输出状态待确认", failure.Message);
        Assert.False(fixture.Transport.Commands.Reader.TryRead(out _));
        AssertReadings(device, 1, 2, 3, 4);
    });

    private static SMUParam NewScan() => new() { IsSourceV = true, StartMeasureVal = 0.1, StopMeasureVal = 1, LmtVal = 2, Number = 5 };

    private static void AssertClose((string Topic, MsgSend Request) command, DeviceSMU device, SMUChannelType channel)
    {
        Assert.Equal(device.Config.SendTopic, command.Topic);
        Assert.Equal(device.Code, command.Request.DeviceCode);
        Assert.Equal("CloseOutput", command.Request.EventName);
        Assert.Equal((int)channel, Assert.IsType<JObject>(command.Request.Params).Value<int>("Channel"));
    }

    private static void AssertReadings(DeviceSMU device, double? aVolts, double? aAmps, double? bVolts, double? bAmps)
    {
        Assert.Equal((aVolts, aAmps), (device.DisplayConfig.ChannelA.V, device.DisplayConfig.ChannelA.I));
        Assert.Equal((bVolts, bAmps), (device.DisplayConfig.ChannelB.V, device.DisplayConfig.ChannelB.I));
    }

    private static PostProcessContext CreateContext() => new(new PostProcessConfig()) { Batch = new MeasureBatchModel() };

    private sealed class Fixture(MQTTControl control, CfwCalibrationMqttRouteTests.CalibrationTransport transport) : IAsyncDisposable
    {
        private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        private readonly List<DeviceSMU> devices = [];

        internal CfwCalibrationMqttRouteTests.CalibrationTransport Transport { get; } = transport;
        internal CancellationToken TimeoutToken => timeout.Token;

        internal static async Task<Fixture> CreateAsync()
        {
            var transport = DispatchProxy.Create<IMqttClient, CfwCalibrationMqttRouteTests.CalibrationTransport>();
            var control = new MQTTControl(transport);
            Assert.True(await control.Connect());
            return new Fixture(control, (CfwCalibrationMqttRouteTests.CalibrationTransport)(object)transport);
        }

        internal DeviceSMU AddDevice(string code, SMUChannelType channel)
        {
            var device = (DeviceSMU)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSMU));
            device.SysResourceModel = new SysResourceModel { Id = -2 - devices.Count, Code = code };
            device.Config = new ConfigSMU
            {
                Code = code, SendTopic = $"test/smu/{code}/cmd", SubscribeTopic = $"test/smu/{code}/status",
                SerialPort = $"COM{200 + devices.Count}",
            };
            var display = new DisplaySMUConfig { UseLocalSmu = false, Channel = channel };
            display.ChannelA.V = 1;
            display.ChannelA.I = 2;
            display.ChannelB.V = 3;
            display.ChannelB.I = 4;
            typeof(DeviceSMU).GetField("<DisplayConfig>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, display);
            typeof(DeviceSMU).GetField("<SmuBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, new SmuBackendState(false));
            device.DService = new MQTTSMU(device, control, static () => [], static () => new Version(4, 0, 2, 115));
            devices.Add(device);
            return device;
        }

        internal IPostProcessor CreateProcessor(TimeSpan? replyTimeout = null) => new SmuClosePostProcessor(() => devices, TimeSpan.Zero, replyTimeout);
        internal Task<bool> StartClose(TimeSpan? replyTimeout = null) => CreateProcessor(replyTimeout).ProcessAsync(CreateContext()).AsTask();
        internal Task<bool> OccupyPublishGateAsync() => control.TryPublishOnConnectionAsync(control.MQTTClient.Options!,
            "test/smu/gate", "{\"EventName\":\"GateProbe\"}", static () => true);
        internal ValueTask<(string Topic, MsgSend Request)> ReadCommandAsync() => Transport.Commands.Reader.ReadAsync(TimeoutToken);
        internal Task ReplyAsync(DeviceSMU device, MsgSend request, int code = 0) => Transport.ReplyAsync(device.Config.SubscribeTopic, request, code);

        internal async Task ReconnectAsync()
        {
            var previous = control.MQTTClient.Options;
            await control.DisconnectAsyncClient();
            Assert.True(await control.Connect());
            Assert.NotSame(previous, control.MQTTClient.Options);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var device in devices) device.DService.Dispose();
            timeout.Dispose();
            await control.DisconnectAsyncClient();
        }
    }
}

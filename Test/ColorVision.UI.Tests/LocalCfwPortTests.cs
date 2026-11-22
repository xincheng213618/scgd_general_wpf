using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.CfwPort.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI;
using FlowEngineLib;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.IO;
using System.IO.Ports;

namespace ColorVision.UI.Tests;

// Protect serial port ownership, verified positioning, and the shared manual/Flow/spectrum connection.
public sealed class LocalCfwPortTests
{
    private static int nextPort = 5000;
    private static LocalCfwPortConnection Connection() => new("COM" + Interlocked.Increment(ref nextPort), 9600, 5000, 1, 6, 0);

    [Theory]
    [InlineData('4', 4)]
    [InlineData(';', 11)]
    public void PositionQuerySendsNowBeforeReadingTheResponse(char reply, int expected)
    {
        using var wire = new QueryWire((byte)reply);
        Assert.Equal(expected, LocalCfwPortTransport.ReadPosition(wire, 5000));
        Assert.Equal(5000, wire.ReadTimeout);
        Assert.Equal(5000, wire.WriteTimeout);
    }

    [Fact]
    public void SerialPortUsesServiceControlLineSettings()
    {
        var config = Connection() with { BaudRate = 19200 };
        using SerialPort serialPort = LocalCfwPortTransport.CreateSerialPort(config);
        Assert.Equal(config.PortName, serialPort.PortName);
        Assert.Equal(19200, serialPort.BaudRate);
        Assert.Equal(8, serialPort.DataBits);
        Assert.Equal(Parity.None, serialPort.Parity);
        Assert.Equal(StopBits.One, serialPort.StopBits);
        Assert.Equal(Handshake.None, serialPort.Handshake);
        Assert.False(serialPort.DtrEnable);
        Assert.False(serialPort.RtsEnable);
    }

    [Fact]
    public async Task OpenRetriesOnSameSerialPortAfterInitialTimeout()
    {
        var config = Connection();
        var transport = new FakeTransport { Port = 3, ReadTimesOut = true };
        await using var session = new LocalCfwPortSession(transport);
        var statuses = new List<DeviceStatusType>();
        session.StatusChanged += statuses.Add;
        transport.OnGet = () =>
        {
            Assert.Equal(DeviceStatusType.Opening, session.Status);
            Assert.Equal(1, transport.Opens);
            Assert.Equal(0, transport.Closes);
            if (transport.Reads == 2) transport.ReadTimesOut = false;
        };

        Assert.Equal(3, await session.ExecuteAsync("Open", config));
        Assert.Equal(2, transport.Reads);
        Assert.Equal(0, transport.Sets);
        Assert.Equal(new[] { DeviceStatusType.Opening, DeviceStatusType.Opened }, statuses);
    }

    [Fact]
    public async Task CancellationStopsRetriesAfterInFlightProbeReturns()
    {
        using var cancellation = new CancellationTokenSource();
        var config = Connection() with { RetryCount = 3 };
        var transport = new FakeTransport { ReadTimesOut = true, OnGet = cancellation.Cancel };
        await using var session = new LocalCfwPortSession(transport);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ExecuteAsync("Open", config, token: cancellation.Token));

        Assert.Equal(1, transport.Reads);
        Assert.Equal(1, transport.Closes);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
        LocalCfwPortSession.EnsureNotLocallyOwned(config.PortName);
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    public async Task OpenRequiresAValidPositionAndAllowsRetryAfterFailure(int response, bool timesOut)
    {
        var config = Connection() with { Timeout = 4200 };
        var transport = new FakeTransport { Port = response, ReadTimesOut = timesOut };
        await using var session = new LocalCfwPortSession(transport);
        transport.OnGet = () => Assert.Equal(DeviceStatusType.Opening, session.Status);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("Open", config));
        Assert.Contains(timesOut ? "NOW 孔位回包超时" : "NOW 返回无效孔位", error.Message);
        Assert.Equal(4200, transport.ReadTimeout);
        Assert.Equal(config.RetryCount + 1, transport.Reads);
        Assert.Equal(1, transport.Opens);
        Assert.Equal(1, transport.Closes);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
        LocalCfwPortSession.EnsureNotLocallyOwned(config.PortName);

        transport.ReadTimesOut = false;
        transport.Port = 4;
        Assert.Equal(4, await session.ExecuteAsync("Open", config));
        Assert.Equal(DeviceStatusType.Opened, session.Status);
        Assert.Equal(0, transport.Sets);
    }

    [Fact]
    public async Task SpectrumOwnedNdReusesItsConnectionAndBlocksServiceUntilDisconnect()
    {
        var native = new FakeTransport();
        await using var session = new LocalCfwPortSession(native);
        var nd = new LocalSpectrumNd(directSession: session);
        var config = new NDConfig { IsNDPort = true, IsBingNDDevice = false, SzComName = Connection().PortName, BaudRate = 19200 };
        try
        {
            await Task.Run(() =>
            {
                using (var operation = nd.Acquire(config)) Assert.Equal(2, operation.SetPort(2));
                Assert.Equal(0, native.Closes);
                using (var operation = nd.Acquire(config)) Assert.Equal(2, operation.GetPort());
                Assert.Equal(1, native.Opens);
                Assert.Equal(19200, native.Connection!.BaudRate);
                Assert.Throws<InvalidOperationException>(() => LocalCfwPortSession.EnsureNotLocallyOwned(config.SzComName));
                nd.Disconnect();
                Assert.Equal(1, native.Closes);
                LocalCfwPortSession.EnsureNotLocallyOwned(config.SzComName);
            });
        }
        finally { nd.Disconnect(); }
    }

    [Fact]
    public async Task PositionRefreshDoesNotWaitForTheMoveDelay()
    {
        var config = Connection() with { Delay = 25000 };
        var transport = new FakeTransport { Port = 3 };
        await using var session = new LocalCfwPortSession(transport);
        await session.ExecuteAsync("Open", config);
        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(3, await session.ExecuteAsync("GetPort", config, token: wait.Token));
        Assert.Equal(2, transport.Reads);
        Assert.Equal(0, transport.Sets);
    }

    [Fact]
    public async Task FailedSetDoesNotReadBackOrReportSuccess()
    {
        var native = new FakeTransport { FailSet = true };
        await using var session = new LocalCfwPortSession(native);
        var config = Connection();

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("SetPort", config, 3, autoConnect: true));

        Assert.Equal(config.RetryCount + 1, native.Sets);
        Assert.Equal(1, native.Reads);
    }

    [Theory]
    [InlineData(6, 5)]
    [InlineData(12, 11)]
    public async Task SerialSettingsAndZeroBasedPortArePreserved(int ports, int target)
    {
        var native = new FakeTransport();
        await using var session = new LocalCfwPortSession(native);
        var config = Connection() with { Ports = ports, BaudRate = 19200, Timeout = 7000 };
        Assert.Equal(target, await session.ExecuteAsync("SetPort", config, target, true));
        Assert.Equal(config, native.Connection);
        Assert.Equal(1, native.Opens);
        Assert.Equal(2, native.Reads);
        Assert.Equal(target, await session.ExecuteAsync("GetPort", config));
        await session.ExecuteAsync("Open", config);
        Assert.Equal(1, native.Opens);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.ExecuteAsync("SetPort", config, ports));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("GetPort", config with { BaudRate = 9600 }));
        await session.ExecuteAsync("Close", config);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
        Assert.Equal(1, native.Closes);
    }

    [Fact]
    public async Task SetAcknowledgementNeedsMatchingReadbackAndRetriesAreBounded()
    {
        var native = new FakeTransport { ReadOverride = 2 };
        await using var session = new LocalCfwPortSession(native);
        var config = Connection();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("SetPort", config, 4, true));
        Assert.Contains("实际孔位 2", error.Message);
        Assert.Equal(config.RetryCount + 1, native.Sets);
        Assert.Equal(config.RetryCount + 2, native.Reads);
        Assert.Equal(DeviceStatusType.Opened, session.Status);
        native.ReadOverride = null;
        Assert.Equal(4, await session.ExecuteAsync("SetPort", config, 4));
        native.ReadTimesOut = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("GetPort", config));
    }

    [Fact]
    public async Task FailedOpenReleasesEndpointButSuccessfulOpenOwnsItUntilClose()
    {
        var config = Connection();
        var native = new FakeTransport { FailOpen = true };
        await using var first = new LocalCfwPortSession(native);
        await using var second = new LocalCfwPortSession(new FakeTransport());
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.ExecuteAsync("Open", config));
        Assert.Null(first.Endpoint);
        await second.ExecuteAsync("Open", config);
        native.FailOpen = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.ExecuteAsync("Open", config));
        await second.ExecuteAsync("Close", config);
        await first.ExecuteAsync("Open", config);
        Assert.Equal(DeviceStatusType.Opened, first.Status);
    }

    [Fact]
    public async Task StatusObserverFailureCannotReleaseAnOpenFilterWheelReservation()
    {
        var config = Connection();
        var native = new FakeTransport();
        var competingNative = new FakeTransport();
        await using var session = new LocalCfwPortSession(native);
        await using var competing = new LocalCfwPortSession(competingNative);
        var observed = new List<DeviceStatusType>();
        bool failAllActiveStatuses = false;
        void FailingObserver(DeviceStatusType status)
        {
            if (status == DeviceStatusType.Opened || (failAllActiveStatuses && status != DeviceStatusType.Closed))
                throw new InvalidOperationException("simulated status observer failure");
        }

        session.StatusChanged += FailingObserver;
        session.StatusChanged += observed.Add;
        try
        {
            await session.ExecuteAsync("Open", config);
            Assert.Equal(DeviceStatusType.Opened, session.Status);
            Assert.Equal(config.PortName, session.Endpoint);
            Assert.Equal(1, native.Opens);
            Assert.Throws<InvalidOperationException>(() => LocalCfwPortSession.EnsureNotLocallyOwned(config.PortName));
            await Assert.ThrowsAsync<InvalidOperationException>(() => competing.ExecuteAsync("Open", config));
            Assert.Equal(0, competingNative.Opens);

            failAllActiveStatuses = true;
            Assert.Equal(3, await session.ExecuteAsync("SetPort", config, 3));
            await session.ExecuteAsync("Close", config);
            Assert.Equal(new[] { DeviceStatusType.Opening, DeviceStatusType.Opened, DeviceStatusType.Busy, DeviceStatusType.Opened,
                DeviceStatusType.Closing, DeviceStatusType.Closed }, observed);
            Assert.Equal(1, native.Closes);
            await competing.ExecuteAsync("Open", config);
            Assert.Equal(1, competingNative.Opens);
        }
        finally
        {
            session.StatusChanged -= FailingObserver;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndCloseWaitForBlockingSerialCommand(bool failSet)
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var native = new FakeTransport { FailSet = failSet, OnSet = () => { entered.TrySetResult(); release.Wait(); } };
        await using var session = new LocalCfwPortSession(native);
        var config = Connection() with { RetryCount = 0 };
        using var cancellation = new CancellationTokenSource();
        Task<int?> move = session.ExecuteAsync("SetPort", config, 3, true, cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            Task<int?> close = session.ExecuteAsync("Close", config);
            Assert.False(move.IsCompleted);
            Assert.False(close.IsCompleted);
            Assert.Equal(0, native.Closes);
            release.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => move);
            await close;
            Assert.Equal(1, native.Reads);
            Assert.Equal(1, native.Closes);
            Assert.Equal(DeviceStatusType.Closed, session.Status);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task CancellationDuringTheFinalReadbackTakesPrecedenceOverAReadError()
    {
        using var cancellation = new CancellationTokenSource();
        var native = new FakeTransport();
        await using var session = new LocalCfwPortSession(native);
        var config = Connection() with { RetryCount = 0 };
        await session.ExecuteAsync("Open", config);
        native.ReadTimesOut = true;
        native.OnGet = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            session.ExecuteAsync("SetPort", config, 3, autoConnect: true, token: cancellation.Token));

        Assert.Equal(1, native.Sets);
        Assert.Equal(2, native.Reads);
        Assert.Equal(DeviceStatusType.Opened, session.Status);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CanceledOpenCleansUpBeforePublishingTheConnection(bool autoConnect, bool failClose)
    {
        using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var native = new FakeTransport { FailClose = failClose, OnOpen = () => { entered.TrySetResult(); release.Wait(); } };
        await using var session = new LocalCfwPortSession(native);
        await using var competing = new LocalCfwPortSession(new FakeTransport());
        var statuses = new List<DeviceStatusType>();
        session.StatusChanged += statuses.Add;
        var config = Connection();
        Task<int?> open = autoConnect
            ? session.ExecuteAsync("SetPort", config, 3, autoConnect: true, token: cancellation.Token)
            : session.ExecuteAsync("Open", config, token: cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            Assert.False(open.IsCompleted);
            Assert.Equal(0, native.Closes);
            Assert.Throws<InvalidOperationException>(() => LocalCfwPortSession.EnsureNotLocallyOwned(config.PortName));

            release.Set();
            if (!failClose)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => open);
                Assert.Equal(DeviceStatusType.Closed, session.Status);
                Assert.Null(session.Endpoint);
                await competing.ExecuteAsync("Open", config);
            }
            else
            {
                var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => open);
                Assert.Contains("释放滤色轮失败", failure.Message);
                Assert.Equal(config.PortName, session.Endpoint);
                await Assert.ThrowsAsync<InvalidOperationException>(() => competing.ExecuteAsync("Open", config));
            }
            Assert.DoesNotContain(DeviceStatusType.Opened, statuses);
            Assert.Equal(1, native.Closes);
            Assert.Equal(0, native.Sets);
        }
        finally
        {
            release.Set();
            native.FailClose = false;
        }
    }

    [Fact]
    public async Task DisposeDrainsSerialCommandAndRejectsQueuedWork()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var native = new FakeTransport { OnSet = () => { entered.TrySetResult(); release.Wait(); } };
        var session = new LocalCfwPortSession(native);
        var config = Connection();
        Task<int?> move = session.ExecuteAsync("SetPort", config, 1, true);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Task<int?> queued = session.ExecuteAsync("SetPort", config, 2);
            Task disposal = session.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            Assert.Equal(0, native.Closes);
            release.Set();
            await move;
            await Assert.ThrowsAsync<ObjectDisposedException>(() => queued);
            await disposal;
            Assert.Equal(1, native.Sets);
            Assert.Equal(1, native.Closes);
        }
        finally { release.Set(); await session.DisposeAsync(); }
    }

    [Fact]
    public async Task FailedReleaseQuarantinesHandleAndEndpoint()
    {
        var config = Connection();
        var native = new FakeTransport { FailClose = true };
        await using var session = new LocalCfwPortSession(native);
        await using var other = new LocalCfwPortSession(new FakeTransport());
        await session.ExecuteAsync("Open", config);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("Close", config));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("Open", config));
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.ExecuteAsync("Open", config));
        await session.DisposeAsync();
        Assert.Equal(1, native.Closes);
        Assert.NotEqual(DeviceStatusType.Closed, session.Status);
    }

    [Fact]
    public async Task LegacyServiceFieldsRoundTripWithoutBlockingLocalOpen()
    {
        var config = new ConfigCfwPort { SzComName = Connection().PortName, IsCVMode = true, EnableVSPHub = true,
            VirtualPort1Name = "COM91", VirtualPort2Name = "COM92" };
        var display = new DisplayCfwPortConfig { UseLocalCfwPort = true };
        var draft = CfwPortConfigurationDraft.From(config, display);
        draft.RetryCount++;
        draft.Apply(config, display);

        var stored = JObject.FromObject(config);
        Assert.True(stored.Value<bool>(nameof(config.IsCVMode)));
        Assert.True(stored.Value<bool>(nameof(config.EnableVSPHub)));
        Assert.Equal("COM91", stored.Value<string>(nameof(config.VirtualPort1Name)));
        Assert.Equal("COM92", stored.Value<string>(nameof(config.VirtualPort2Name)));
        var transport = new FakeTransport { Port = 3 };
        await using var session = new LocalCfwPortSession(transport);
        Assert.Equal(3, await session.ExecuteAsync("Open", LocalCfwPortConnection.From(config)));
    }

    [Fact]
    public void BackendSwitchWaitsForConfirmedCloseIncludingOfflineAndPendingCommands()
    {
        var backend = new CfwPortBackendState(false);
        backend.Begin(false, "Open", "COM12");
        backend.SetPreference(true);
        Assert.False(backend.OpensLocally);
        backend.End(false);
        backend.ObserveService(DeviceStatusType.Opened);
        backend.ObserveService(DeviceStatusType.OffLine);
        Assert.Throws<InvalidOperationException>(() => backend.EnsureAvailable(true));
        Assert.False(backend.OpensLocally);
        backend.ObserveService(DeviceStatusType.Closed);
        Assert.True(backend.OpensLocally);
        backend.Begin(true, "Open", "COM12");
        backend.SetPreference(false);
        Assert.True(backend.OpensLocally);
        backend.SetLocalStatus(DeviceStatusType.Opened);
        backend.End(true);
        Assert.Equal("COM12", backend.Endpoint);
        Assert.Throws<InvalidOperationException>(() => backend.EnsureAvailable(false));
        backend.Begin(true, "Close", "COM99");
        backend.SetLocalStatus(DeviceStatusType.Closed);
        Assert.True(backend.OpensLocally);
        backend.End(true);
        Assert.False(backend.OpensLocally);
        Assert.Null(backend.Endpoint);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ConfigurationDraftKeepsLocalPreferenceOutOfServiceConfig(bool editService, bool editDisplay)
    {
        var config = new ConfigCfwPort { SzComName = "COM12", BaudRate = 9600, SN = "CAMERA-1" };
        var display = new DisplayCfwPortConfig { PortNum = 2 };
        var draft = CfwPortConfigurationDraft.From(config, display);
        var session = PropertyEditSession.Create(draft, PropertyEditorEditMode.Transactional);
        var editable = (CfwPortConfigurationDraft)session.EditableObject;
        if (editDisplay) editable.UseLocalCfwPort = true;
        if (editService)
        {
            editable.BaudRate = 19200;
            editable.SN = "SPECTRUM-1";
        }
        Assert.Equal("CAMERA-1", config.SN);
        Assert.False(display.UseLocalCfwPort);
        Assert.Equal(9600, config.BaudRate);

        session.Commit();
        if (!editService) config.BaudRate = 38400;
        if (!editDisplay) display.UseLocalCfwPort = true;
        display.PortNum = 3;
        var (serviceChanged, displayChanged) = draft.Apply(config, display);
        Assert.Equal(editService, serviceChanged);
        Assert.Equal(editDisplay, displayChanged);
        Assert.True(display.UseLocalCfwPort);
        Assert.Equal(3, display.PortNum);
        Assert.Equal(editService ? 19200 : 38400, config.BaudRate);
        Assert.Equal(editService ? "SPECTRUM-1" : "CAMERA-1", JsonConvert.DeserializeObject<ConfigCfwPort>(JsonConvert.SerializeObject(config))!.SN);
        Assert.DoesNotContain("UseLocalCfwPort", JsonConvert.SerializeObject(config));
        Assert.True(JsonConvert.DeserializeObject<DisplayCfwPortConfig>(JsonConvert.SerializeObject(display))!.UseLocalCfwPort);
    }

    [Fact]
    public async Task ManualFlowAndSpectrumShareLocalConnectionAndCalibrationReservation()
    {
        var native = new FakeTransport();
        await using var session = new LocalCfwPortSession(native);
        DeviceCfwPort? device = null;
        IConfigService? previousConfig = null;
        var managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousManager = ServiceManager.Current;
        var physicalManagerField = typeof(PhyCameraManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousPhysicalManager = physicalManagerField.GetValue(null);
        var directory = Path.Combine(Path.GetTempPath(), "cfw-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            WpfTestHost.Invoke(() =>
            {
                previousConfig = ConfigService.Instance;
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false, ConfigFilePath = Path.Combine(directory, "config.json") });
                var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
                manager.DeviceServices = [];
                managerField.SetValue(null, manager);
                var physicalManager = (PhyCameraManager)RuntimeHelpers.GetUninitializedObject(typeof(PhyCameraManager));
                physicalManager.PhyCameras = [];
                physicalManagerField.SetValue(null, physicalManager);
                device = new DeviceCfwPort(new SysResourceModel { Id = -2, Code = "CFW.Local." + Guid.NewGuid(),
                    Value = JsonConvert.SerializeObject(new ConfigCfwPort { SzComName = Connection().PortName }) }, session);
                manager.DeviceServices.Add(device);
                Assert.False(device.DisplayConfig.UseLocalCfwPort);
                device.DisplayConfig.UseLocalCfwPort = true;
                Assert.Equal(DeviceStatusType.Closed, device.DService.DeviceStatus);
                device.Config.Ports = 12;
                Assert.Equal(Enumerable.Range(0, 12), device.FilterWheelConfig.HoleMapping.Select(item => item.HoleIndex));
                device.Config.Ports = 6;
            });
            Assert.Equal(MsgRecordState.Success, (await Command(() => device!.DService.Open())).MsgRecordState);
            var moved = await Command(() => device!.DService.SetPort(4));
            Assert.IsType<LocalCfwPortRecord>(moved);
            Assert.Equal(4, ((JObject)moved.MsgReturn.Data).Value<int>("Port"));
            Assert.Equal(4, WpfTestHost.Invoke(() => device!.DisplayConfig.PortNum));
            var invalid = await Command(() => device!.DService.SetPort(6));
            Assert.Equal(MsgRecordState.Fail, invalid.MsgRecordState);
            Assert.Equal(4, WpfTestHost.Invoke(() => device!.DisplayConfig.PortNum));

            // Exercise the registered backend, retaining the existing FWNode payload and remote variants.
            FlowLocalExecution? execution = null;
            WpfTestHost.Invoke(() =>
            {
                var node = new FWNode();
                node.Create();
                node.DeviceCode = device!.Code;
                Assert.False(node.RequiresRemoteService);
                execution = FlowLocalExecution.CreateForNode(node, new CVMQTTRequest { DeviceCode = device.Code, EventName = "SetPort", Data = new FWData(2) });
                Assert.IsType<LocalCfwPortFlowExecution>(execution);
                node.ModelType = FWModelType.Camera_FilterWheel;
                Assert.False(LocalCfwPortFlowExecution.Supports(node));
            });
            var action = new CVStartCFC("local-cfw-test");
            execution!.Bind(action);
            await Task.Run(execution.Execute);
            Assert.Equal(2, ((JObject)execution.Complete(action)).Value<int>("Port"));
            execution.Dispose();

            using var spectrum = await Task.Run(() => new LocalSpectrumFilterWheel(new ShutterConfig
            {
                FilterWheelDeviceCode = device!.Code, UseFilterWheel = true, MeasurementPort = 5, DarkPort = 0
            }));
            // The reservation owner is a worker thread, so a UI command must fail without moving the wheel.
            var blocked = await Command(() => device!.DService.SetPort(3));
            Assert.Equal(MsgRecordState.Fail, blocked.MsgRecordState);
            // Dispose the reservation, then exercise positioning on one fixed worker, as local spectrum does.
            spectrum.Dispose();
            using var calibrationFinished = new ManualResetEventSlim();
            Task? calibration = null;
            bool calibrationCompletedWhileUiWaited = WpfTestHost.Invoke(() =>
            {
                calibration = Task.Run(() =>
                {
                    try
                    {
                        using (var operation = new LocalSpectrumFilterWheel(new ShutterConfig
                        { FilterWheelDeviceCode = device!.Code, UseFilterWheel = true, MeasurementPort = 5, DarkPort = 0 }))
                        {
                            operation.SetTransmitting(false);
                            Assert.Equal(0, native.Port);
                            operation.SetTransmitting(true);
                            Assert.Equal(5, native.Port);
                        }
                        using var available = device!.DService.ReserveForCalibration();
                    }
                    finally { calibrationFinished.Set(); }
                });
                return calibrationFinished.Wait(TimeSpan.FromSeconds(5));
            });
            await calibration!.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(calibrationCompletedWhileUiWaited);
            Assert.Equal(1, native.Opens);
            WpfTestHost.Invoke(() => device!.DisplayConfig.UseLocalCfwPort = false);
            Assert.True(device!.CfwBackend.OpensLocally);
            Assert.Equal(MsgRecordState.Success, (await Command(() => device.DService.Clsoe())).MsgRecordState);
            Assert.False(device.CfwBackend.OpensLocally);
            Assert.Equal(1, native.Closes);
            WpfTestHost.Invoke(() =>
            {
                device.DService.DeviceStatus = DeviceStatusType.Opened;
                device.DisplayConfig.UseLocalCfwPort = true;
                foreach (string operation in new[] { "SetPort", "GetPort", "Close" })
                    device.DService.MsgReturnReceived!(new MsgReturn { DeviceCode = device.Code, EventName = operation, Code = 1 });
                Assert.False(device.CfwBackend.OpensLocally);
                device.DService.MsgReturnReceived!(new MsgReturn { DeviceCode = "another-device", EventName = "Close", Code = 0 });
                Assert.False(device.CfwBackend.OpensLocally);
                device.DService.MsgReturnReceived!(new MsgReturn { DeviceCode = device.Code, EventName = "Close", Code = 0 });
                Assert.True(device.CfwBackend.OpensLocally);
            });
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                device?.Dispose();
                managerField.SetValue(null, previousManager);
                physicalManagerField.SetValue(null, previousPhysicalManager);
                if (previousConfig != null) TestEnvironment.SetConfig(previousConfig);
            });
            foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static async Task<MsgRecord> Command(Func<MsgRecord> send)
    {
        var completion = new TaskCompletionSource<MsgRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
        WpfTestHost.Invoke(() =>
        {
            var record = send();
            void Complete(object? sender, MsgRecordState state)
            {
                if (state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)) return;
                record.MsgRecordStateChanged -= Complete;
                completion.TrySetResult(record);
            }
            record.MsgRecordStateChanged += Complete;
            Complete(record, record.MsgRecordState);
        });
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class QueryWire(byte reply) : MemoryStream
    {
        public override int ReadTimeout { get; set; }
        public override int WriteTimeout { get; set; }

        public override int ReadByte()
        {
            Assert.Equal("NOW"u8.ToArray(), ToArray());
            return reply;
        }
    }

    private sealed class FakeTransport : ILocalCfwPortTransport
    {
        public int Opens, Closes, Sets, Reads, Port;
        public int? ReadOverride;
        public bool FailOpen, FailClose, FailSet, ReadTimesOut;
        public int ReadTimeout;
        public Action? OnOpen, OnSet, OnGet;
        public LocalCfwPortConnection? Connection;
        public void Open(LocalCfwPortConnection config)
        {
            Opens++;
            Connection = config;
            OnOpen?.Invoke();
            if (FailOpen) throw new IOException("串口不可用");
        }

        public void Close()
        {
            Closes++;
            if (FailClose) throw new IOException("串口释放失败");
        }

        public void SetPort(int port)
        {
            Sets++;
            OnSet?.Invoke();
            if (FailSet) throw new TimeoutException();
            Port = port;
        }

        public int ReadPort(int timeout)
        {
            Reads++;
            ReadTimeout = timeout;
            OnGet?.Invoke();
            if (ReadTimesOut) throw new TimeoutException();
            return ReadOverride ?? Port;
        }
    }
}

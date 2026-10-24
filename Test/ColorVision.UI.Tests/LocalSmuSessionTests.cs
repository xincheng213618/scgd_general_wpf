using ColorVision.Engine.Services;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.UI;
using cvColorVision;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Channel = ColorVision.Engine.Services.Devices.SMU.Dao.SMUChannelType;

namespace ColorVision.UI.Tests;

// Protect the unit contract and native handle/output ownership across cancellation and failure.
public sealed class LocalSmuSessionTests
{
    private static LocalSmuConnection Config() => new(false, "synthetic-" + Guid.NewGuid(), Pss_Type.Keithley_2600, 10, true, false);
    private static LocalSmuParameters Point(Channel channel = Channel.A) => new() { Channel = channel, MeasureValue = 5, LimitValue = 10 };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeviceCommandsRouteLocallyAndCompleteThroughMessageRecords(bool configuredAsNetwork)
    {
        string address = configuredAsNetwork ? "192.0.2.17" : "COM23";
        var sdk = new FakeNative();
        var session = new LocalSmuSession(sdk);
        DeviceSMU? device = null;
        DeviceSMU? destination = null;
        var managerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? previousManager = null;
        ColorVision.UI.IConfigService? previousConfig = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                previousConfig = ColorVision.UI.ConfigService.Instance;
                ColorVision.UI.ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                previousManager = managerField.GetValue(null);
                var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
                manager.DeviceServices = [];
                managerField.SetValue(null, manager);
                device = new DeviceSMU(new() { Id = -2, Code = "synthetic-smu-" + Guid.NewGuid(),
                    Value = Newtonsoft.Json.JsonConvert.SerializeObject(new ConfigSMU { DevName = "synthetic", DevType = Pss_Type.Keithley_2600, IsNet = configuredAsNetwork }) }, session);
                Assert.True(device.DisplayConfig.UseLocalSmu);
                device.DisplayConfig.LocalBaudRate = SMUSerialBaudRate.Baud115200;
                if (configuredAsNetwork) device.Config.IpAddress = address;
                else device.Config.SerialPort = address;
                var display = device.DisplayConfig;
                device.Config.Code += "-renamed";
                Assert.Same(display, device.DisplayConfig);
                manager.DeviceServices.Add(device);
            });
            sdk.OpenFailureCode = configuredAsNetwork ? 6 : 3;
            sdk.OpenDetail = "stage=synchronize, response=";
            var failed = await Command(() => device!.DService.Open(device.Config.IsNet, device.Config.DevName));
            Assert.Equal(MsgRecordState.Fail, failed.MsgRecordState);
            Assert.Contains(address, failed.MsgReturn.Message);
            Assert.Contains("stage=synchronize", failed.MsgReturn.Message);
            if (!configuredAsNetwork)
            {
                Assert.Contains("115200 baud", failed.MsgReturn.Message);
                Assert.Contains("波特率是否一致", failed.MsgReturn.Message);
            }
            Assert.Equal(DeviceStatusType.Closed, session.Status);
            sdk.OpenFailureCode = 0;
            var opened = await Command(() => device!.DService.Open(device.Config.IsNet, device.Config.DevName));
            Assert.Equal(MsgRecordState.Success, opened.MsgRecordState);
            Assert.Equal(DeviceStatusType.Opened, session.Status);
            Assert.Equal(configuredAsNetwork, sdk.OpenedConnection!.IsNet);
            Assert.Equal(address, sdk.OpenedConnection.DeviceName);
            Assert.Equal(configuredAsNetwork ? 9600 : 115200, sdk.OpenedConnection.BaudRate);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal("synthetic SMU", device!.LocalDeviceIdentity);
                device.DisplayConfig.UseLocalSmu = false;
                Assert.True(device.SmuBackend.OpensLocally);
            });
            var invalid = await Command(() => device!.DService.GetData(true, double.NaN, 1, Channel.B)!);
            Assert.Equal(MsgRecordState.Fail, invalid.MsgRecordState);
            Assert.Equal(0, sdk.Measurements);
            WpfTestHost.Invoke(() =>
            {
                destination = new DeviceSMU(new() { Id = -3, Code = "occupied-smu-" + Guid.NewGuid(),
                    Value = Newtonsoft.Json.JsonConvert.SerializeObject(new ConfigSMU { DevType = Pss_Type.Keithley_2600,
                        IsNet = configuredAsNetwork }) }, new LocalSmuSession(new FakeNative()));
                if (configuredAsNetwork) device!.Config.IpAddress = destination.Config.IpAddress = "192.0.2.77";
                else device!.Config.SerialPort = destination.Config.SerialPort = "COM77";
                ServiceManager.Current!.DeviceServices.Add(destination);
                destination.SmuBackend.SetLocalStatus(DeviceStatusType.Opened);
            });
            var occupied = await Command(() => device!.DService.Open(device.Config.IsNet, device.Config.DevName));
            Assert.Equal(MsgRecordState.Fail, occupied.MsgRecordState);
            Assert.Equal(0, sdk.Closes);
            Assert.Equal(2, sdk.Opens);
            Assert.Equal(MsgRecordState.Success, (await Command(() => device!.DService.CloseOutput())).MsgRecordState);
            Assert.Single(sdk.ClosedOutputs);
            Assert.Equal(MsgRecordState.Success, (await Command(() => device!.DService.Close())).MsgRecordState);
            Assert.Equal(DeviceStatusType.Closed, session.Status);
            Assert.True(destination!.SmuBackend.LocalOwned);
            WpfTestHost.Invoke(() => Assert.False(device!.SmuBackend.OpensLocally));
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                device?.Dispose(); destination?.Dispose(); managerField.SetValue(null, previousManager);
                if (previousConfig != null) ColorVision.UI.ConfigService.SetInstance(previousConfig);
            });
            await session.DisposeAsync();
        }
        static Task<MsgRecord> Command(Func<MsgRecord> send)
        {
            var completion = new TaskCompletionSource<MsgRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
            WpfTestHost.Invoke(() =>
            {
                var record = send();
                void Finish(object? sender, MsgRecordState state)
                {
                    if (state is MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)
                    { record.MsgRecordStateChanged -= Finish; completion.TrySetResult(record); }
                }
                record.MsgRecordStateChanged += Finish;
                Finish(record, record.MsgRecordState);
            });
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Theory]
    [InlineData(true, 5, 10, 5, 0.01)]
    [InlineData(false, 10, 5, 0.01, 5)]
    public async Task PointAndReadUseVoltsAndMilliamps(bool voltage, double source, double limit, double nativeSource, double nativeLimit)
    {
        var sdk = new FakeNative();
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        var parameters = Point(Channel.B) with { IsSourceV = voltage, MeasureValue = source, LimitValue = limit };
        var point = await session.CaptureAsync(config, parameters, false, true, true);
        Assert.Equal(nativeSource, sdk.LastSource);
        Assert.Equal(nativeLimit, sdk.LastLimit);
        Assert.Equal(5, point.V);
        Assert.Equal(12, point.I);
        var read = await session.CaptureAsync(config, Point(), false, false, false, read: true);
        Assert.Equal(Channel.B, read.Parameters.Channel);
        Assert.Equal(voltage, read.Parameters.IsSourceV);
        Assert.Equal(12, read.I);
        var stored = LocalSmuResultService.CreateMeasurement(point, "synthetic");
        Assert.Equal(12, stored.IResult);
        Assert.Equal((float)source, stored.SrcValue);
        Assert.Equal((float)limit, stored.LimitValue);
        Assert.Equal(point.TotalTime, stored.TotalTime);
        var view = new ColorVision.Engine.Services.Devices.SMU.Views.ViewResultSMU(stored);
        Assert.Equal((float)source, view.LimitStart);
        Assert.Equal((float)limit, view.LimitEnd);
        Assert.Equal(point.TotalTime, view.TotalTime);
        dynamic response = LocalSmuResultService.Response(point, 0);
        Assert.Equal(12d, (double)response.I);
        await session.CloseAsync();
        Assert.Equal(new[] { Channel.B }, sdk.ClosedOutputs);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
    }

    // A node override must preserve zero and must not become the next caller's device default.
    [Fact]
    public async Task MeasurementDelayChangesAndOverridesAreScopedAndValidatedBeforeNativeCalls()
    {
        var sdk = new FakeNative();
        await using var session = new LocalSmuSession(sdk);
        var config = Config() with { DelayTime = 100 };
        await session.OpenAsync(config);
        var zero = await session.CaptureAsync(config, Point() with { MeasureDelayMs = 0 }, false, true, true);
        Assert.Equal(0d, zero.Parameters.MeasureDelayMs);
        await session.CaptureAsync(config, Point(Channel.B) with { MeasureDelayMs = 25 }, false, true, true);
        await session.CaptureAsync(config, Point(), false, true, true);
        await session.CaptureAsync(config, Point(), false, false, false, read: true);
        await session.CaptureAsync(config, Point() with { MeasureDelayMs = 0, BeginValue = 1, EndValue = 2, Points = 2 }, true, true, true);
        await session.CaptureAsync(config, Point(), false, false, true);
        config = config with { DelayTime = 40 };
        await session.OpenAsync(config);
        await session.CaptureAsync(config, Point(), false, true, true);
        await session.CaptureAsync(config with { DelayTime = 0 }, Point(), false, true, true);
        Assert.Equal(new double[] { 100, 0, 25, 100, 0, 100, 40, 0 }, sdk.AppliedDelays);
        Assert.Equal(1, sdk.Opens);
        int measurements = sdk.Measurements;
        foreach (double delay in new[] { -1d, double.NaN, double.PositiveInfinity })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => session.CaptureAsync(config, Point() with { MeasureDelayMs = delay }, false, true, true));
            await Assert.ThrowsAsync<ArgumentException>(() => session.CaptureAsync(config with { DelayTime = delay }, Point(), false, true, true));
        }
        Assert.Equal(measurements, sdk.Measurements);
        Assert.Equal(8, sdk.AppliedDelays.Count);
        Assert.Equal(0, sdk.Closes);
    }

    [Fact]
    public void ConnectionEditorsCommitOneLegacyAddressAndSwitchByTransport()
    {
        var serialProperty = typeof(ConfigSMU).GetProperty(nameof(ConfigSMU.SerialPort))!;
        var networkProperty = typeof(ConfigSMU).GetProperty(nameof(ConfigSMU.IpAddress))!;
        Assert.False(TypeDescriptor.GetProperties(typeof(ConfigSMU))[nameof(ConfigSMU.DevName)]!.IsBrowsable);
        Assert.Equal(typeof(ColorVision.Engine.PropertyEditor.TextSerialPortPropertiesEditor),
            serialProperty.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
        Assert.Equal(typeof(IPAddressPropertiesEditor), networkProperty.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
        var serialVisibility = serialProperty.GetCustomAttribute<PropertyVisibilityAttribute>()!;
        var networkVisibility = networkProperty.GetCustomAttribute<PropertyVisibilityAttribute>()!;
        Assert.Equal(nameof(ConfigSMU.IsNet), serialVisibility.PropertyName);
        Assert.True(serialVisibility.IsInverted);
        Assert.Equal(nameof(ConfigSMU.IsNet), networkVisibility.PropertyName);
        Assert.False(networkVisibility.IsInverted);

        // Hidden DevName must still survive transactional copying through its editable aliases.
        var source = new JObject { ["IsNet"] = false, ["DevName"] = "COM7", ["DevType"] = (int)Pss_Type.Keithley_2600 }
            .ToObject<ConfigSMU>()!;
        var edit = PropertyEditSession.Create(source, PropertyEditorEditMode.Transactional);
        var working = Assert.IsType<ConfigSMU>(edit.EditableObject);
        Assert.Equal("COM7", working.SerialPort);
        Assert.Equal("COM7", working.IpAddress);
        var changes = new List<string?>();
        working.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        working.SerialPort = "COM23";
        Assert.Equal("COM7", source.DevName);
        Assert.Contains(nameof(ConfigSMU.DevName), changes);
        Assert.Contains(nameof(ConfigSMU.SerialPort), changes);
        Assert.Contains(nameof(ConfigSMU.IpAddress), changes);
        edit.Commit();
        Assert.False(source.IsNet);
        Assert.Equal("COM23", source.DevName);
        AssertLegacyPayload(source, "COM23");

        changes.Clear();
        working.IsNet = true;
        Assert.Contains(nameof(ConfigSMU.IsNet), changes);
        Assert.Contains(nameof(ConfigSMU.SerialPort), changes);
        Assert.Contains(nameof(ConfigSMU.IpAddress), changes);
        working.IpAddress = "192.0.2.17";
        Assert.False(source.IsNet);
        Assert.Equal("COM23", source.DevName);
        edit.Commit();
        Assert.True(source.IsNet);
        Assert.Equal("192.0.2.17", source.DevName);
        Assert.Equal(source.DevName, source.SerialPort);
        AssertLegacyPayload(source, "192.0.2.17");

        var legacyNetwork = new JObject { ["IsNet"] = true, ["DevName"] = "192.0.2.8", ["DevType"] = (int)Pss_Type.Keithley_2600 }
            .ToObject<ConfigSMU>()!;
        Assert.True(legacyNetwork.IsNet);
        Assert.Equal("192.0.2.8", legacyNetwork.IpAddress);

        static void AssertLegacyPayload(ConfigSMU config, string address)
        {
            var payload = JObject.FromObject(config);
            Assert.Equal(address, payload.Value<string>(nameof(ConfigSMU.DevName)));
            Assert.Null(payload[nameof(ConfigSMU.SerialPort)]);
            Assert.Null(payload[nameof(ConfigSMU.IpAddress)]);
            var restored = payload.ToObject<ConfigSMU>()!;
            Assert.Equal(config.IsNet, restored.IsNet);
            Assert.Equal(address, restored.DevName);
            Assert.Equal(address, restored.IsNet ? restored.IpAddress : restored.SerialPort);
        }
    }

    [Fact]
    public void LocalBaudRateDefaultsTo9600AndPersistsForSerialConnections()
    {
        var display = new JObject().ToObject<DisplaySMUConfig>()!;
        Assert.Equal(SMUSerialBaudRate.Baud9600, display.LocalBaudRate);
        display.LocalBaudRate = SMUSerialBaudRate.Baud115200;
        display = JObject.FromObject(display).ToObject<DisplaySMUConfig>()!;
        Assert.Equal(SMUSerialBaudRate.Baud115200, display.LocalBaudRate);
        var config = new ConfigSMU { DevName = "COM7", DevType = Pss_Type.Keithley_2600 };
        var serial = LocalSmuConnection.From(config, (int)display.LocalBaudRate);
        serial.Validate();
        Assert.Equal(115200, serial.BaudRate);
        config.IsNet = true;
        Assert.Equal(LocalSmuConnection.From(config), LocalSmuConnection.From(config, (int)display.LocalBaudRate));
    }

    [Theory]
    [InlineData(Pss_Type.Keithley_2600, 0)]
    [InlineData(Pss_Type.Keithley_2600, 921600)]
    [InlineData(Pss_Type.Keithley_2400, 115200)]
    [InlineData(Pss_Type.Precise_S100, 115200)]
    public async Task UnsupportedBaudRateNeverOpensNativeConnection(Pss_Type type, int baudRate)
    {
        var sdk = new FakeNative();
        await using var session = new LocalSmuSession(sdk);
        await Assert.ThrowsAsync<ArgumentException>(() => session.OpenAsync(Config() with { DeviceType = type, BaudRate = baudRate }));
        Assert.Null(sdk.OpenedConnection);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SweepKeepsRangesAndClosesSubmittedChannelBeforeQueuedMeasurement(bool autoRange)
    {
        var sdk = new FakeNative { BlockCapture = true };
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        var scan = Point(Channel.B) with { IsSourceV = false, BeginValue = 1, EndValue = 3, Points = 3,
            IsAutoRng = autoRange, SrcRng = 0.1, LmtRng = 20, IsCloseOutput = true };
        Task<LocalSmuCapture> scanning = session.CaptureAsync(config, scan, true, false, true);
        await sdk.CaptureStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<LocalSmuCapture> queued = session.CaptureAsync(config, Point(), false, false, true);
        sdk.Release.TrySetResult();
        var result = await scanning;
        await queued;
        Assert.Equal(new double[] { 10, 20, 30 }, result.Currents);
        Assert.Equal(autoRange, sdk.ScanParameters!.IsAutoRng);
        Assert.Equal(0.1, sdk.ScanParameters.SrcRng);
        Assert.Equal(20, sdk.ScanParameters.LmtRng);
        Assert.True(sdk.Events.IndexOf("off:B") < sdk.Events.IndexOf("measure:A"));
        var model = LocalSmuResultService.CreateScan(result, "synthetic");
        Assert.Equal(new double[] { 0.01, 0.02, 0.03 }, JArray.Parse(model.IResult!).ToObject<double[]>());
        Assert.Equal(new double[] { 0.001, 0.002, 0.003 }, LocalSmuResultService.Response(result, 0)["ScanList"]!.ToObject<double[]>());
        Assert.Equal(Channel.B, LocalSmuResultService.CreateView(result, "synthetic", 0).ChannelType);
    }

    [Fact]
    public async Task ClosingWaitsForNativeReturnAndRejectsQueuedCapture()
    {
        var sdk = new FakeNative { BlockCapture = true };
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        Task first = session.CaptureAsync(config, Point(Channel.B), false, true, true);
        await sdk.CaptureStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task queued = session.CaptureAsync(config, Point(), false, true, true);
        Task closing = session.CloseAsync();
        Assert.False(closing.IsCompleted);
        Assert.Equal(0, sdk.Closes);
        await using var contender = new LocalSmuSession(new FakeNative());
        await Assert.ThrowsAsync<InvalidOperationException>(() => contender.OpenAsync(config));
        sdk.Release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        await closing;
        Assert.Equal(1, sdk.Measurements);
        Assert.Equal(1, sdk.Closes);
        Assert.Equal(new[] { Channel.B }, sdk.ClosedOutputs);
        await contender.OpenAsync(config);
    }

    [Fact]
    public async Task CallerCancellationClosesConnectionAfterNativeReturn()
    {
        var sdk = new FakeNative { BlockCapture = true };
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        using var stop = new CancellationTokenSource();
        Task measuring = session.CaptureAsync(config, Point(), false, true, true, token: stop.Token);
        await sdk.CaptureStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        stop.Cancel();
        Assert.False(measuring.IsCompleted);
        Assert.Equal(0, sdk.Closes);
        sdk.Release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => measuring);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
        Assert.Equal(new[] { Channel.A }, sdk.ClosedOutputs);
    }

    [Fact]
    public async Task CancelingQueuedWorkDoesNotCloseActiveOutput()
    {
        var sdk = new FakeNative { BlockCapture = true };
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        Task active = session.CaptureAsync(config, Point(), false, true, true);
        await sdk.CaptureStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var stop = new CancellationTokenSource();
        Task queued = session.CaptureAsync(config, Point(Channel.B), false, true, true, token: stop.Token);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.Empty(sdk.ClosedOutputs);
        sdk.Release.TrySetResult();
        await active;
        Assert.Empty(sdk.ClosedOutputs);
        Assert.Equal(1, sdk.Measurements);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public async Task FailedMeasurementClosesChannelAndSurfacesCleanupFailure(int closeOutputCode)
    {
        var sdk = new FakeNative { MeasureCode = 0, CloseOutputCode = closeOutputCode };
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        try
        {
            await session.OpenAsync(config);
            var failure = await Record.ExceptionAsync(() => session.CaptureAsync(config, Point(Channel.B), false, true, true));
            if (closeOutputCode == 1) Assert.IsType<InvalidOperationException>(failure);
            else Assert.Equal(2, Assert.IsType<AggregateException>(failure).InnerExceptions.Count);
            Assert.Equal(new[] { Channel.B }, sdk.ClosedOutputs);
            Assert.Equal(closeOutputCode == 1 ? DeviceStatusType.Opened : DeviceStatusType.Unknown, session.Status);
        }
        finally { sdk.CloseOutputCode = 1; }
    }

    [Fact]
    public async Task DiscardedCaptureCannotCloseNewerOutput()
    {
        var sdk = new FakeNative();
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        var first = await session.CaptureAsync(config, Point(), false, true, true);
        var next = await session.CaptureAsync(config, Point() with { MeasureValue = 6 }, false, true, true);
        await session.CloseOutputAsync(Channel.A, expectedGeneration: first.Generation);
        Assert.Empty(sdk.ClosedOutputs);
        await session.CloseOutputAsync(Channel.A, expectedGeneration: next.Generation);
        Assert.Equal(new[] { Channel.A }, sdk.ClosedOutputs);
    }

    [Theory]
    [InlineData(-1, 1, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, 4)]
    public async Task FailedOpenReportsAttemptAndReleasesHandleAndEndpoint(int openCode, int wiringCode, int failureCode)
    {
        var sdk = new FakeNative { OpenCode = openCode, WiringCode = wiringCode, OpenFailureCode = failureCode,
            OpenDetail = "stage=reset status, response=TSP?, instrument=-100, synthetic error" };
        await using var session = new LocalSmuSession(sdk);
        var config = Config() with { BaudRate = 57600 };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => session.OpenAsync(config));
        Assert.Contains(config.DeviceName, error.Message);
        Assert.Contains("57600 baud", error.Message);
        Assert.Contains(wiringCode == 0 ? "设置接线" : "打开连接", error.Message);
        if (failureCode == 4)
        {
            Assert.Contains("TSP?", error.Message);
            Assert.Contains("instrument=-100, synthetic error", error.Message);
        }
        Assert.Equal(wiringCode == 0 ? 1 : 0, sdk.Closes);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
        await using var contender = new LocalSmuSession(new FakeNative());
        await contender.OpenAsync(config);
        await contender.CloseAsync();
        sdk.OpenCode = 0; sdk.WiringCode = 1; sdk.OpenFailureCode = 0;
        await session.OpenAsync(config with { BaudRate = 115200 });
        Assert.Equal(115200, sdk.OpenedConnection!.BaudRate);
        Assert.Equal(DeviceStatusType.Opened, session.Status);
    }

    [Fact]
    public async Task FailedCloseQuarantinesEndpointUntilConfirmedRelease()
    {
        var sdk = new FakeNative { CloseCode = 0 };
        var session = new LocalSmuSession(sdk);
        var config = Config();
        try
        {
            await session.OpenAsync(config);
            await session.CaptureAsync(config, Point(Channel.B), false, true, true);
            await Assert.ThrowsAsync<AggregateException>(() => session.CloseAsync());
            Assert.Equal(DeviceStatusType.Unknown, session.Status);
            await using var contender = new LocalSmuSession(new FakeNative());
            await Assert.ThrowsAsync<InvalidOperationException>(() => contender.OpenAsync(config));
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.OpenAsync(config));
            sdk.CloseCode = 1;
            await session.CloseAsync();
            await contender.OpenAsync(config);
        }
        finally { sdk.CloseCode = 1; await session.DisposeAsync(); }
    }

    [Fact]
    public async Task InvalidSweepNeverReconnectsOrReachesMeasurement()
    {
        var sdk = new FakeNative();
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        var changed = config with { BaudRate = 115200 };
        await Assert.ThrowsAsync<ArgumentException>(() => session.CaptureAsync(changed,
            Point() with { BeginValue = 50, EndValue = 1, Points = 3 }, true, false, true));
        await Assert.ThrowsAsync<ArgumentException>(() => session.CaptureAsync(changed,
            Point() with { Points = 1 }, true, false, true));
        Assert.Equal(1, sdk.Opens);
        Assert.Equal(0, sdk.Closes);
        Assert.Equal(0, sdk.Measurements);
    }

    // Reconnect is a serialized resource handover, not a retry of a completed output operation.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ChangedOpenSettingsReconnectBeforeMeasurementAndPreserveOwnership(int change)
    {
        var sdk = new FakeNative();
        await using var session = new LocalSmuSession(sdk);
        await using var contender = new LocalSmuSession(new FakeNative());
        var config = Config();
        await session.OpenAsync(config);
        object owner = new();
        using var lease = await session.AcquireFlowLeaseAsync(owner, () => false);
        lease.ConfigureChannel(Channel.A, true);
        var first = await session.CaptureAsync(config, Point(), false, true, true, flowOwner: owner);
        var changed = change switch
        {
            0 => config with { DeviceName = config.DeviceName + "-other" },
            1 => config with { DeviceType = Pss_Type.Keithley_2400 },
            2 => config with { IsNet = true },
            3 => config with { Is4Wire = !config.Is4Wire },
            4 => config with { IsFront = !config.IsFront },
            _ => config with { BaudRate = 115200 }
        };
        bool ownershipChecked = false;
        void CheckOwnership(DeviceStatusType status)
        {
            if (status != DeviceStatusType.Closed) return;
            Assert.Throws<InvalidOperationException>(() => contender.OpenAsync(changed).GetAwaiter().GetResult());
            ownershipChecked = true;
        }
        sdk.Events.Clear();
        sdk.SweepCapabilities = 3;
        session.StatusChanged += CheckOwnership;
        try { await session.CaptureAsync(changed, Point(), false, true, true, flowOwner: owner); }
        finally { session.StatusChanged -= CheckOwnership; }
        Assert.True(ownershipChecked);
        Assert.Equal(new[] { "off:A", "close", "open", "measure:A" }, sdk.Events);
        Assert.Equal(changed, sdk.OpenedConnection);
        Assert.Equal(2, sdk.Opens);
        Assert.Equal(1, sdk.Closes);
        Assert.Equal(2, sdk.Measurements);
        Assert.Equal(3, session.SweepCapabilities);
        await session.CloseOutputAsync(Channel.A, expectedGeneration: first.Generation, flowOwner: owner);
        Assert.Single(sdk.ClosedOutputs);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.OpenAsync(changed));
        await session.OpenAsync(changed, flowOwner: owner);
        Assert.Equal(2, sdk.Opens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedReconnectCleanupNeverOpensDestinationAndRetainsOldOwnership(bool outputCloseFails)
    {
        var sdk = new FakeNative();
        var session = new LocalSmuSession(sdk);
        var config = Config();
        var changed = config with { DeviceName = config.DeviceName + "-other" };
        try
        {
            await session.OpenAsync(config);
            await session.CaptureAsync(config, Point(), false, true, true);
            sdk.CloseOutputCode = outputCloseFails ? 0 : 1;
            sdk.CloseCode = outputCloseFails ? 1 : 0;
            await Assert.ThrowsAsync<AggregateException>(() => session.CaptureAsync(changed, Point(), false, true, true));
            Assert.Equal(DeviceStatusType.Unknown, session.Status);
            Assert.Equal(1, sdk.Opens);
            Assert.Equal(1, sdk.Measurements);
            await Assert.ThrowsAsync<InvalidOperationException>(() => session.OpenAsync(changed));
            await using var contender = new LocalSmuSession(new FakeNative());
            await Assert.ThrowsAsync<InvalidOperationException>(() => contender.OpenAsync(config));
            await contender.OpenAsync(changed);
        }
        finally { sdk.CloseCode = sdk.CloseOutputCode = 1; await session.DisposeAsync(); }
    }

    [Fact]
    public async Task FailedReconnectOpenNeverRetriesMeasurementAndReleasesDestination()
    {
        var sdk = new FakeNative();
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        await session.CaptureAsync(config, Point(), false, true, true);
        sdk.OpenCode = -1;
        var changed = config with { BaudRate = 115200 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAsync(changed, Point(), false, true, true));
        Assert.Equal(2, sdk.Opens);
        Assert.Equal(1, sdk.Measurements);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
        await using var contender = new LocalSmuSession(new FakeNative());
        await contender.OpenAsync(changed);
    }

    [Fact]
    public async Task ReadingPreviousResultAndClosingOutputNeverReconnect()
    {
        var sdk = new FakeNative();
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        await session.CaptureAsync(config, Point(), false, true, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAsync(config with { IsFront = !config.IsFront },
            Point(), false, false, true, read: true));
        await session.CloseOutputAsync(Channel.A);
        Assert.Equal(1, sdk.Opens);
        Assert.Equal(0, sdk.Closes);
        Assert.Single(sdk.ClosedOutputs);
    }

    [Fact]
    public void BackendStaysPinnedUntilCurrentOwnerIsClosed()
    {
        var backend = new SmuBackendState(false);
        backend.ObserveService(DeviceStatusType.LiveOpened);
        backend.SetPreference(true);
        backend.ObserveService(DeviceStatusType.OffLine);
        Assert.False(backend.OpensLocally);
        Assert.Throws<InvalidOperationException>(backend.EnsureLocalAvailable);
        backend.ObserveService(DeviceStatusType.Closed);
        Assert.True(backend.OpensLocally);
        backend.SetLocalStatus(DeviceStatusType.Opened);
        backend.SetPreference(false);
        Assert.True(backend.OpensLocally);
        Assert.Throws<InvalidOperationException>(backend.EnsureServiceAvailable);
        backend.SetLocalStatus(DeviceStatusType.Closed);
        Assert.False(backend.OpensLocally);
    }

    [Fact]
    public async Task FlowLeaseKeepsOutputAcrossPointsAndRejectsInterleavedCommands()
    {
        var sdk = new FakeNative();
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        var owner = new object();
        using var lease = await session.AcquireFlowLeaseAsync(owner, () => false);
        Assert.Same(lease, await session.AcquireFlowLeaseAsync(owner, () => false));
        lease.ConfigureChannel(Channel.B, true);
        await session.OpenAsync(config, flowOwner: owner);
        await session.CaptureAsync(config, Point(Channel.B), false, true, true, flowOwner: owner);
        await session.CaptureAsync(config, Point(Channel.B) with { MeasureValue = 6 }, false, true, true, flowOwner: owner);
        Assert.Empty(sdk.ClosedOutputs);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAsync(config, Point(), false, true, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CloseOutputAsync(Channel.B));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.AcquireFlowLeaseAsync(new object(), () => false));
        Assert.Throws<InvalidOperationException>(() => { _ = session.CloseAsync(); });
        Assert.Equal(2, sdk.Measurements);
        lease.Dispose();
        Assert.Equal(new[] { Channel.B }, sdk.ClosedOutputs);
        await session.CaptureAsync(config, Point(), false, true, true);
        Assert.Equal(3, sdk.Measurements);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task FlowLeaseClosesOnlyAtFlowReleaseAndHonorsSuccessfulKeepOutput(bool closeOnFinish, bool failed, bool shouldClose)
    {
        var sdk = new FakeNative();
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        var owner = new object();
        var lease = await session.AcquireFlowLeaseAsync(owner, () => failed);
        lease.ConfigureChannel(Channel.B, closeOnFinish);
        await session.OpenAsync(config, flowOwner: owner);
        await session.CaptureAsync(config, Point(Channel.B), false, true, true, flowOwner: owner);
        Assert.Empty(sdk.ClosedOutputs);
        lease.Dispose();
        lease.Dispose();
        Assert.Equal(shouldClose ? 1 : 0, sdk.ClosedOutputs.Count);
        await session.CloseAsync();
        Assert.Single(sdk.ClosedOutputs);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task RepeatScanPreservesRequestedSourcesAndActualMeasurements(bool list, bool voltageSource)
    {
        var sdk = new FakeNative { SweepCapabilities = 3 };
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        Assert.Equal(3, session.SweepCapabilities);
        sdk.SweepCapabilities = 0;
        Assert.Equal(3, session.SweepCapabilities);
        var parameters = Scan(list, voltageSource);
        var capture = await session.CaptureAsync(config, parameters, true, false, true);
        Assert.Equal(list ? 0 : 1, sdk.Sweeps);
        Assert.Equal(list ? 1 : 0, sdk.Lists);
        Assert.Equal(0, sdk.PointMeasurements);
        Assert.Equal(parameters.SourceValues, capture.Parameters.SourceValues);
        Assert.Equal(parameters.IsAutoRng, sdk.ScanParameters!.IsAutoRng);
        Assert.Equal(parameters.SrcRng, sdk.ScanParameters.SrcRng);
        Assert.Equal(parameters.LmtRng, sdk.ScanParameters.LmtRng);
        if (list) Assert.Equal(parameters.SourceValues, sdk.ListSourceValues);
        Assert.Equal(new double[] { 1, 2, 3 }, capture.Voltages);
        Assert.Equal(new double[] { 10, 20, 30 }, capture.Currents);
        Assert.Equal(3, capture.V);
        Assert.Equal(30, capture.I);
        var response = LocalSmuResultService.Response(capture, 0);
        Assert.Equal(parameters.SourceValues!.Select(parameters.NativeSource).ToArray(), response["ScanList"]!.ToObject<double[]>());
        Assert.Equal(new double[] { 0.01, 0.02, 0.03 }, response["IList"]!.ToObject<double[]>());
        Assert.Equal(new double[] { 0.01, 0.02, 0.03 }, JArray.Parse(LocalSmuResultService.CreateScan(capture, "synthetic").IResult!).ToObject<double[]>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRepeatScanClosesOutputWithoutRetryingPointMeasurements(bool list)
    {
        var sdk = new FakeNative { SweepCapabilities = 3, SweepCode = 0, ListCode = 0 };
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAsync(config, Scan(list), true, false, true));
        Assert.Equal(1, sdk.Sweeps + sdk.Lists);
        Assert.Equal(0, sdk.PointMeasurements);
        Assert.Equal(new[] { Channel.B }, sdk.ClosedOutputs);
        Assert.Equal(DeviceStatusType.Opened, session.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledRepeatScanKeepsHandleUntilNativeReturnsAndNeverRetriesPoints(bool list)
    {
        var sdk = new FakeNative { SweepCapabilities = 3, BlockCapture = true };
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        using var stop = new CancellationTokenSource();
        Task scanning = session.CaptureAsync(config, Scan(list), true, false, true, token: stop.Token);
        try
        {
            await sdk.CaptureStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stop.Cancel();
            Assert.False(scanning.IsCompleted);
            Assert.Empty(sdk.ClosedOutputs);
            Assert.Equal(0, sdk.Closes);
        }
        finally { sdk.Release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanning);
        Assert.Equal(1, sdk.Sweeps + sdk.Lists);
        Assert.Equal(0, sdk.PointMeasurements);
        Assert.Equal(new[] { Channel.B }, sdk.ClosedOutputs);
        Assert.Equal(1, sdk.Closes);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
    }

    [Fact]
    public async Task RepeatListValidatesEverySourceBeforeStartingNativeMeasurement()
    {
        var sdk = new FakeNative { SweepCapabilities = 3 };
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        var parameters = Scan(true) with { BeginValue = 1, EndValue = 2, SourceValues = [1, 250, 2] };
        await Assert.ThrowsAsync<ArgumentException>(() => session.CaptureAsync(config, parameters, true, false, true));
        Assert.Equal(0, sdk.Sweeps + sdk.Lists + sdk.PointMeasurements);
        Assert.Empty(sdk.ClosedOutputs);
    }

    private static LocalSmuParameters Scan(bool list, bool voltageSource = true)
    {
        double[] values = list ? [3, 1, 4] : [1, 2, 3];
        return Point(Channel.B) with
        {
            IsSourceV = voltageSource, MeasureValue = values[0], BeginValue = values[0], EndValue = values[^1], Points = values.Length,
            IsAutoRng = false, SrcRng = voltageSource ? 20 : 0.1, LmtRng = voltageSource ? 0.1 : 20, SourceValues = values, IsListSweep = list
        };
    }

    private sealed class FakeNative : ILocalSmuNative
    {
        internal int OpenCode, OpenFailureCode, Opens;
        internal string OpenDetail = string.Empty;
        internal int WiringCode = 1, CloseCode = 1, MeasureCode = 1, CloseOutputCode = 1, Closes, Measurements;
        internal int SweepCapabilities, SweepCode = 1, ListCode = 1, Sweeps, Lists, PointMeasurements;
        internal bool BlockCapture;
        internal double LastSource, LastLimit;
        internal LocalSmuParameters? ScanParameters;
        internal double[]? ListSourceValues;
        internal LocalSmuConnection? OpenedConnection;
        internal readonly TaskCompletionSource CaptureStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly List<string> Events = [];
        internal readonly List<double> AppliedDelays = [];
        internal readonly List<Channel> ClosedOutputs = [];
        private Channel channel;
        public int Open(LocalSmuConnection c)
        {
            Events.Add("open");
            Opens++;
            OpenedConnection = c;
            if (OpenFailureCode != 0) throw LocalSmuNative.OpenFailure(OpenFailureCode, OpenDetail);
            return OpenCode; // Zero is a valid SDK handle.
        }
        public int GetIdn(int h, StringBuilder text, ref int length) { text.Append("synthetic SMU"); return 1; }
        public int SetWiring(int h, bool wire, bool front) => WiringCode;
        public int SetDelay(int h, double delay) { AppliedDelays.Add(delay); return 1; }
        public int SetChannel(int h, bool channelA) { channel = channelA ? Channel.A : Channel.B; return 1; }
        public int SetSource(int h, bool voltage) => 1;
        private void Capture()
        {
            Measurements++;
            CaptureStarted.TrySetResult();
            if (BlockCapture) Release.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        }
        public int Measure(int h, LocalSmuParameters p, bool step, ref double v, ref double i)
        {
            PointMeasurements++;
            Events.Add("measure:" + channel); Capture();
            LastSource = p.NativeSource(p.MeasureValue); LastLimit = p.NativeLimit;
            v = 5; i = 0.012; return MeasureCode;
        }
        public int Read(int h, ref double v, ref double i) { v = 5; i = 0.012; return 1; }
        public int Sweep(int h, LocalSmuParameters p, double[] v, double[] i)
        {
            Sweeps++; ScanParameters = p;
            Events.Add("scan:" + channel); Capture();
            for (int index = 0; index < v.Length; index++) { v[index] = index + 1; i[index] = (index + 1) * 0.01; }
            return SweepCode;
        }
        public int GetSweepCapabilities(int h) => SweepCapabilities;
        public int List(int h, LocalSmuParameters p, double[] sourceValues, double[] v, double[] i)
        {
            Lists++; ScanParameters = p; ListSourceValues = (double[])sourceValues.Clone();
            Events.Add("list:" + channel); Capture();
            for (int index = 0; index < v.Length; index++) { v[index] = index + 1; i[index] = (index + 1) * 0.01; }
            return ListCode;
        }
        public int CloseOutput(int h) { Events.Add("off:" + channel); ClosedOutputs.Add(channel); return CloseOutputCode; }
        public int Close(int h) { Events.Add("close"); Closes++; return CloseCode; }
    }
}

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
        ColorVision.UI.IConfigService? previousConfig = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                previousConfig = ColorVision.UI.ConfigService.Instance;
                ColorVision.UI.ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                device = new DeviceSMU(new() { Id = -2, Code = "synthetic-smu-" + Guid.NewGuid(),
                    Value = Newtonsoft.Json.JsonConvert.SerializeObject(new ConfigSMU { DevName = "synthetic", DevType = Pss_Type.Keithley_2600, IsNet = configuredAsNetwork }) }, session);
                Assert.True(device.DisplayConfig.UseLocalSmu);
                device.DisplayConfig.LocalBaudRate = SMUSerialBaudRate.Baud115200;
                if (configuredAsNetwork) device.Config.IpAddress = address;
                else device.Config.SerialPort = address;
                var display = device.DisplayConfig;
                device.Config.Code += "-renamed";
                Assert.Same(display, device.DisplayConfig);
            });
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
            Assert.Equal(MsgRecordState.Success, (await Command(() => device!.DService.Close())).MsgRecordState);
            Assert.Equal(DeviceStatusType.Closed, session.Status);
            WpfTestHost.Invoke(() => Assert.False(device!.SmuBackend.OpensLocally));
        }
        finally
        {
            WpfTestHost.Invoke(() => { device?.Dispose(); if (previousConfig != null) ColorVision.UI.ConfigService.SetInstance(previousConfig); });
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
        Assert.Equal(12, LocalSmuResultService.CreateMeasurement(point, "synthetic").IResult);
        dynamic response = LocalSmuResultService.Response(point, 0);
        Assert.Equal(12d, (double)response.I);
        await session.CloseAsync();
        Assert.Equal(new[] { Channel.B }, sdk.ClosedOutputs);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
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

    [Fact]
    public async Task FailedOpenReleasesHandleAndEndpoint()
    {
        var sdk = new FakeNative { WiringCode = 0 };
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.OpenAsync(config));
        Assert.Equal(1, sdk.Closes);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
        await using var contender = new LocalSmuSession(new FakeNative());
        await contender.OpenAsync(config);
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
    public async Task InvalidSweepAndChangedConnectionNeverReachMeasurement()
    {
        var sdk = new FakeNative();
        await using var session = new LocalSmuSession(sdk);
        var config = Config();
        await session.OpenAsync(config);
        await Assert.ThrowsAsync<ArgumentException>(() => session.CaptureAsync(config,
            Point() with { BeginValue = 50, EndValue = 1, Points = 3 }, true, false, true));
        await Assert.ThrowsAsync<ArgumentException>(() => session.CaptureAsync(config,
            Point() with { Points = 1 }, true, false, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAsync(config with { DelayTime = 20 }, Point(), false, false, true));
        var faster = config with { BaudRate = 115200 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAsync(faster, Point(), false, false, true));
        Assert.Equal(0, sdk.Measurements);
        await session.CloseAsync();
        await session.OpenAsync(faster);
        Assert.Equal(115200, sdk.OpenedConnection!.BaudRate);
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

    private sealed class FakeNative : ILocalSmuNative
    {
        internal int WiringCode = 1, CloseCode = 1, MeasureCode = 1, CloseOutputCode = 1, Closes, Measurements;
        internal bool BlockCapture;
        internal double LastSource, LastLimit;
        internal LocalSmuParameters? ScanParameters;
        internal LocalSmuConnection? OpenedConnection;
        internal readonly TaskCompletionSource CaptureStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly List<string> Events = [];
        internal readonly List<Channel> ClosedOutputs = [];
        private Channel channel;
        public int Open(LocalSmuConnection c) { OpenedConnection = c; return 0; } // Zero is a valid SDK handle.
        public int GetIdn(int h, StringBuilder text, ref int length) { text.Append("synthetic SMU"); return 1; }
        public int SetWiring(int h, bool wire, bool front) => WiringCode;
        public int SetDelay(int h, double delay) => 1;
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
            Events.Add("measure:" + channel); Capture();
            LastSource = p.NativeSource(p.MeasureValue); LastLimit = p.NativeLimit;
            v = 5; i = 0.012; return MeasureCode;
        }
        public int Read(int h, ref double v, ref double i) { v = 5; i = 0.012; return 1; }
        public int Sweep(int h, LocalSmuParameters p, double[] v, double[] i)
        {
            Events.Add("scan:" + channel); Capture(); ScanParameters = p;
            for (int index = 0; index < v.Length; index++) { v[index] = index + 1; i[index] = (index + 1) * 0.01; }
            return 1;
        }
        public int CloseOutput(int h) { Events.Add("off:" + channel); ClosedOutputs.Add(channel); return CloseOutputCode; }
        public int Close(int h) { Closes++; return CloseCode; }
    }
}

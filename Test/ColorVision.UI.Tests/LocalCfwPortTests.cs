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

namespace ColorVision.UI.Tests;

// Protect SDK ownership, verified positioning, and the shared manual/Flow/spectrum connection.
public sealed class LocalCfwPortTests
{
    private static int nextPort = 5000;
    private static LocalCfwPortConnection Connection() => new("COM" + Interlocked.Increment(ref nextPort), 9600, 5000, 1, 6, 0, false, false);

    [Fact]
    public async Task SpectrumOwnedNdReusesItsConnectionAndBlocksServiceUntilDisconnect()
    {
        var native = new FakeNative();
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

    [Theory]
    [InlineData(6, 5)]
    [InlineData(12, 11)]
    public async Task NativeSettingsAndZeroBasedPortArePreserved(int ports, int target)
    {
        var native = new FakeNative();
        await using var session = new LocalCfwPortSession(native);
        var config = Connection() with { Ports = ports, BaudRate = 19200, Timeout = 7000 };
        Assert.Equal(target, await session.ExecuteAsync("SetPort", config, target, true));
        Assert.Equal(config, native.Connection);
        Assert.Equal(1, native.Opens);
        Assert.Equal(1, native.Reads);
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
        var native = new FakeNative { ReadOverride = 2 };
        await using var session = new LocalCfwPortSession(native);
        var config = Connection();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("SetPort", config, 4, true));
        Assert.Contains("实际孔位 2", error.Message);
        Assert.Equal(config.RetryCount + 1, native.Sets);
        Assert.Equal(config.RetryCount + 1, native.Reads);
        Assert.Equal(DeviceStatusType.Opened, session.Status);
        native.ReadOverride = null;
        Assert.Equal(4, await session.ExecuteAsync("SetPort", config, 4));
        native.GetCode = -7;
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("GetPort", config));
    }

    [Fact]
    public async Task FailedOpenReleasesEndpointButSuccessfulOpenOwnsItUntilClose()
    {
        var config = Connection();
        var native = new FakeNative { FailOpen = true };
        await using var first = new LocalCfwPortSession(native);
        await using var second = new LocalCfwPortSession(new FakeNative());
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
    public async Task CancellationAndCloseWaitForBlockingNativeCommand()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var native = new FakeNative { OnSet = () => { entered.TrySetResult(); release.Wait(); } };
        await using var session = new LocalCfwPortSession(native);
        var config = Connection();
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
            Assert.Equal(1, native.Closes);
            Assert.Equal(DeviceStatusType.Closed, session.Status);
        }
        finally { release.Set(); }
    }

    [Fact]
    public async Task DisposeDrainsNativeCommandAndRejectsQueuedWork()
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var native = new FakeNative { OnSet = () => { entered.TrySetResult(); release.Wait(); } };
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
        var native = new FakeNative { CloseCode = -1 };
        await using var session = new LocalCfwPortSession(native);
        await using var other = new LocalCfwPortSession(new FakeNative());
        await session.ExecuteAsync("Open", config);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("Close", config));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("Open", config));
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.ExecuteAsync("Open", config));
        await session.DisposeAsync();
        Assert.Equal(1, native.Closes);
        Assert.NotEqual(DeviceStatusType.Closed, session.Status);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task UnsupportedConnectionModesFailBeforeOpeningHardware(bool cvMode, bool hub)
    {
        var native = new FakeNative();
        await using var session = new LocalCfwPortSession(native);
        await Assert.ThrowsAsync<NotSupportedException>(() => session.ExecuteAsync("Open", Connection() with { IsCVMode = cvMode, EnableVSPHub = hub }));
        Assert.Equal(0, native.Opens);
        Assert.Equal(DeviceStatusType.Closed, session.Status);
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

    [Fact]
    public void ConfigurationDraftKeepsLocalPreferenceOutOfServiceConfig()
    {
        var config = new ConfigCfwPort { SzComName = "COM12", BaudRate = 9600 };
        var display = new DisplayCfwPortConfig();
        var draft = CfwPortConfigurationDraft.From(config, display);
        draft.UseLocalCfwPort = true;
        draft.BaudRate = 19200;
        Assert.False(display.UseLocalCfwPort);
        Assert.Equal(9600, config.BaudRate);
        draft.Apply(config, display);
        Assert.True(display.UseLocalCfwPort);
        Assert.Equal(19200, config.BaudRate);
        Assert.DoesNotContain("UseLocalCfwPort", JsonConvert.SerializeObject(config));
        Assert.True(JsonConvert.DeserializeObject<DisplayCfwPortConfig>(JsonConvert.SerializeObject(display))!.UseLocalCfwPort);
    }

    [Fact]
    public async Task ManualFlowAndSpectrumShareLocalConnectionAndCalibrationReservation()
    {
        var native = new FakeNative();
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
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false, ConfigFilePath = Path.Combine(directory, "config.json") });
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
            await Task.Run(() =>
            {
                using var operation = new LocalSpectrumFilterWheel(new ShutterConfig
                { FilterWheelDeviceCode = device!.Code, UseFilterWheel = true, MeasurementPort = 5, DarkPort = 0 });
                operation.SetOpen(false);
                Assert.Equal(0, native.Port);
                operation.SetOpen(true);
                Assert.Equal(5, native.Port);
            });
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
                if (previousConfig != null) ConfigService.SetInstance(previousConfig);
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

    private sealed class FakeNative : ILocalCfwPortNative
    {
        public int Opens, Closes, Sets, Reads, Port, CloseCode, GetCode;
        public int? ReadOverride;
        public bool FailOpen;
        public Action? OnSet;
        public LocalCfwPortConnection? Connection;
        public IntPtr Open(LocalCfwPortConnection config) { Opens++; Connection = config; return FailOpen ? IntPtr.Zero : new IntPtr(17); }
        public int Close(IntPtr handle) { Closes++; return CloseCode; }
        public int SetPort(IntPtr handle, int port) { Sets++; OnSet?.Invoke(); Port = port; return 0; }
        public int GetPort(IntPtr handle, ref int port) { Reads++; port = ReadOverride ?? Port; return GetCode; }
    }
}

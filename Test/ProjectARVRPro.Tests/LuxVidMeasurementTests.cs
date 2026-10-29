using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using cvColorVision;
using ProjectARVRPro.Services;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ProjectARVRPro.Tests;

[Collection(nameof(SocketControlTestGroup))]
public sealed class LuxVidMeasurementTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(DeviceStatusType.Closed, LuxVidMeasurementMode.AutoFocus)]
    [InlineData(DeviceStatusType.Opened, LuxVidMeasurementMode.AutoFocus)]
    [InlineData(DeviceStatusType.Closed, LuxVidMeasurementMode.GetPosition)]
    [InlineData(DeviceStatusType.Opened, LuxVidMeasurementMode.GetPosition)]
    public async Task VidWaitsForOpenAndMeasurementBeforeReturningTheConvertedDistance(DeviceStatusType initialStatus, LuxVidMeasurementMode mode)
    {
        using var camera = new CameraScope(initialStatus, mode);
        Task<double?> measurement = null!;
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => measurement = LuxDeviceMeasurements.MeasureVidAsync(CancellationToken.None));
        if (initialStatus == DeviceStatusType.Closed)
        {
            MsgRecord open = await camera.Service.Opened.Task.WaitAsync(Timeout);
            Assert.False(measurement.IsCompleted);
            Assert.False(camera.Service.Measured.Task.IsCompleted);
            Assert.Equal("VID.TEST.CAMERA", open.MsgSend.Params["CameraID"]);
            Assert.Equal(16, open.MsgSend.Params["Bpp"]);
            camera.Complete(open, MsgRecordState.Success);
        }

        MsgRecord focus = await camera.Service.Measured.Task.WaitAsync(Timeout);
        string measurementEvent = mode == LuxVidMeasurementMode.AutoFocus ? "AutoFocus" : "GetPosition";
        Assert.Equal(initialStatus == DeviceStatusType.Closed ? ["Open", measurementEvent] : [measurementEvent], camera.Service.Commands);
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            focus.MsgReturn = new MsgReturn { EventName = measurementEvent, Code = 102 };
            focus.MsgRecordState = MsgRecordState.Sended;
        });
        Assert.False(measurement.IsCompleted);
        camera.Complete(focus, MsgRecordState.Success, mode == LuxVidMeasurementMode.AutoFocus
            ? new { Position = 999, VidPosition = 123.45 } : (object)new { nPosition = 999, VidPos = 123.45 });
        Assert.Equal(123.45, await measurement.WaitAsync(Timeout));
    }

    [Theory]
    [InlineData(MsgRecordState.Fail)]
    [InlineData(MsgRecordState.Timeout)]
    public async Task FailedOpenReturnsTheLegacyFailureValueWithoutStartingFocus(MsgRecordState state)
    {
        using var camera = new CameraScope(DeviceStatusType.Closed);
        Task<double?> measurement = null!;
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => measurement = LuxDeviceMeasurements.MeasureVidAsync(CancellationToken.None));
        MsgRecord open = await camera.Service.Opened.Task.WaitAsync(Timeout);
        camera.Complete(open, state);
        Assert.Null(await measurement.WaitAsync(Timeout));
        Assert.False(camera.Service.Measured.Task.IsCompleted);
    }

    [Fact]
    public async Task CanceledOpenDoesNotStartFocus()
    {
        using var camera = new CameraScope(DeviceStatusType.Closed);
        using var cancellation = new CancellationTokenSource();
        Task<double?> measurement = null!;
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => measurement = LuxDeviceMeasurements.MeasureVidAsync(cancellation.Token));
        MsgRecord open = await camera.Service.Opened.Task.WaitAsync(Timeout);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => measurement.WaitAsync(Timeout));
        camera.Complete(open, MsgRecordState.Success);
        Assert.False(camera.Service.Measured.Task.IsCompleted);
    }

    // Exercise the production VID method and camera API with controllable command completion, without native devices.
    private sealed class CameraScope : IDisposable
    {
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo ManagerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly ServiceManager? previousManager = ServiceManager.Current;
        private readonly IConfigService previousConfig = ConfigService.Instance;
        private readonly ObservableCollection<TemplateModel<AutoFocusParam>> previousTemplates = TemplateAutoFocus.Params;
        private ServiceManager manager = null!;
        internal FakeCameraService Service { get; private set; } = null!;

        internal CameraScope(DeviceStatusType status, LuxVidMeasurementMode mode = LuxVidMeasurementMode.AutoFocus)
        {
            ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
            {
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                ProjectARVRProConfig.Instance.LuxVidMode = mode;
                TemplateAutoFocus.Params = mode == LuxVidMeasurementMode.AutoFocus
                    ? [new("VID.TEST.FOCUS", new AutoFocusParam { Id = 42, Name = "VID.TEST.FOCUS" })] : [];
                var device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
                device.Config = new ConfigCamera
                {
                    Code = "VID.TEST.DEVICE", CameraID = "VID.TEST.CAMERA",
                    TakeImageMode = TakeImageMode.Measure_Normal, ImageBpp = (ImageBpp)16
                };
                typeof(DeviceCamera).GetField("<CameraBackend>k__BackingField", PrivateInstance)!.SetValue(device, new CameraBackendState(false));
                Service = (FakeCameraService)RuntimeHelpers.GetUninitializedObject(typeof(FakeCameraService));
                Service.Device = device;
                Service.Config = device.Config;
                Service.DeviceStatus = status;
                Service.Commands = [];
                Service.Opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Service.Measured = new(TaskCreationOptions.RunContinuationsAsynchronously);
                device.DService = Service;
                manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
                manager.DeviceServices = [device];
                ManagerField.SetValue(null, manager);
            });
        }

        internal void Complete(MsgRecord record, MsgRecordState state, object? data = null) =>
            ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
            {
                record.MsgReturn = new MsgReturn { EventName = record.MsgSend.EventName, Code = state == MsgRecordState.Success ? 0 : -1, Data = data! };
                record.MsgRecordState = state;
            });

        public void Dispose() => ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            (typeof(ServiceManager).GetField("_copilotAgentExtensionRegistration", PrivateInstance)!.GetValue(manager) as IDisposable)?.Dispose();
            ManagerField.SetValue(null, previousManager);
            TemplateAutoFocus.Params = previousTemplates;
            ConfigService.SetInstance(previousConfig);
        });
    }

    private sealed class FakeCameraService : MQTTCamera
    {
        private FakeCameraService() : base(null!) { }
        public override DeviceStatusType DeviceStatus { get; set; }
        internal List<string> Commands { get; set; } = null!;
        internal TaskCompletionSource<MsgRecord> Opened { get; set; } = null!;
        internal TaskCompletionSource<MsgRecord> Measured { get; set; } = null!;

        internal override MsgRecord PublishAsyncClient(MsgSend msg, double timeout = 30000)
        {
            Commands.Add(msg.EventName);
            var record = new MsgRecord { MsgSend = msg, MsgRecordState = MsgRecordState.Sended };
            (msg.EventName == "Open" ? Opened : Measured).TrySetResult(record);
            return record;
        }
    }
}

using ColorVision.Engine.Messages;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Templates;
using ColorVision.SocketProtocol;
using ColorVision.UI;
using cvColorVision;
using Newtonsoft.Json.Linq;
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
        Assert.Empty(camera.OtherService.Commands);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VidUsesTheFirstCameraWhenNoCameraIsConfigured(bool singleCamera)
    {
        using var camera = new CameraScope(DeviceStatusType.Opened, LuxVidMeasurementMode.GetPosition);
        Task<double?> measurement = null!;
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            if (singleCamera) ServiceManager.GetInstance().DeviceServices.Remove(camera.OtherService.Device);
            ProjectARVRProConfig.Instance.LuxVidCameraCode = string.Empty;
            measurement = LuxDeviceMeasurements.MeasureVidAsync(CancellationToken.None);
        });
        FakeCameraService first = singleCamera ? camera.Service : camera.OtherService;
        FakeCameraService unused = singleCamera ? camera.OtherService : camera.Service;
        MsgRecord command = await first.Measured.Task.WaitAsync(Timeout);
        camera.Complete(command, MsgRecordState.Success, new { VidPos = 123.45 });
        Assert.Equal(123.45, await measurement.WaitAsync(Timeout));
        Assert.Equal(["GetPosition"], first.Commands);
        Assert.Empty(unused.Commands);
    }

    [Theory]
    [InlineData("REMOVED", LuxVidMeasurementMode.GetPosition, "找不到 VID 相机")]
    [InlineData(CameraScope.VidCode, LuxVidMeasurementMode.FineFocus, "仅支持本地相机")]
    public async Task InvalidVidCameraSettingsDoNotIssueDeviceCommands(string code, LuxVidMeasurementMode mode, string expectedError)
    {
        using var camera = new CameraScope(DeviceStatusType.Closed, mode);
        Task<double?> measurement = null!;
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            ProjectARVRProConfig.Instance.LuxVidCameraCode = code;
            camera.Service.Device.DisplayConfig.UseLocalCamera = false;
            measurement = LuxDeviceMeasurements.MeasureVidAsync(CancellationToken.None);
        });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => measurement.WaitAsync(Timeout));
        Assert.Contains(expectedError, error.Message);
        Assert.Empty(camera.OtherService.Commands);
        Assert.Empty(camera.Service.Commands);
    }

    [Fact]
    public async Task FineFocusBindsToTheHostLocalCommandWithoutSendingMqtt()
    {
        using var camera = new CameraScope(DeviceStatusType.Opened, LuxVidMeasurementMode.FineFocus);
        Task<MsgReturn?> completion = null!;
        MsgRecord command = null!;
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            camera.Service.Device.CameraBackend.SetLocalStatus(DeviceStatusType.Opened);
            // Invalid range stops the real host command before it reaches a native camera.
            var template = new AutoFocusParam { MinPosition = 10, MaxPosition = 10 };
            command = LocalVidFocus.ForCamera(camera.Service.Device).Measure(camera.Service.Device, template, CancellationToken.None);
            completion = LuxDeviceMeasurements.WaitForResultAsync(command, CancellationToken.None);
        });
        Assert.Null(await completion.WaitAsync(Timeout));
        Assert.Equal("AutoFocus", command.MsgSend.EventName);
        Assert.Equal(CameraScope.VidCode, command.MsgSend.DeviceCode);
        Assert.Contains("范围下限", command.MsgReturn.Message);
        Assert.Empty(camera.Service.Commands);
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

    [Theory]
    [InlineData(100.0, 0.0, false)]
    [InlineData(110.0, 20.0, true)]
    [InlineData(null, null, false)]
    public async Task JsonVidReturnsOnlyTheFinalMeasurementAndDistinguishesZeroFromFailure(double? raw, double? expected, bool pass)
    {
        using var camera = new CameraScope(DeviceStatusType.Opened, LuxVidMeasurementMode.GetPosition);
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            var config = ProjectARVRProConfig.Instance;
            config.LuxVidFix = 2;
            config.LuxVidB = -200;
            config.LuxVidMin = 10;
        });
        var handler = new MeasureVidSocket(async () =>
        {
            Task<(bool Succeeded, ObjectiveTestItem Item)> measurement = null!;
            ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() => measurement = LuxDeviceMeasurements.MeasureVidResultAsync(CancellationToken.None));
            var (succeeded, item) = await measurement;
            return (false, succeeded ? item : null);
        });
        var request = new SocketRequest { EventName = "MeasureVID", Version = "1.0", MsgID = "vid-001", SerialNumber = "SN001" };
        Task<SocketResponse> response = Task.Run(() => handler.Handle(null!, request));

        MsgRecord command = await camera.Service.Measured.Task.WaitAsync(Timeout);
        Assert.False(response.IsCompleted);
        camera.Complete(command, MsgRecordState.Success, new { VidPos = raw });

        JObject reply = JObject.FromObject(await response.WaitAsync(Timeout));
        Assert.Equal("MeasureVID", reply.Value<string>("EventName"));
        Assert.Equal(request.MsgID, reply.Value<string>("MsgID"));
        Assert.Equal(request.SerialNumber, reply.Value<string>("SerialNumber"));
        Assert.Equal(expected.HasValue ? 0 : -2, reply.Value<int>("Code"));
        if (expected.HasValue)
        {
            Assert.Equal(expected.Value, reply["Data"]!.Value<double>("VID"));
            Assert.Equal(pass, reply["Data"]!.Value<bool>("Pass"));
        }
        else
        {
            Assert.Equal(JTokenType.Null, reply["Data"]!.Type);
        }
        Assert.Equal(["GetPosition"], camera.Service.Commands);
    }

    // Exercise the production VID method and camera API with controllable command completion, without native devices.
    internal sealed class CameraScope : IDisposable
    {
        internal const string VidCode = "VID.TEST.DEVICE";
        internal const string OtherCode = "MAIN.TEST.DEVICE";
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo ManagerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly ServiceManager? previousManager = ServiceManager.Current;
        private readonly IConfigService previousConfig = ConfigService.Instance;
        private readonly ObservableCollection<TemplateModel<AutoFocusParam>> previousTemplates = TemplateAutoFocus.Params;
        private ServiceManager manager = null!;
        internal FakeCameraService Service { get; private set; } = null!;
        internal FakeCameraService OtherService { get; private set; } = null!;

        internal CameraScope(DeviceStatusType status, LuxVidMeasurementMode mode = LuxVidMeasurementMode.AutoFocus)
        {
            ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
            {
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                var config = ProjectARVRProConfig.Instance;
                config.LuxVidMode = mode;
                config.LuxVidCameraCode = VidCode;
                TemplateAutoFocus.Params = mode != LuxVidMeasurementMode.GetPosition
                    ? [new("VID.TEST.FOCUS", new AutoFocusParam { Id = 42, Name = "VID.TEST.FOCUS" })] : [];
                OtherService = CreateCamera(OtherCode, "SV6100_Camera", "MAIN.TEST.CAMERA", status);
                Service = CreateCamera(VidCode, "VID100", "VID.TEST.CAMERA", status);
                manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
                manager.DeviceServices = [OtherService.Device, Service.Device];
                ManagerField.SetValue(null, manager);
            });
        }

        private static FakeCameraService CreateCamera(string code, string name, string cameraId, DeviceStatusType status)
        {
            var device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
            device.SysResourceModel = new SysResourceModel { Code = code, Name = name };
            device.Config = new ConfigCamera
            {
                Code = code, CameraID = cameraId, TakeImageMode = TakeImageMode.Measure_Normal, ImageBpp = (ImageBpp)16
            };
            typeof(DeviceCamera).GetField("<CameraBackend>k__BackingField", PrivateInstance)!.SetValue(device, new CameraBackendState(false));
            var service = (FakeCameraService)RuntimeHelpers.GetUninitializedObject(typeof(FakeCameraService));
            service.Device = device;
            service.Config = device.Config;
            service.DeviceStatus = status;
            service.Commands = [];
            service.Opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
            service.Measured = new(TaskCreationOptions.RunContinuationsAsynchronously);
            device.DService = service;
            return service;
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

    internal sealed class FakeCameraService : MQTTCamera
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

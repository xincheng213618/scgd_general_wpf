using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates;
using cvColorVision;
using MQTTnet;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CameraAutoExposureReplyTests
{
    private static readonly TimeSpan TestDeadline = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RemoteExposureModalDoesNotBlockSubsequentMqttReply(bool currentShape, bool failure)
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        using var modal = new ModalProbe();
        using var camera = WpfTestHost.Invoke(() => new CameraScope(control, modal.Show, currentShape));
        using var other = new MQTTServiceBase(control) { SendTopic = "test/other/cmd", SubscribeTopic = "test/other/status" };
        Assert.True(await control.Connect());
        try
        {
            var (exposure, unrelated) = WpfTestHost.Invoke(() =>
            {
                MsgRecord pending = camera.Service.GetAutoExpTime(new ParamBase { Id = 42, Name = "Exposure" });
                camera.Service.TrackAutoExposureFailure(pending, () => true);
                return (pending, other.PublishAsyncClient(new MsgSend { EventName = "GetData" }));
            });

            // MQTTnet's publish pump awaits each callback before it dispatches the next packet.
            Task serialPump = Task.Run(async () =>
            {
                await client.ReplyAsync(camera.Service.SubscribeTopic, camera.CreateReply(exposure.MsgID, currentShape, failure));
                await client.ReplyAsync(other.SubscribeTopic, new MsgReturn
                {
                    MsgID = unrelated.MsgID, EventName = "GetData", Code = 0,
                });
                await client.ReplyAsync(camera.Service.SubscribeTopic, camera.CreateReply("queued-next", currentShape, failure: false));
            });

            await modal.Entered.Task.WaitAsync(TestDeadline);
            await serialPump.WaitAsync(TestDeadline);
            await WpfTestHost.Invoke(async () => await Dispatcher.Yield(DispatcherPriority.Background));
            Assert.Equal(MsgRecordState.Success, unrelated.MsgRecordState);
            Assert.Equal(failure ? MsgRecordState.Fail : MsgRecordState.Success, exposure.MsgRecordState);
            Assert.Equal(1, modal.Presentations);
            if (failure)
            {
                Assert.Contains("synthetic exposure error", modal.Message);
                Assert.Equal(800d, camera.Scope.Display.ExpTime);
            }
            else if (currentShape)
            {
                Assert.Equal((22d, 33d, 11d), (camera.Scope.Display.ExpTimeR, camera.Scope.Display.ExpTimeG, camera.Scope.Display.ExpTimeB));
                Assert.Equal((0.2, 0.3, 0.1), (camera.Scope.Display.SaturationR, camera.Scope.Display.SaturationG, camera.Scope.Display.SaturationB));
                Assert.Equal(7, camera.Scope.Camera.Config.NDPort);
            }
            else
            {
                Assert.Equal(11d, camera.Scope.Display.ExpTime);
                Assert.Equal(0.1, camera.Scope.Display.Saturation);
                Assert.Equal(4, camera.Scope.Camera.Config.NDPort);
            }
        }
        finally
        {
            modal.Dismiss();
            await WpfTestHost.Invoke(async () => await Dispatcher.Yield(DispatcherPriority.Background));
            await control.DisconnectAsyncClient();
        }

        Assert.Equal(2, modal.Presentations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedExposureDoesNotUpdateRetiredOrLocalCamera(bool switchToLocal)
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        int presentations = 0;
        using var camera = WpfTestHost.Invoke(() => new CameraScope(control, (_, _) => presentations++, currentShape: true));

        await WpfTestHost.Invoke(async () =>
        {
            await client.ReplyAsync(camera.Service.SubscribeTopic, camera.CreateReply("queued", currentShape: true, failure: false));
            if (switchToLocal)
                camera.Scope.Camera.CameraBackend.SetLocalStatus(DeviceStatusType.Opened);
            else
                camera.Service.Dispose();
            await Dispatcher.Yield(DispatcherPriority.Background);
        });

        Assert.Equal(0, presentations);
        Assert.Equal((10d, 20d, 30d), (camera.Scope.Display.ExpTimeR, camera.Scope.Display.ExpTimeG, camera.Scope.Display.ExpTimeB));
        Assert.Equal(4, camera.Scope.Camera.Config.NDPort);
    }

    [Theory]
    [InlineData("Open")]
    [InlineData("Close")]
    [InlineData("GetPort")]
    [InlineData("GetData")]
    public async Task PanelFailureModalDoesNotHoldTheSharedReceivePump(string eventName)
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        using var modal = new ModalProbe();
        using var camera = WpfTestHost.Invoke(() => new CameraScope(control, modal.Show, currentShape: false));
        using var other = new MQTTServiceBase(control) { SendTopic = "test/other/cmd", SubscribeTopic = "test/other/status" };
        Assert.True(await control.Connect());
        try
        {
            var (pending, unrelated) = WpfTestHost.Invoke(() =>
            {
                MsgRecord record = camera.Service.PublishAsyncClient(new MsgSend { EventName = eventName });
                camera.Service.TrackPanelFailure(record, serviceOnly: true, (_, reason) =>
                {
                    modal.Show(reason ?? string.Empty, "ColorVision");
                    return Task.CompletedTask;
                });
                return (record, other.PublishAsyncClient(new MsgSend { EventName = "GetData" }));
            });

            Task pump = Task.Run(async () =>
            {
                await client.ReplyAsync(camera.Service.SubscribeTopic, new MsgReturn { MsgID = pending.MsgID,
                    DeviceCode = camera.Service.DeviceCode, EventName = eventName, Code = -1, Message = "synthetic operation failure" });
                await client.ReplyAsync(other.SubscribeTopic, new MsgReturn { MsgID = unrelated.MsgID, EventName = "GetData", Code = 0 });
            });

            await modal.Entered.Task.WaitAsync(TestDeadline);
            await pump.WaitAsync(TestDeadline);
            Assert.Equal(MsgRecordState.Fail, pending.MsgRecordState);
            Assert.Equal(MsgRecordState.Success, unrelated.MsgRecordState);
            Assert.Equal("synthetic operation failure", modal.Message);
            Assert.Equal(1, modal.Presentations);
        }
        finally
        {
            modal.Dismiss();
            await WpfTestHost.Invoke(async () => await Dispatcher.Yield(DispatcherPriority.Background));
            await control.DisconnectAsyncClient();
        }
    }

    private sealed class CameraScope : IDisposable
    {
        public CurrentCameraDefaultsScope Scope { get; } = new();
        public MQTTCamera Service { get; }

        public CameraScope(MQTTControl control, Action<string, string?> show, bool currentShape)
        {
            DeviceCamera camera = Scope.Camera;
            typeof(DeviceCamera).GetField("<CameraBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(camera, new CameraBackendState(false));
            if (!currentShape) camera.Config.TakeImageMode = TakeImageMode.Live;
            camera.Config.CFW.ChannelCfgs =
            [
                new() { Chtype = ImageChannelType.Gray_Z },
                new() { Chtype = ImageChannelType.Gray_X },
                new() { Chtype = ImageChannelType.Gray_Y },
            ];
            camera.Config.NDPort = 4;
            camera.Config.SendTopic = "test/camera/cmd";
            camera.Config.SubscribeTopic = "test/camera/status";
            Service = camera.DService = new MQTTCamera(camera, control, show);
        }

        public MsgReturn CreateReply(string messageId, bool currentShape, bool failure)
        {
            JArray readings = JArray.FromObject(new[]
            {
                new { result = 11, resultSaturation = 0.1 },
                new { result = 22, resultSaturation = 0.2 },
                new { result = 33, resultSaturation = 0.3 },
            });
            return new MsgReturn
            {
                MsgID = messageId,
                DeviceCode = Service.DeviceCode,
                EventName = "GetAutoExpTime",
                Code = failure ? -1 : 0,
                Message = failure ? "synthetic exposure error" : "ok",
                Data = currentShape ? (JToken)new JObject { ["ExpTime"] = readings, ["NDPort"] = 7 } : readings,
            };
        }

        public void Dispose()
        {
            Service.Dispose();
            Scope.Dispose();
        }
    }

    private sealed class ModalProbe : IDisposable
    {
        private DispatcherFrame? _first;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Presentations { get; private set; }
        public string Message { get; private set; } = string.Empty;

        public void Show(string message, string? caption)
        {
            if (++Presentations != 1)
                return;
            Message = message;
            _first = new DispatcherFrame();
            Entered.TrySetResult();
            Dispatcher.PushFrame(_first);
        }

        public void Dismiss() => WpfTestHost.Invoke(() => { if (_first != null) _first.Continue = false; });
        public void Dispose() => Dismiss();
    }
}

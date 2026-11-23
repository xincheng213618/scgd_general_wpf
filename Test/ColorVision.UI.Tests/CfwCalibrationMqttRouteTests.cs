using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.CfwPort;
using MQTTnet;
using MQTTnet.Packets;
using Newtonsoft.Json;
using System.Reflection;
using System.Text;
using System.Threading.Channels;

namespace ColorVision.UI.Tests;

public sealed class CfwCalibrationMqttRouteTests
{
    private const string OriginalSend = "cfw/original/command";
    private const string OriginalReply = "cfw/original/status";
    private const string ReplacementSend = "cfw/replacement/command";
    private const string ReplacementReply = "cfw/replacement/status";

    [Fact]
    public async Task ConfigurationEditKeepsPendingAndRestoreOnTheAdmittedRoute()
    {
        var (control, transport, service) = CreateService();
        using (service)
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            Assert.True(await control.Connect());
            MQTTCfwPort.CalibrationRoute route = service.CaptureCalibrationRoute();
            var pending = new TaskCompletionSource<MsgRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
            var notifications = new List<string>();
            service.MsgReturnReceived += reply => notifications.Add(reply.MsgID);
            Task worker = Task.Run(() =>
            {
                using var reservation = service.ReserveForCalibration();
                MsgRecord dark = service.SendCalibrationCommand(route, "SetPort", 0);
                pending.SetResult(dark);
                Assert.Equal(MsgRecordState.Success, dark.WorkerCompletion!.WaitAsync(timeout.Token).GetAwaiter().GetResult());
                MsgRecord restore = service.SendCalibrationCommand(route, "SetPort", 5);
                Assert.Equal(MsgRecordState.Success, restore.WorkerCompletion!.WaitAsync(timeout.Token).GetAwaiter().GetResult());
            });

            try
            {
                var darkCommand = await transport.Commands.Reader.ReadAsync(timeout.Token);
                AssertOriginalWire(darkCommand, 0);
                MsgRecord darkRecord = await pending.Task.WaitAsync(timeout.Token);
                await transport.ReplyAsync(OriginalReply, darkCommand.Request, code: 102);
                Assert.Equal(MsgRecordState.Sended, darkRecord.MsgRecordState);
                Assert.Empty(notifications);

                service.Config.SendTopic = ReplacementSend;
                service.Config.SubscribeTopic = ReplacementReply;
                service.Config.ServiceToken = "replacement-token";
                service.Config.Code = "replacement-wheel";
                Assert.True(service.IsCalibrationRouteUsable(route));

                await transport.ReplyAsync(ReplacementReply, darkCommand.Request);
                Assert.Equal(MsgRecordState.Sended, darkRecord.MsgRecordState);
                Assert.Empty(notifications);
                await transport.ReplyAsync(ReplacementReply, new MsgSend { MsgID = "ordinary-notification", EventName = "GetPort" });
                Assert.Equal("ordinary-notification", Assert.Single(notifications));

                await transport.ReplyAsync(OriginalReply, darkCommand.Request);
                var restoreCommand = await transport.Commands.Reader.ReadAsync(timeout.Token);
                AssertOriginalWire(restoreCommand, 5);
                await transport.ReplyAsync(OriginalReply, restoreCommand.Request);
                await worker.WaitAsync(timeout.Token);
                Assert.Single(notifications);
                using var nextReservation = service.ReserveForCalibration();
            }
            finally
            {
                timeout.Cancel();
                try { await worker; }
                catch (OperationCanceledException) { }
                await control.DisconnectAsyncClient();
            }
        }
    }

    [Fact]
    public async Task QueuedCalibrationCannotPublishAfterTheAdmittedConnectionCloses()
    {
        var (control, transport, service) = CreateService();
        using (service)
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            Assert.True(await control.Connect());
            var route = service.CaptureCalibrationRoute();
            var allowDisconnect = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            transport.DisconnectGate = allowDisconnect.Task;
            Task disconnect = control.DisconnectAsyncClient();
            try
            {
                Assert.False(disconnect.IsCompleted);
                Assert.True(service.IsCalibrationRouteUsable(route));
                using (service.ReserveForCalibration())
                {
                    MsgRecord pending = service.SendCalibrationCommand(route, "SetPort", 5);
                    Assert.Equal(MsgRecordState.Sended, pending.MsgRecordState);
                    Assert.False(transport.Commands.Reader.TryRead(out _));

                    allowDisconnect.TrySetResult();
                    await disconnect.WaitAsync(timeout.Token);
                    Assert.Equal(MsgRecordState.Fail, await pending.WorkerCompletion!.WaitAsync(timeout.Token));
                }

                Assert.False(transport.Commands.Reader.TryRead(out _));
                using var nextReservation = service.ReserveForCalibration();
            }
            finally
            {
                allowDisconnect.TrySetResult();
                await disconnect.WaitAsync(timeout.Token);
            }
        }
    }

    [Fact]
    public async Task ReconnectedTransportCannotClaimTheOldCalibrationLease()
    {
        var (control, transport, service) = CreateService();
        using (service)
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            try
            {
                Assert.True(await control.Connect());
                var route = service.CaptureCalibrationRoute();
                using (service.ReserveForCalibration())
                {
                    MsgRecord pending = service.SendCalibrationCommand(route, "GetPort");
                    var command = await transport.Commands.Reader.ReadAsync(timeout.Token);
                    await control.DisconnectAsyncClient();
                    Assert.False(service.IsCalibrationRouteUsable(route));
                    Assert.True(await control.Connect());
                    Assert.False(service.IsCalibrationRouteUsable(route));
                    await transport.ReplyAsync(OriginalReply, command.Request);
                    Assert.Equal(MsgRecordState.Fail, await pending.WorkerCompletion!.WaitAsync(timeout.Token));
                }

                using var nextReservation = service.ReserveForCalibration();
                Assert.Throws<InvalidOperationException>(() => service.SendCalibrationCommand(route, "SetPort", 5));
                Assert.False(transport.Commands.Reader.TryRead(out _));
                var newRoute = service.CaptureCalibrationRoute();
                Assert.True(service.IsCalibrationRouteUsable(newRoute));
                service.Dispose();
                Assert.False(service.IsCalibrationRouteUsable(newRoute));
            }
            finally { await control.DisconnectAsyncClient(); }
        }
    }

    private static void AssertOriginalWire((string Topic, MsgSend Request) command, int expectedPort)
    {
        Assert.Equal(OriginalSend, command.Topic);
        Assert.Equal(OriginalSend, command.Request.ServiceName);
        Assert.Equal("original-wheel", command.Request.DeviceCode);
        Assert.Equal("original-token", command.Request.Token);
        Assert.Equal("SetPort", command.Request.EventName);
        Assert.Equal(expectedPort, ((Newtonsoft.Json.Linq.JObject)command.Request.Params).Value<int>("PortNum"));
    }

    private static (MQTTControl Control, CalibrationTransport Transport, MQTTCfwPort Service) CreateService()
    {
        IMqttClient client = DispatchProxy.Create<IMqttClient, CalibrationTransport>();
        var control = new MQTTControl(client);
        var service = new MQTTCfwPort(new ConfigCfwPort
        {
            Code = "original-wheel", SendTopic = OriginalSend, SubscribeTopic = OriginalReply, ServiceToken = "original-token",
        }, control);
        return (control, (CalibrationTransport)(object)client, service);
    }

    public class CalibrationTransport : MQTTControlTests.ClientProxy
    {
        private Func<MqttApplicationMessageReceivedEventArgs, Task>? messageReceived;
        internal Task? DisconnectGate { get; set; }
        internal Task? PublishGate { get; set; }
        internal Action<MsgSend>? PublishStarted { get; set; }
        internal Channel<(string Topic, MsgSend Request)> Commands { get; } = Channel.CreateUnbounded<(string, MsgSend)>();

        internal Task ReplyAsync(string topic, MsgSend request, int code = 0)
        {
            string payload = JsonConvert.SerializeObject(new MsgReturn
            {
                MsgID = request.MsgID, EventName = request.EventName, DeviceCode = request.DeviceCode, Code = code,
            });
            var message = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payload).Build();
            return messageReceived!(new("cfw-calibration-test", message, new MqttPublishPacket(), (_, _) => Task.CompletedTask));
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod!.Name)
            {
                case "add_ApplicationMessageReceivedAsync":
                    messageReceived += (Func<MqttApplicationMessageReceivedEventArgs, Task>)args![0]!;
                    return null;
                case "PublishAsync":
                    var message = (MqttApplicationMessage)args![0]!;
                    var request = JsonConvert.DeserializeObject<MsgSend>(Encoding.UTF8.GetString(message.Payload))!;
                    PublishStarted?.Invoke(request);
                    Commands.Writer.TryWrite((message.Topic, request));
                    return WaitForPublishAsync(PublishGate);
                case "DisconnectAsync" when DisconnectGate is { } gate:
                    return DisconnectAfterGateAsync(targetMethod, args, gate);
                default:
                    return base.Invoke(targetMethod, args);
            }
        }

        private static async Task<MqttClientPublishResult> WaitForPublishAsync(Task? gate)
        {
            if (gate != null) await gate;
            return null!;
        }

        private async Task DisconnectAfterGateAsync(MethodInfo method, object?[]? args, Task gate)
        {
            await gate;
            await (Task)base.Invoke(method, args)!;
        }
    }
}

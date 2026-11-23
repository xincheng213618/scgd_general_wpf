using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using MQTTnet;
using MQTTnet.Packets;
using Newtonsoft.Json;
using System.Reflection;

namespace ColorVision.UI.Tests;

public class MQTTServiceBaseTests
{
    [Theory]
    [InlineData(0, MsgRecordState.Success)]
    [InlineData(500, MsgRecordState.Fail)]
    public async Task ProgressRemainsPendingUntilTheFirstTerminalResponse(int terminalCode, MsgRecordState terminalState)
    {
        var (service, client) = CreateService();
        using (service)
        {
            var notifications = new List<int>();
            service.MsgReturnReceived += message => notifications.Add(message.Code);
            var transitions = new List<MsgRecordState>();
            MsgRecord record = WpfTestHost.Invoke(() =>
            {
                MsgRecord pending = service.PublishAsyncClient(new MsgSend { EventName = "AutoFocus" });
                pending.MsgRecordStateChanged += (_, state) => transitions.Add(state);
                return pending;
            });

            await client.ReceiveAsync(service.SubscribeTopic, record.MsgID, 102);
            await client.ReceiveAsync(service.SubscribeTopic, record.MsgID, 102);

            Assert.Equal(MsgRecordState.Sended, record.MsgRecordState);
            Assert.Null(record.MsgReturn);
            Assert.Empty(transitions);

            await client.ReceiveAsync(service.SubscribeTopic, record.MsgID, terminalCode);
            await client.ReceiveAsync(service.SubscribeTopic, record.MsgID, 501);

            Assert.Equal(terminalState, record.MsgRecordState);
            Assert.Equal(terminalCode, Assert.IsType<MsgReturn>(record.MsgReturn).Code);
            Assert.Equal(new[] { terminalState }, transitions);
            Assert.Equal(new[] { 102, 102, terminalCode, 501 }, notifications);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TerminalNotificationsAllowDisposalOnAnotherThread(bool receiveResponse)
    {
        var (service, client) = CreateService();
        using (service)
        {
            using var disposalFinished = new ManualResetEventSlim();
            var notification = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task? disposal = null;
            var (record, outstanding) = WpfTestHost.Invoke(() =>
            {
                MsgRecord queued = service.PublishAsyncClient(new MsgSend { EventName = "GetData" });
                MsgRecord pending = service.PublishAsyncClient(new MsgSend { EventName = "GetData" }, receiveResponse ? 5000 : 100);
                pending.MsgRecordStateChanged += (_, _) =>
                {
                    disposal = Task.Run(() =>
                    {
                        service.Dispose();
                        disposalFinished.Set();
                    });
                    notification.SetResult(disposalFinished.Wait(TimeSpan.FromSeconds(2)));
                };
                return (pending, queued);
            });

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task<MsgRecordState> waiting = ScheduledDeviceJobHelper.WaitForTerminalStateAsync(outstanding, Timeout.InfiniteTimeSpan, cancellation.Token);

            if (receiveResponse)
                await client.ReceiveAsync(service.SubscribeTopic, record.MsgID, 0);

            bool disposedDuringNotification = await notification.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await disposal!;
            Assert.True(disposedDuringNotification);
            Assert.Equal(receiveResponse ? MsgRecordState.Success : MsgRecordState.Timeout, record.MsgRecordState);
            Assert.Equal(MsgRecordState.Fail, await waiting);
        }
    }

    [Fact]
    public async Task DisposingPendingCommandSettlesSchedulerWithoutAResponse()
    {
        var (service, client) = CreateService();
        using (service)
        {
            var transitions = new List<MsgRecordState>();
            MsgRecord record = WpfTestHost.Invoke(() =>
            {
                MsgRecord pending = service.PublishAsyncClient(new MsgSend { EventName = "GetData" });
                pending.MsgRecordStateChanged += (_, _) => throw new InvalidOperationException("Faulting consumer.");
                pending.MsgRecordStateChanged += (_, state) => transitions.Add(state);
                return pending;
            });
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task<MsgRecordState> waiting = ScheduledDeviceJobHelper.WaitForTerminalStateAsync(record, Timeout.InfiniteTimeSpan, cancellation.Token);
            Assert.False(waiting.IsCompleted);

            await Task.Run(service.Dispose);
            service.Dispose();
            await client.ReceiveAsync(service.SubscribeTopic, record.MsgID, 0);

            Assert.Equal(MsgRecordState.Fail, await waiting);
            Assert.Equal(new[] { MsgRecordState.Fail }, transitions);
            Assert.Equal(MsgRecordState.Fail, record.MsgRecordState);
            Assert.Equal(record.MsgID, record.MsgReturn.MsgID);
            Assert.Equal(-1, record.MsgReturn.Code);
            Assert.False(string.IsNullOrWhiteSpace(record.MsgReturn.Message));
            Assert.Equal(default, record.ReciveTime);
        }
    }

    private static (MQTTServiceBase Service, MessageClientProxy Client) CreateService()
    {
        IMqttClient client = DispatchProxy.Create<IMqttClient, MessageClientProxy>();
        var service = new MQTTServiceBase(new MQTTControl(client)) { SendTopic = "test/cmd", SubscribeTopic = "test/status" };
        return (service, (MessageClientProxy)(object)client);
    }

    public class MessageClientProxy : DispatchProxy
    {
        private Func<MqttApplicationMessageReceivedEventArgs, Task>? messageReceived;

        public Task ReceiveAsync(string topic, string messageId, int code)
        {
            string payload = JsonConvert.SerializeObject(new MsgReturn { EventName = "AutoFocus", MsgID = messageId, Code = code });
            var message = new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payload).Build();
            return messageReceived!(new("test", message, new MqttPublishPacket(), (_, _) => Task.CompletedTask));
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod!.Name)
            {
                case "add_ApplicationMessageReceivedAsync":
                    messageReceived += (Func<MqttApplicationMessageReceivedEventArgs, Task>)args![0]!;
                    return null;
                case "add_ConnectedAsync":
                case "add_DisconnectedAsync":
                    return null;
                case "get_IsConnected":
                    return false;
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }
    }
}

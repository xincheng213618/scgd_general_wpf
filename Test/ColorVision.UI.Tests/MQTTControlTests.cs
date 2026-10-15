using System.Reflection;
using System.Net;
using ColorVision.Engine.MQTT;
using MQTTnet;

namespace ColorVision.UI.Tests;

public class MQTTControlTests
{
    [Fact]
    public async Task ConcurrentConnectionsAndSubscriptionsReuseOneClient()
    {
        var (control, client) = CreateControl();
        client.ConnectGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        control.SubscribeCache("test/status");
        Task<bool> first = control.Connect();
        Task<bool> second = control.Connect();
        Task subscribe = control.SubscribeAsyncClientAsync("test/status");
        client.ConnectGate.SetResult(true);

        await Task.WhenAll(first, second, subscribe);

        Assert.True(await first);
        Assert.True(await second);
        Assert.Equal(1, client.ConnectCalls);
        Assert.Single(client.Subscriptions);
        var tcp = Assert.IsType<MqttClientTcpOptions>(client.Options!.ChannelOptions);
        var endpoint = Assert.IsType<DnsEndPoint>(tcp.RemoteEndpoint);
        Assert.Equal("127.0.0.1", endpoint.Host);
        Assert.Equal(1883, endpoint.Port);
        Assert.Null(client.Options.Credentials);
        Assert.Equal(0, client.DisconnectCalls);
        await control.DisconnectAsyncClient();
        Assert.Equal(0, client.DisposeCalls);
    }

    [Fact]
    public async Task RecoveryRetriesRejectedSubscriptionWithoutDisconnecting()
    {
        var (control, client) = CreateControl();
        control.SubscribeCache("test/status");
        client.RejectSubscription = true;
        Assert.False(await control.RecoverConnectionAsync());
        Assert.Empty(control.GetSubscribeTopicSnapshot());
        Assert.Equal(1, control.CaptureRuntimeDiagnostics().RegisteredSubscriptionCount);

        client.RejectSubscription = false;
        Assert.True(await control.RecoverConnectionAsync());

        Assert.Equal(1, client.ConnectCalls);
        Assert.Equal(0, client.DisconnectCalls);
        Assert.Single(control.GetSubscribeTopicSnapshot());
        await control.DisconnectAsyncClient();
    }

    [Fact]
    public async Task DisconnectClearsActiveTopicsAndOfflineUnsubscribePreventsRestoration()
    {
        var (control, client) = CreateControl();
        control.SubscribeCache("test/keep");
        control.SubscribeCache("test/remove");
        Assert.True(await control.Connect());
        await control.DisconnectAsyncClient();
        Assert.Empty(control.GetSubscribeTopicSnapshot());
        await control.UnsubscribeAsyncClientAsync("test/remove");

        Assert.True(await control.Connect());

        Assert.Equal(new[] { "test/keep" }, control.GetSubscribeTopicSnapshot());
        Assert.Equal(2, client.Subscriptions.Count(topic => topic == "test/keep"));
        Assert.Equal(1, client.Subscriptions.Count(topic => topic == "test/remove"));
        Assert.Equal(2, client.ConnectCalls);
        await control.DisconnectAsyncClient();
    }

    [Fact]
    public async Task CanceledRecoveryLeavesSubscriptionPendingForNextAttempt()
    {
        var (control, client) = CreateControl();
        Assert.True(await control.Connect());
        client.SubscribeGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        client.RejectSubscription = true;
        await control.SubscribeAsyncClientAsync("test/status");
        client.RejectSubscription = false;
        using CancellationTokenSource cancellation = new();
        Task<bool> recovery = control.RecoverConnectionAsync(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recovery);
        Assert.Empty(control.GetSubscribeTopicSnapshot());
        client.SubscribeGate = null;
        Assert.True(await control.RecoverConnectionAsync());
        Assert.Equal(1, client.ConnectCalls);
        await control.DisconnectAsyncClient();
    }

    [Fact]
    public async Task ReconnectingAfterExplicitStopReusesClientAndNotifiesOnlyStateChanges()
    {
        var (control, client) = CreateControl();
        List<bool> states = [];
        control.MQTTConnectChanged += (_, _) => states.Add(control.IsConnect);
        Assert.True(await control.Connect());
        Assert.True(await control.Connect());
        await control.DisconnectAsyncClient();
        Assert.True(await control.Connect());

        Assert.Equal(new[] { true, false, true }, states);
        Assert.Equal(2, client.ConnectCalls);
        Assert.Equal(1, client.DisconnectCalls);
        Assert.Equal(0, client.DisposeCalls);
        await control.DisconnectAsyncClient();
    }

    private static (MQTTControl Control, ClientProxy Client) CreateControl()
    {
        IMqttClient client = DispatchProxy.Create<IMqttClient, ClientProxy>();
        return (new MQTTControl(client), (ClientProxy)(object)client);
    }

    public class ClientProxy : DispatchProxy
    {
        private Func<MqttClientConnectedEventArgs, Task>? _connected;
        private Func<MqttClientDisconnectedEventArgs, Task>? _disconnected;
        public bool IsConnected { get; private set; }
        public MqttClientOptions? Options { get; private set; }
        public TaskCompletionSource<bool>? ConnectGate { get; set; }
        public TaskCompletionSource<bool>? SubscribeGate { get; set; }
        public bool RejectSubscription { get; set; }
        public List<string> Subscriptions { get; } = [];
        public int ConnectCalls { get; private set; }
        public int DisconnectCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod!.Name)
            {
                case "get_IsConnected": return IsConnected;
                case "get_Options": return Options;
                case "add_ConnectedAsync": _connected += (Func<MqttClientConnectedEventArgs, Task>)args![0]!; return null;
                case "add_DisconnectedAsync": _disconnected += (Func<MqttClientDisconnectedEventArgs, Task>)args![0]!; return null;
                case "add_ApplicationMessageReceivedAsync": return null;
                case "ConnectAsync": return ConnectAsync((MqttClientOptions)args![0]!, (CancellationToken)args[1]!);
                case "SubscribeAsync": return SubscribeAsync((MqttClientSubscribeOptions)args![0]!, (CancellationToken)args[1]!);
                case "UnsubscribeAsync": return Task.FromResult<MqttClientUnsubscribeResult>(null!);
                case "DisconnectAsync": return DisconnectAsync();
                case "Dispose": DisposeCalls++; return null;
                default: throw new NotSupportedException(targetMethod.Name);
            }
        }

        private async Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken)
        {
            ConnectCalls++;
            Options = options;
            if (ConnectGate != null) await ConnectGate.Task.WaitAsync(cancellationToken);
            IsConnected = true;
            var result = new MqttClientConnectResult();
            if (_connected != null) await _connected(new(result));
            return result;
        }

        private async Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken)
        {
            if (!RejectSubscription && SubscribeGate != null) await SubscribeGate.Task.WaitAsync(cancellationToken);
            var topic = Assert.Single(options.TopicFilters);
            Subscriptions.Add(topic.Topic);
            var code = RejectSubscription ? MqttClientSubscribeResultCode.NotAuthorized : MqttClientSubscribeResultCode.GrantedQoS0;
            return new(1, [new(topic, code)], string.Empty, []);
        }

        private async Task DisconnectAsync()
        {
            DisconnectCalls++;
            IsConnected = false;
            if (_disconnected != null) await _disconnected(null!);
        }
    }
}

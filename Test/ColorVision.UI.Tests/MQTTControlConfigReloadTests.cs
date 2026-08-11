using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using MQTTnet;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class MQTTControlConfigReloadTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        $"ColorVisionMqttConfigReload-{Guid.NewGuid():N}");

    public MQTTControlConfigReloadTests()
    {
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public async Task LoadConfigs_RetiresC1ClientAndReconnectsWithC2Snapshot()
    {
        using var configScope = CreateConfigScope();
        WriteMqttConfig(configScope.Handler.ConfigFilePath, "mqtt-c1", 1883, "user-c1", "password-c1");
        AssertSuccessful(configScope.Handler.LoadConfigsWithResult());

        var clientFactory = new TrackingMqttClientFactory();
        var mqttControl = new MQTTControl(clientFactory.CreateClient);
        mqttControl.ApplicationMessageReceivedAsync += _ =>
        {
            clientFactory.ForwardedMessageCount++;
            return Task.CompletedTask;
        };
        AssertSuccessful(configScope.Handler.RegisterReloadParticipants(mqttControl));

        try
        {
            MQTTSetting c1Setting = MQTTSetting.Instance;
            Assert.True(await mqttControl.Connect());
            TrackingMqttClientProxy c1Client = clientFactory.LastClient;
            AssertConnectionOptions(c1Client, "mqtt-c1", 1883, "user-c1", "password-c1");
            Assert.True(mqttControl.IsConnect);

            // Start C1's delayed reconnect before importing C2. Bind must cancel this exact
            // in-flight callback, then reconnect only the new client with the C2 snapshot.
            Task c1ReconnectAttempt = c1Client.InvokeCapturedDisconnectedHandlerAsync();
            Assert.False(mqttControl.IsConnect);

            WriteMqttConfig(configScope.Handler.ConfigFilePath, "mqtt-c2", 2884, "user-c2", "password-c2");
            ConfigReloadResult c2Load = configScope.Handler.LoadConfigsWithResult();
            Task<bool> c2Reconnect = mqttControl.CurrentConfigBindTask;
            TrackingMqttClientProxy c2Client = clientFactory.LastClient;

            AssertSuccessful(c2Load);
            Assert.NotSame(c1Setting, MQTTSetting.Instance);
            Assert.Equal("mqtt-c2", MQTTControl.Config.Host);
            Assert.Equal(2884, MQTTControl.Config.Port);

            // BindCurrentConfig is synchronous: C1 is already unable to deliver events when LoadConfigs returns.
            Assert.Equal(1, c1Client.DisposeCalls);
            Assert.Equal(1, c1Client.ConnectedHandlerRemoveCount);
            Assert.Equal(1, c1Client.DisconnectedHandlerRemoveCount);
            Assert.Equal(1, c1Client.ApplicationMessageHandlerRemoveCount);

            await c1ReconnectAttempt.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await c2Reconnect.WaitAsync(TimeSpan.FromSeconds(5)));
            AssertConnectionOptions(c2Client, "mqtt-c2", 2884, "user-c2", "password-c2");
            Assert.True(mqttControl.IsConnect);

            int createdClientCount = clientFactory.CreatedClientCount;
            await c1Client.InvokeCapturedConnectedHandlerAsync();
            await c1Client.InvokeCapturedDisconnectedHandlerAsync();
            await c1Client.InvokeCapturedApplicationMessageHandlerAsync();

            Assert.Equal(createdClientCount, clientFactory.CreatedClientCount);
            Assert.Equal(0, clientFactory.ForwardedMessageCount);
            Assert.True(mqttControl.IsConnect);

            c1Setting.MQTTConfig.Host = "mutated-c1";
            c1Setting.MQTTConfig.Port = 3885;
            c1Setting.MQTTConfig.UserName = "mutated-user-c1";
            c1Setting.MQTTConfig.UserPwd = "mutated-password-c1";

            Assert.False(await mqttControl.Connect(c1Setting.MQTTConfig));
            Assert.Equal(createdClientCount, clientFactory.CreatedClientCount);

            Assert.True(await mqttControl.Connect());
            AssertConnectionOptions(clientFactory.LastClient, "mqtt-c2", 2884, "user-c2", "password-c2");
        }
        finally
        {
            await mqttControl.DisconnectAsyncClient();
        }
    }

    [Fact]
    public async Task ConnectCapturedBeforeC2Bind_CannotInstallC1AfterReload()
    {
        using var configScope = CreateConfigScope();
        WriteMqttConfig(configScope.Handler.ConfigFilePath, "mqtt-c1", 1883, "user-c1", "password-c1");
        AssertSuccessful(configScope.Handler.LoadConfigsWithResult());

        var clientFactory = new TrackingMqttClientFactory();
        var mqttControl = new MQTTControl(clientFactory.CreateClient);
        AssertSuccessful(configScope.Handler.RegisterReloadParticipants(mqttControl));
        MQTTSetting c1Setting = MQTTSetting.Instance;

        using var connectCaptured = new ManualResetEventSlim();
        using var continueConnect = new ManualResetEventSlim();
        mqttControl.BeforeConnectTransition = () =>
        {
            connectCaptured.Set();
            if (!continueConnect.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The test did not release the captured C1 connect.");
        };

        try
        {
            Task<bool> c1Connect = Task.Run(async () => await mqttControl.Connect());
            Assert.True(connectCaptured.Wait(TimeSpan.FromSeconds(5)));
            int clientsBeforeC2 = clientFactory.CreatedClientCount;

            WriteMqttConfig(configScope.Handler.ConfigFilePath, "mqtt-c2", 2884, "user-c2", "password-c2");
            AssertSuccessful(configScope.Handler.LoadConfigsWithResult());
            TrackingMqttClientProxy c2BoundClient = clientFactory.LastClient;

            continueConnect.Set();
            Assert.False(await c1Connect.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(clientsBeforeC2 + 1, clientFactory.CreatedClientCount);
            Assert.Same(c2BoundClient.Client, mqttControl.MQTTClient);

            // A caller that retained the C1 object is rejected even after the bind completed.
            Assert.False(await mqttControl.Connect(c1Setting.MQTTConfig));
            Assert.Equal(clientsBeforeC2 + 1, clientFactory.CreatedClientCount);

            mqttControl.BeforeConnectTransition = null;
            Assert.True(await mqttControl.Connect());
            AssertConnectionOptions(clientFactory.LastClient, "mqtt-c2", 2884, "user-c2", "password-c2");
        }
        finally
        {
            continueConnect.Set();
            mqttControl.BeforeConnectTransition = null;
            await mqttControl.DisconnectAsyncClient();
        }
    }

    [Fact]
    public async Task RetainedC1EditorOwner_CannotWriteBackOrReconnectC1AfterReload()
    {
        using var configScope = CreateConfigScope();
        WriteMqttConfig(configScope.Handler.ConfigFilePath, "mqtt-c1", 1883, "user-c1", "password-c1");
        AssertSuccessful(configScope.Handler.LoadConfigsWithResult());

        var clientFactory = new TrackingMqttClientFactory();
        var mqttControl = new MQTTControl(clientFactory.CreateClient);
        AssertSuccessful(configScope.Handler.RegisterReloadParticipants(mqttControl));
        MQTTControl.MqttConfigOwnerIdentity c1Owner = mqttControl.CaptureCurrentConfigOwner();
        MQTTConfig c1Config = c1Owner.Setting.MQTTConfig;

        try
        {
            WriteMqttConfig(configScope.Handler.ConfigFilePath, "mqtt-c2", 2884, "user-c2", "password-c2");
            AssertSuccessful(configScope.Handler.LoadConfigsWithResult());
            MQTTControl.MqttConfigOwnerIdentity c2Owner = mqttControl.CaptureCurrentConfigOwner();
            MQTTConfig c2Config = c2Owner.Setting.MQTTConfig;
            int clientsAfterC2Bind = clientFactory.CreatedClientCount;

            Assert.NotSame(c1Owner, c2Owner);
            Assert.True(c2Owner.Generation > c1Owner.Generation);

            // This is the old window's SelectionChanged path. Its C1 owner is no longer
            // authorized to replace the current C2 setting's selected configuration.
            Assert.False(mqttControl.TrySelectCurrentConfig(c1Owner, c1Config));
            Assert.Same(c2Config, MQTTSetting.Instance.MQTTConfig);

            // Reproduce the historical bypass directly: even if stale code writes C1 into the
            // public setting property, that property is no longer the connection authority.
            MQTTSetting.Instance.MQTTConfig = c1Config;
            Assert.False(await mqttControl.ConnectOwnedConfig(c1Owner, c1Config));
            Assert.False(await mqttControl.TestConnectOwnedConfig(c1Owner, c1Config));
            Assert.False(await mqttControl.Connect(c1Config));
            Assert.Equal(clientsAfterC2Bind, clientFactory.CreatedClientCount);

            Assert.True(mqttControl.TrySelectCurrentConfig(c2Owner, c2Config));
            Assert.True(await mqttControl.ConnectOwnedConfig(c2Owner, c2Config));
            AssertConnectionOptions(clientFactory.LastClient, "mqtt-c2", 2884, "user-c2", "password-c2");
        }
        finally
        {
            await mqttControl.DisconnectAsyncClient();
        }
    }

    [Theory]
    [InlineData(BindFailurePoint.Factory)]
    [InlineData(BindFailurePoint.EventAttach)]
    [InlineData(BindFailurePoint.IsConnectedGetter)]
    public async Task BindSynchronousFailure_IsAggregatedAndLeavesNoC1OrPartialC2Client(
        BindFailurePoint failurePoint)
    {
        using var configScope = CreateConfigScope();
        WriteMqttConfig(configScope.Handler.ConfigFilePath, "mqtt-c1", 1883, "user-c1", "password-c1");
        AssertSuccessful(configScope.Handler.LoadConfigsWithResult());

        var clientFactory = new TrackingMqttClientFactory();
        var mqttControl = new MQTTControl(clientFactory.CreateClient);
        AssertSuccessful(configScope.Handler.RegisterReloadParticipants(mqttControl));
        TrackingMqttClientProxy c1Client = clientFactory.LastClient;
        int clientsBeforeFailure = clientFactory.CreatedClientCount;

        switch (failurePoint)
        {
            case BindFailurePoint.Factory:
                clientFactory.FailNextCreate(new InvalidOperationException("factory failed"));
                break;
            case BindFailurePoint.EventAttach:
                clientFactory.ConfigureNextClient(client => client.ThrowOnDisconnectedHandlerAdd = true);
                break;
            case BindFailurePoint.IsConnectedGetter:
                c1Client.ThrowOnIsConnectedGet = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failurePoint));
        }

        try
        {
            WriteMqttConfig(configScope.Handler.ConfigFilePath, "mqtt-c2", 2884, "user-c2", "password-c2");
            ConfigReloadResult c2Load = configScope.Handler.LoadConfigsWithResult();

            Assert.False(c2Load.Succeeded);
            Assert.Contains(nameof(MQTTControl), c2Load.BuildFailureSummary());
            Assert.Equal("mqtt-c2", MQTTControl.Config.Host);
            Assert.Equal(1, c1Client.DisposeCalls);
            Assert.Equal(1, c1Client.ConnectedHandlerRemoveCount);
            Assert.Equal(1, c1Client.DisconnectedHandlerRemoveCount);
            Assert.Equal(1, c1Client.ApplicationMessageHandlerRemoveCount);
            Assert.Null(mqttControl.MQTTClient);
            Assert.False(mqttControl.IsConnect);
            Assert.False(await mqttControl.CurrentConfigBindTask);

            await c1Client.InvokeCapturedConnectedHandlerAsync();
            await c1Client.InvokeCapturedDisconnectedHandlerAsync();
            await c1Client.InvokeCapturedApplicationMessageHandlerAsync();
            Assert.False(mqttControl.IsConnect);

            if (failurePoint == BindFailurePoint.Factory)
            {
                Assert.Equal(clientsBeforeFailure, clientFactory.CreatedClientCount);
            }
            else
            {
                TrackingMqttClientProxy partialC2Client = clientFactory.LastClient;
                Assert.NotSame(c1Client, partialC2Client);
                Assert.Equal(1, partialC2Client.DisposeCalls);
                Assert.Equal(1, partialC2Client.ConnectedHandlerRemoveCount);
                Assert.Equal(1, partialC2Client.DisconnectedHandlerRemoveCount);
                Assert.Equal(1, partialC2Client.ApplicationMessageHandlerRemoveCount);
                await partialC2Client.InvokeCapturedConnectedHandlerAsync();
                Assert.False(mqttControl.IsConnect);
            }

            // The failed bind never restores C1. A later explicit attempt captures only current C2.
            c1Client.ThrowOnIsConnectedGet = false;
            Assert.True(await mqttControl.Connect());
            AssertConnectionOptions(clientFactory.LastClient, "mqtt-c2", 2884, "user-c2", "password-c2");
        }
        finally
        {
            await mqttControl.DisconnectAsyncClient();
        }
    }

    [Fact]
    public async Task ConnectionStateSubscriberFailure_DoesNotBlockC2SwapOrReconnect()
    {
        using var configScope = CreateConfigScope();
        WriteMqttConfig(configScope.Handler.ConfigFilePath, "mqtt-c1", 1883, "user-c1", "password-c1");
        AssertSuccessful(configScope.Handler.LoadConfigsWithResult());

        var clientFactory = new TrackingMqttClientFactory();
        var mqttControl = new MQTTControl(clientFactory.CreateClient);
        AssertSuccessful(configScope.Handler.RegisterReloadParticipants(mqttControl));

        int successfulSubscriberCalls = 0;
        mqttControl.MQTTConnectChanged += (_, _) => throw new InvalidOperationException("subscriber failed");
        mqttControl.MQTTConnectChanged += (_, _) => successfulSubscriberCalls++;
        mqttControl.PropertyChanged += (_, _) => throw new InvalidOperationException("property subscriber failed");

        try
        {
            Assert.True(await mqttControl.Connect());

            WriteMqttConfig(configScope.Handler.ConfigFilePath, "mqtt-c2", 2884, "user-c2", "password-c2");
            ConfigReloadResult c2Load = configScope.Handler.LoadConfigsWithResult();
            TrackingMqttClientProxy c2Client = clientFactory.LastClient;

            AssertSuccessful(c2Load);
            Assert.True(await mqttControl.CurrentConfigBindTask.WaitAsync(TimeSpan.FromSeconds(5)));
            AssertConnectionOptions(c2Client, "mqtt-c2", 2884, "user-c2", "password-c2");
            Assert.True(mqttControl.IsConnect);
            Assert.Equal(3, successfulSubscriberCalls);
        }
        finally
        {
            await mqttControl.DisconnectAsyncClient();
        }
    }

    [Fact]
    public async Task PublicMqttClientSetter_UpdatesTheSingleBindingAndFailsClosed()
    {
        var clientFactory = new TrackingMqttClientFactory();
        var mqttControl = new MQTTControl(clientFactory.CreateClient);
        TrackingMqttClientProxy originalClient = clientFactory.LastClient;

        try
        {
            _ = clientFactory.CreateClient();
            TrackingMqttClientProxy replacementClient = clientFactory.LastClient;
            mqttControl.MQTTClient = replacementClient.Client;

            Assert.Same(replacementClient.Client, mqttControl.MQTTClient);
            Assert.Equal(1, originalClient.DisposeCalls);
            Assert.Equal(1, originalClient.ConnectedHandlerRemoveCount);
            Assert.Equal(1, originalClient.DisconnectedHandlerRemoveCount);
            Assert.Equal(1, originalClient.ApplicationMessageHandlerRemoveCount);
            Assert.Equal(1, replacementClient.ConnectedHandlerAddCount);
            Assert.Equal(1, replacementClient.DisconnectedHandlerAddCount);
            Assert.Equal(1, replacementClient.ApplicationMessageHandlerAddCount);

            await originalClient.InvokeCapturedConnectedHandlerAsync();
            Assert.False(mqttControl.IsConnect);
            await replacementClient.InvokeCapturedConnectedHandlerAsync();
            Assert.True(mqttControl.IsConnect);

            mqttControl.MQTTClient = replacementClient.Client;
            Assert.Equal(0, replacementClient.DisposeCalls);

            _ = clientFactory.CreateClient();
            TrackingMqttClientProxy failingClient = clientFactory.LastClient;
            failingClient.ThrowOnIsConnectedGet = true;

            Assert.Throws<InvalidOperationException>(() => mqttControl.MQTTClient = failingClient.Client);
            Assert.Equal(1, replacementClient.DisposeCalls);
            Assert.Equal(1, failingClient.DisposeCalls);
            Assert.Null(mqttControl.MQTTClient);
            Assert.False(mqttControl.IsConnect);

            await replacementClient.InvokeCapturedConnectedHandlerAsync();
            Assert.False(mqttControl.IsConnect);
        }
        finally
        {
            await mqttControl.DisconnectAsyncClient();
        }
    }

    [Fact]
    public async Task Disconnect_WaitsForSameClientConnectToFinish()
    {
        using var configScope = CreateConfigScope();
        WriteMqttConfig(configScope.Handler.ConfigFilePath, "mqtt-c1", 1883, "user-c1", "password-c1");
        AssertSuccessful(configScope.Handler.LoadConfigsWithResult());

        var clientFactory = new TrackingMqttClientFactory();
        var mqttControl = new MQTTControl(clientFactory.CreateClient);
        AssertSuccessful(configScope.Handler.RegisterReloadParticipants(mqttControl));
        clientFactory.ConfigureNextClient(client => client.BlockConnect = true);

        Task<bool> connectTask = mqttControl.Connect();
        TrackingMqttClientProxy connectingClient = clientFactory.LastClient;
        await connectingClient.ConnectEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Task disconnectTask = mqttControl.DisconnectAsyncClient();
        Assert.False(disconnectTask.IsCompleted);
        Assert.False(connectingClient.DisconnectEntered.Task.IsCompleted);

        connectingClient.AllowConnect();
        Assert.False(await connectTask.WaitAsync(TimeSpan.FromSeconds(5)));
        await disconnectTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(connectingClient.DisconnectEntered.Task.IsCompletedSuccessfully);
        Assert.False(connectingClient.ConcurrentConnectDisconnectObserved);
        Assert.Equal(1, connectingClient.DisposeCalls);
    }

    private ConfigScope CreateConfigScope() => new(Path.Combine(
        _rootDirectory,
        $"ColorVisionConfig-{Guid.NewGuid():N}.json"));

    private static void WriteMqttConfig(
        string fileName,
        string host,
        int port,
        string userName,
        string password)
    {
        var root = new JObject
        {
            [nameof(MQTTSetting)] = new JObject
            {
                [nameof(MQTTSetting.MQTTConfig)] = new JObject
                {
                    [nameof(MQTTConfig.Name)] = $"{host}:{port}",
                    [nameof(MQTTConfig.Host)] = host,
                    [nameof(MQTTConfig.Port)] = port,
                    [nameof(MQTTConfig.UserName)] = userName,
                    [nameof(MQTTConfig.UserPwd)] = Cryptography.AESEncrypt(
                        password,
                        MQTTSetting.ConfigAESKey,
                        MQTTSetting.ConfigAESVector),
                },
                [nameof(MQTTSetting.MQTTConfigs)] = new JArray(),
            },
        };
        File.WriteAllText(fileName, root.ToString());
    }

    private static void AssertSuccessful(ConfigReloadResult result) =>
        Assert.True(result.Succeeded, result.BuildFailureSummary());

    private static void AssertConnectionOptions(
        TrackingMqttClientProxy client,
        string expectedHost,
        int expectedPort,
        string expectedUserName,
        string expectedPassword)
    {
        MqttClientOptions options = Assert.IsType<MqttClientOptions>(client.LastConnectOptions);
        MqttClientTcpOptions tcpOptions = Assert.IsType<MqttClientTcpOptions>(options.ChannelOptions);
        DnsEndPoint endpoint = Assert.IsType<DnsEndPoint>(tcpOptions.RemoteEndpoint);

        Assert.Equal(expectedHost, endpoint.Host);
        Assert.Equal(expectedPort, endpoint.Port);
        Assert.Equal(expectedUserName, options.Credentials.GetUserName(options));
        Assert.Equal(expectedPassword, Encoding.UTF8.GetString(options.Credentials.GetPassword(options)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
            Directory.Delete(_rootDirectory, recursive: true);
    }

    public enum BindFailurePoint
    {
        Factory,
        EventAttach,
        IsConnectedGetter,
    }

    private sealed class ConfigScope : IDisposable
    {
        private readonly IConfigService? _previousConfigService;
        private readonly MQTTSetting? _previousSetting;

        public ConfigScope(string configFilePath)
        {
            _previousConfigService = ConfigService.Instance;
            _previousSetting = _previousConfigService == null ? null : MQTTSetting.Instance;
            Handler = new ConfigHandler { ConfigFilePath = configFilePath };
            ConfigService.SetInstance(Handler);
        }

        public ConfigHandler Handler { get; }

        public void Dispose()
        {
            MQTTSetting.Instance = _previousSetting ?? new MQTTSetting();
            ConfigService.SetInstance(_previousConfigService!);
        }
    }

    private sealed class TrackingMqttClientFactory
    {
        private readonly object _locker = new();
        private readonly List<TrackingMqttClientProxy> _clients = new();
        private Exception? _nextCreateException;
        private Action<TrackingMqttClientProxy>? _nextClientConfiguration;

        public int ForwardedMessageCount { get; set; }

        public int CreatedClientCount
        {
            get
            {
                lock (_locker)
                {
                    return _clients.Count;
                }
            }
        }

        public TrackingMqttClientProxy LastClient
        {
            get
            {
                lock (_locker)
                {
                    return _clients[^1];
                }
            }
        }

        public void FailNextCreate(Exception exception)
        {
            lock (_locker)
            {
                _nextCreateException = exception;
            }
        }

        public void ConfigureNextClient(Action<TrackingMqttClientProxy> configure)
        {
            lock (_locker)
            {
                _nextClientConfiguration = configure;
            }
        }

        public IMqttClient CreateClient()
        {
            lock (_locker)
            {
                if (_nextCreateException != null)
                {
                    Exception exception = _nextCreateException;
                    _nextCreateException = null;
                    throw exception;
                }

                IMqttClient client = DispatchProxy.Create<IMqttClient, TrackingMqttClientProxy>();
                var proxy = (TrackingMqttClientProxy)(object)client;
                proxy.Client = client;
                _nextClientConfiguration?.Invoke(proxy);
                _nextClientConfiguration = null;
                _clients.Add(proxy);
                return client;
            }
        }
    }

    public class TrackingMqttClientProxy : DispatchProxy
    {
        private Func<MqttClientConnectedEventArgs, Task>? _connectedHandlers;
        private Func<MqttClientDisconnectedEventArgs, Task>? _disconnectedHandlers;
        private Func<MqttApplicationMessageReceivedEventArgs, Task>? _applicationMessageHandlers;
        private Func<MqttClientConnectedEventArgs, Task>? _capturedConnectedHandler;
        private Func<MqttClientDisconnectedEventArgs, Task>? _capturedDisconnectedHandler;
        private Func<MqttApplicationMessageReceivedEventArgs, Task>? _capturedApplicationMessageHandler;
        private readonly TaskCompletionSource _allowConnect = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeConnectCalls;
        private int _activeDisconnectCalls;

        public IMqttClient Client { get; set; } = null!;

        public bool IsConnected { get; private set; }

        public bool BlockConnect { get; set; }

        public bool ThrowOnIsConnectedGet { get; set; }

        public bool ThrowOnDisconnectedHandlerAdd { get; set; }

        public bool ConcurrentConnectDisconnectObserved { get; private set; }

        public MqttClientOptions? LastConnectOptions { get; private set; }

        public int DisposeCalls { get; private set; }

        public int ConnectedHandlerAddCount { get; private set; }

        public int DisconnectedHandlerAddCount { get; private set; }

        public int ApplicationMessageHandlerAddCount { get; private set; }

        public int ConnectedHandlerRemoveCount { get; private set; }

        public int DisconnectedHandlerRemoveCount { get; private set; }

        public int ApplicationMessageHandlerRemoveCount { get; private set; }

        public TaskCompletionSource ConnectEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource DisconnectEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void AllowConnect() => _allowConnect.TrySetResult();

        public Task InvokeCapturedConnectedHandlerAsync() =>
            _capturedConnectedHandler?.Invoke(null!) ?? Task.CompletedTask;

        public Task InvokeCapturedDisconnectedHandlerAsync()
        {
            IsConnected = false;
            return _capturedDisconnectedHandler?.Invoke(null!) ?? Task.CompletedTask;
        }

        public Task InvokeCapturedApplicationMessageHandlerAsync() =>
            _capturedApplicationMessageHandler?.Invoke(null!) ?? Task.CompletedTask;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_IsConnected":
                    if (ThrowOnIsConnectedGet)
                        throw new InvalidOperationException("IsConnected getter failed");
                    return IsConnected;
                case "get_Options":
                    return LastConnectOptions;
                case "add_ConnectedAsync":
                    ConnectedHandlerAddCount++;
                    var connected = (Func<MqttClientConnectedEventArgs, Task>)args![0]!;
                    _connectedHandlers += connected;
                    _capturedConnectedHandler ??= connected;
                    return null;
                case "remove_ConnectedAsync":
                    _connectedHandlers -= (Func<MqttClientConnectedEventArgs, Task>)args![0]!;
                    ConnectedHandlerRemoveCount++;
                    return null;
                case "add_DisconnectedAsync":
                    DisconnectedHandlerAddCount++;
                    if (ThrowOnDisconnectedHandlerAdd)
                        throw new InvalidOperationException("DisconnectedAsync event attachment failed");
                    var disconnected = (Func<MqttClientDisconnectedEventArgs, Task>)args![0]!;
                    _disconnectedHandlers += disconnected;
                    _capturedDisconnectedHandler ??= disconnected;
                    return null;
                case "remove_DisconnectedAsync":
                    _disconnectedHandlers -= (Func<MqttClientDisconnectedEventArgs, Task>)args![0]!;
                    DisconnectedHandlerRemoveCount++;
                    return null;
                case "add_ApplicationMessageReceivedAsync":
                    ApplicationMessageHandlerAddCount++;
                    var applicationMessage = (Func<MqttApplicationMessageReceivedEventArgs, Task>)args![0]!;
                    _applicationMessageHandlers += applicationMessage;
                    _capturedApplicationMessageHandler ??= applicationMessage;
                    return null;
                case "remove_ApplicationMessageReceivedAsync":
                    _applicationMessageHandlers -= (Func<MqttApplicationMessageReceivedEventArgs, Task>)args![0]!;
                    ApplicationMessageHandlerRemoveCount++;
                    return null;
                case "ConnectAsync":
                    LastConnectOptions = (MqttClientOptions)args![0]!;
                    return CreateTaskForReturnType(targetMethod.ReturnType, ConnectCoreAsync());
                case "DisconnectAsync":
                    return CreateTaskForReturnType(targetMethod.ReturnType, DisconnectCoreAsync());
                case "Dispose":
                    IsConnected = false;
                    DisposeCalls++;
                    return null;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }

        private async Task ConnectCoreAsync()
        {
            Interlocked.Increment(ref _activeConnectCalls);
            if (Volatile.Read(ref _activeDisconnectCalls) != 0)
                ConcurrentConnectDisconnectObserved = true;
            ConnectEntered.TrySetResult();
            try
            {
                if (BlockConnect)
                    await _allowConnect.Task.ConfigureAwait(false);
                IsConnected = true;
            }
            finally
            {
                Interlocked.Decrement(ref _activeConnectCalls);
            }
        }

        private Task DisconnectCoreAsync()
        {
            Interlocked.Increment(ref _activeDisconnectCalls);
            if (Volatile.Read(ref _activeConnectCalls) != 0)
                ConcurrentConnectDisconnectObserved = true;
            DisconnectEntered.TrySetResult();
            IsConnected = false;
            Interlocked.Decrement(ref _activeDisconnectCalls);
            return Task.CompletedTask;
        }

        private static object CreateTaskForReturnType(Type returnType, Task operation)
        {
            if (returnType == typeof(Task))
                return operation;

            Type resultType = returnType.GetGenericArguments()[0];
            return typeof(TrackingMqttClientProxy)
                .GetMethod(nameof(CompleteWithDefaultResultAsync), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(resultType)
                .Invoke(null, [operation])!;
        }

        private static async Task<TResult> CompleteWithDefaultResultAsync<TResult>(Task operation)
        {
            await operation.ConfigureAwait(false);
            return default!;
        }
    }
}

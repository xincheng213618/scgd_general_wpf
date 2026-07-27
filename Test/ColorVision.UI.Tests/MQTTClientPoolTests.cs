using FlowEngineLib;
using FlowEngineLib.Start;
using MQTTnet;
using System.Reflection;

namespace ColorVision.UI.Tests;

public class MQTTClientPoolTests
{
    [Fact]
    public void ReleaseKeepsActiveEndpointUntilConfigurationChanges()
    {
        string server = $"mqtt-pool-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        var client = DispatchProxy.Create<IMqttClient, ConnectedMqttClientProxy>();

        MQTTClientPool.SetActiveEndpoint(server, port, userName);
        MQTTClientPool.Register(client, server, port, userName);
        MQTTClientPool.Release(client);

        Assert.Same(client, MQTTClientPool.Acquire(server, port, userName));

        MQTTClientPool.SetActiveEndpoint(server + "-new", port, userName);
        MQTTClientPool.Release(client);

        Assert.Null(MQTTClientPool.Acquire(server, port, userName));
    }

    [Fact]
    public void AcquireKeepsDisconnectedClientWithActiveReferences()
    {
        string server = $"mqtt-pool-disconnected-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        var (client, proxy) = CreateTrackingClient(isConnected: false);

        MQTTClientPool.SetActiveEndpoint(server, port, userName);
        Assert.True(MQTTClientPool.Register(client, server, port, userName));

        Assert.Same(client, MQTTClientPool.Acquire(server, port, userName));
        Assert.Equal(0, proxy.DisposeCalls);

        MQTTClientPool.Release(client);
        MQTTClientPool.SetActiveEndpoint(server + "-new", port, userName);
        Assert.Equal(0, proxy.DisposeCalls);
        MQTTClientPool.Release(client);
    }

    [Fact]
    public void PasswordChangeRetiresConnectionWithoutLoggingOrReusingOldCredentials()
    {
        string server = $"mqtt-pool-credentials-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        const string oldPassword = "old-password";
        const string newPassword = "new-password";
        var (client, _) = CreateTrackingClient(isConnected: true);

        MQTTClientPool.SetActiveEndpoint(server, port, userName, oldPassword);
        Assert.True(MQTTClientPool.Register(client, server, port, userName, oldPassword));
        MQTTClientPool.Release(client);

        Assert.Same(client, MQTTClientPool.Acquire(server, port, userName, oldPassword));
        MQTTClientPool.Release(client);

        MQTTClientPool.SetActiveEndpoint(server, port, userName, newPassword);
        Assert.Null(MQTTClientPool.Acquire(server, port, userName, newPassword));
        Assert.Null(MQTTClientPool.Acquire(server, port, userName, oldPassword));
    }

    [Fact]
    public async Task SharedDisconnectUsesOneReconnectLoopAndRetriesUntilSuccess()
    {
        string server = $"mqtt-pool-reconnect-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        var (client, proxy) = CreateTrackingClient(isConnected: true);
        proxy.ReconnectFailuresRemaining = 2;
        Func<int, TimeSpan> originalDelayProvider = MQTTClientPool.ReconnectDelayProvider;
        MQTTClientPool.ReconnectDelayProvider = _ => TimeSpan.FromMilliseconds(1);

        try
        {
            MQTTClientPool.SetActiveEndpoint(server, port, userName);
            Assert.True(MQTTClientPool.Register(client, server, port, userName));
            Assert.Same(client, MQTTClientPool.Acquire(server, port, userName));

            proxy.IsConnected = false;
            await proxy.RaiseDisconnectedAsync();

            Task<bool>[] reconnectWaiters =
            [
                MQTTClientPool.EnsureReconnectAsync(client),
                MQTTClientPool.EnsureReconnectAsync(client),
                MQTTClientPool.EnsureReconnectAsync(client)
            ];
            bool[] reconnectResults = await Task
                .WhenAll(reconnectWaiters)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.All(reconnectResults, Assert.True);
            Assert.Equal(3, proxy.ReconnectCalls);
            Assert.Equal(1, proxy.MaxConcurrentReconnects);

            MQTTClientPool.Release(client);
            MQTTClientPool.SetActiveEndpoint(server + "-new", port, userName);
            MQTTClientPool.Release(client);
        }
        finally
        {
            MQTTClientPool.ReconnectDelayProvider = originalDelayProvider;
        }
    }

    [Fact]
    public async Task TopicSubscriptionsAreReferenceCountedAndRestoredOnce()
    {
        string server = $"mqtt-pool-topics-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        var (client, proxy) = CreateTrackingClient(isConnected: true);
        Guid firstOwner = Guid.NewGuid();
        Guid secondOwner = Guid.NewGuid();

        MQTTClientPool.SetActiveEndpoint(server, port, userName);
        Assert.True(MQTTClientPool.Register(client, server, port, userName));

        Assert.True(await MQTTClientPool.TrySubscribeAsync(client, firstOwner, "DEVICE/STATUS"));
        Assert.True(await MQTTClientPool.TrySubscribeAsync(client, secondOwner, "DEVICE/STATUS"));
        Assert.True(await MQTTClientPool.TrySubscribeAsync(client, firstOwner, "FLOW/CMD/Test"));
        Assert.Equal(2, proxy.SubscribeCalls);

        Assert.True(await MQTTClientPool.TryUnsubscribeAsync(client, firstOwner, "DEVICE/STATUS"));
        Assert.Equal(0, proxy.UnsubscribeCalls);

        proxy.IsConnected = false;
        MQTTClientPool.MarkDisconnected(client);
        proxy.IsConnected = true;
        Assert.True(await MQTTClientPool.RestoreSubscriptionsAsync(client));
        Assert.Equal(4, proxy.SubscribeCalls);

        Assert.True(await MQTTClientPool.TryUnsubscribeAsync(client, secondOwner, "DEVICE/STATUS"));
        Assert.Equal(1, proxy.UnsubscribeCalls);

        await MQTTClientPool.ReleaseOwnerTopicsAsync(client, firstOwner);
        MQTTClientPool.SetActiveEndpoint(server + "-new", port, userName);
        MQTTClientPool.Release(client);
    }

    [Fact]
    public async Task CancelledSubscriptionDoesNotLeaveAnOwnerRegistration()
    {
        string server = $"mqtt-pool-cancelled-topic-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        const string topic = "FLOW/CMD/CANCELLED";
        var (client, proxy) = CreateTrackingClient(isConnected: true);
        Guid cancelledOwner = Guid.NewGuid();
        Guid activeOwner = Guid.NewGuid();

        MQTTClientPool.SetActiveEndpoint(server, port, userName);
        Assert.True(MQTTClientPool.Register(client, server, port, userName));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => MQTTClientPool.TrySubscribeAsync(
                client,
                cancelledOwner,
                topic,
                cancellation.Token));

        Assert.True(await MQTTClientPool.TrySubscribeAsync(client, activeOwner, topic));
        Assert.True(await MQTTClientPool.TryUnsubscribeAsync(client, activeOwner, topic));
        Assert.Equal(1, proxy.SubscribeCalls);
        Assert.Equal(1, proxy.UnsubscribeCalls);

        MQTTClientPool.SetActiveEndpoint(server + "-new", port, userName);
        MQTTClientPool.Release(client);
    }

    [Fact]
    public async Task RetiringConnectionCancelsSubscriptionBeforeDisposal()
    {
        string server = $"mqtt-pool-dispose-subscription-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        var (client, proxy) = CreateTrackingClient(isConnected: true);
        proxy.SubscribeGate = NewGate();

        MQTTClientPool.SetActiveEndpoint(server, port, userName);
        Assert.True(MQTTClientPool.Register(client, server, port, userName));

        Task<bool> subscribeTask = MQTTClientPool.TrySubscribeAsync(
            client,
            Guid.NewGuid(),
            "FLOW/CMD/BLOCKED");
        await proxy.SubscribeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        MQTTClientPool.SetActiveEndpoint(server + "-new", port, userName);
        MQTTClientPool.Release(client);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => subscribeTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(
            SpinWait.SpinUntil(() => proxy.DisposeCalls == 1, TimeSpan.FromSeconds(2)),
            "The retired MQTT client was not disposed.");
        Assert.False(proxy.DisposedWhileOperationActive);
        Assert.True(proxy.LastSubscribeCancellationToken.CanBeCanceled);
    }

    public static TheoryData<Type> MqttStartNodeTypes => new()
    {
        typeof(MQTTStartNode),
        typeof(MQTTStartV5Node)
    };

    [Theory]
    [MemberData(nameof(MqttStartNodeTypes))]
    public async Task CurrentClientRemainsPublishableWhileAnotherTopicRestores(Type startNodeType)
    {
        string server = $"mqtt-start-publish-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        var (client, _) = CreateTrackingClient(isConnected: true);

        MQTTClientPool.SetActiveEndpoint(server, port, userName);
        Assert.True(MQTTClientPool.Register(client, server, port, userName));
        MQTTClientPool.Release(client);

        var helper = new MQTTHelper();
        ResultData_MQTT result = await helper.CreateMQTTClientAndStart(
            server,
            port,
            userName,
            string.Empty,
            _ => { });
        Assert.Equal(1, result.ResultCode);

        var startNode = (BaseStartNode)Activator.CreateInstance(startNodeType)!;
        startNode.Create();
        FieldInfo helperField = startNodeType.GetField("_MQTTHelper", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo hasCurrentClientMethod = startNodeType.GetMethod(
            "HasCurrentMqttClient",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        helperField.SetValue(startNode, helper);
        startNode.Ready = false;

        Assert.False(startNode.IsExecutionReady);
        Assert.True((bool)hasCurrentClientMethod.Invoke(startNode, [helper])!);

        helperField.SetValue(startNode, null);
        startNode.Dispose();
        await helper.DisconnectAsync_Client();
        MQTTClientPool.SetActiveEndpoint(server + "-retired", port, userName);
    }

    [Fact]
    public async Task PublishCancellationUnblocksDisconnectWithoutAStaleCallback()
    {
        string server = $"mqtt-helper-publish-cancel-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        var (client, proxy) = CreateTrackingClient(isConnected: true);
        proxy.PublishGate = NewGate();
        int callbackCount = 0;

        MQTTClientPool.SetActiveEndpoint(server, port, userName);
        Assert.True(MQTTClientPool.Register(client, server, port, userName));
        MQTTClientPool.Release(client);
        var helper = new MQTTHelper();
        ResultData_MQTT result = await helper.CreateMQTTClientAndStart(
            server,
            port,
            userName,
            string.Empty,
            _ => Interlocked.Increment(ref callbackCount));
        Assert.Equal(1, result.ResultCode);
        int callbackCountAfterCreate = callbackCount;

        using var cancellation = new CancellationTokenSource();
        Task<bool> publishTask = helper.TryPublishAsync_Client(
            "FLOW/STATUS",
            "payload",
            false,
            cancellation.Token);
        await proxy.PublishStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Task disconnectTask = helper.DisconnectAsync_Client();
        Assert.True(
            SpinWait.SpinUntil(
                () => GetHelperActiveState(helper) == 0,
                TimeSpan.FromSeconds(2)),
            "Disconnect did not invalidate the helper generation.");
        Assert.False(disconnectTask.IsCompleted);

        cancellation.Cancel();
        Assert.False(await publishTask.WaitAsync(TimeSpan.FromSeconds(2)));
        await disconnectTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(cancellation.Token, proxy.LastPublishCancellationToken);
        Assert.Equal(callbackCountAfterCreate, callbackCount);
        MQTTClientPool.SetActiveEndpoint(server + "-new", port, userName);
    }

    [Fact]
    public async Task ReconnectCancellationCancelsTheUnderlyingConnect()
    {
        string server = $"mqtt-helper-reconnect-cancel-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        var (client, proxy) = CreateTrackingClient(isConnected: true);

        MQTTClientPool.SetActiveEndpoint(server, port, userName);
        Assert.True(MQTTClientPool.Register(client, server, port, userName));
        MQTTClientPool.Release(client);
        var helper = new MQTTHelper();
        ResultData_MQTT result = await helper.CreateMQTTClientAndStart(
            server,
            port,
            userName,
            string.Empty,
            _ => { });
        Assert.Equal(1, result.ResultCode);

        proxy.IsConnected = false;
        proxy.ConnectGate = NewGate();
        using var cancellation = new CancellationTokenSource();
        Task<bool> reconnectTask = helper.TryReconnectAsync_Client(cancellation.Token);
        await proxy.ConnectStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cancellation.Cancel();
        Assert.False(await reconnectTask.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.True(proxy.LastConnectCancellationToken.CanBeCanceled);
        Assert.True(proxy.LastConnectCancellationToken.IsCancellationRequested);
        Assert.False(proxy.IsConnected);

        await helper.DisconnectAsync_Client();
        MQTTClientPool.SetActiveEndpoint(server + "-new", port, userName);
    }

    [Fact]
    public async Task QueuedCreateDoesNotReactivateHelperAfterDisconnect()
    {
        var helper = new MQTTHelper();
        var lifecycleGate = (SemaphoreSlim)typeof(MQTTHelper)
            .GetField("_lifecycleGate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(helper)!;
        int callbackCount = 0;

        await lifecycleGate.WaitAsync();
        Task<ResultData_MQTT> createTask = null!;
        Task disconnectTask = null!;
        try
        {
            createTask = helper.CreateMQTTClientAndStart(
                "localhost",
                1883,
                string.Empty,
                string.Empty,
                _ => Interlocked.Increment(ref callbackCount));
            disconnectTask = helper.DisconnectAsync_Client();
        }
        finally
        {
            lifecycleGate.Release();
        }

        ResultData_MQTT result = await createTask.WaitAsync(TimeSpan.FromSeconds(2));
        await disconnectTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(-1, result.ResultCode);
        Assert.Equal(0, GetHelperActiveState(helper));
        Assert.Equal(0, callbackCount);
        Assert.False(helper.IsClientConnect());
    }

    [Fact]
    public async Task ReleasedHelperDoesNotReceiveSharedClientEvents()
    {
        string server = $"mqtt-helper-release-{Guid.NewGuid():N}";
        const int port = 1883;
        const string userName = "test-user";
        var (client, proxy) = CreateTrackingClient(isConnected: true);
        int callbackCount = 0;

        MQTTClientPool.SetActiveEndpoint(server, port, userName);
        Assert.True(MQTTClientPool.Register(client, server, port, userName));
        MQTTClientPool.Release(client);

        var helper = new MQTTHelper();
        ResultData_MQTT result = await helper.CreateMQTTClientAndStart(
            server,
            port,
            userName,
            string.Empty,
            _ => Interlocked.Increment(ref callbackCount));
        Assert.Equal(1, result.ResultCode);
        Assert.Equal(1, callbackCount);

        await helper.DisconnectAsync_Client();
        int callbackCountAfterRelease = callbackCount;

        proxy.IsConnected = false;
        await proxy.RaiseDisconnectedAsync();
        proxy.IsConnected = true;
        await proxy.RaiseConnectedAsync();
        Assert.Equal(callbackCountAfterRelease, callbackCount);

        MQTTClientPool.SetActiveEndpoint(server + "-new", port, userName);
    }

    private static TaskCompletionSource<bool> NewGate()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static int GetHelperActiveState(MQTTHelper helper)
        => (int)typeof(MQTTHelper)
            .GetField("_active", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(helper)!;

    private static (IMqttClient Client, TrackingMqttClientProxy Proxy) CreateTrackingClient(bool isConnected)
    {
        IMqttClient client = DispatchProxy.Create<IMqttClient, TrackingMqttClientProxy>();
        var proxy = (TrackingMqttClientProxy)(object)client;
        proxy.IsConnected = isConnected;
        return (client, proxy);
    }

    public class ConnectedMqttClientProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "get_IsConnected")
            {
                return true;
            }
            if (targetMethod?.ReturnType == typeof(Task))
            {
                return Task.CompletedTask;
            }
            if (targetMethod?.ReturnType.IsGenericType == true &&
                targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type resultType = targetMethod.ReturnType.GetGenericArguments()[0];
                object? result = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, new[] { result });
            }
            if (targetMethod?.ReturnType == typeof(void))
            {
                return null;
            }
            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }
    }

    public class TrackingMqttClientProxy : DispatchProxy
    {
        private Func<MqttClientConnectedEventArgs, Task>? _connectedHandlers;
        private Func<MqttClientDisconnectedEventArgs, Task>? _disconnectedHandlers;
        private int _concurrentReconnects;
        private int _activeOperations;
        private readonly object _options = new MqttClientOptionsBuilder()
            .WithTcpServer("localhost", 1883)
            .Build();

        public bool IsConnected { get; set; }
        public TaskCompletionSource<bool>? ConnectGate { get; set; }
        public TaskCompletionSource<bool>? SubscribeGate { get; set; }
        public TaskCompletionSource<bool>? PublishGate { get; set; }
        public TaskCompletionSource<bool> ConnectStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> SubscribeStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> PublishStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ReconnectFailuresRemaining { get; set; }
        public int ReconnectCalls { get; private set; }
        public int MaxConcurrentReconnects { get; private set; }
        public int SubscribeCalls { get; private set; }
        public int UnsubscribeCalls { get; private set; }
        public int PublishCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public bool DisposedWhileOperationActive { get; private set; }
        public CancellationToken LastConnectCancellationToken { get; private set; }
        public CancellationToken LastSubscribeCancellationToken { get; private set; }
        public CancellationToken LastPublishCancellationToken { get; private set; }

        public async Task RaiseConnectedAsync()
        {
            if (_connectedHandlers == null)
            {
                return;
            }
            foreach (Func<MqttClientConnectedEventArgs, Task> handler in _connectedHandlers.GetInvocationList())
            {
                await handler(null!);
            }
        }

        public async Task RaiseDisconnectedAsync()
        {
            if (_disconnectedHandlers == null)
            {
                return;
            }
            foreach (Func<MqttClientDisconnectedEventArgs, Task> handler in _disconnectedHandlers.GetInvocationList())
            {
                await handler(null!);
            }
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_IsConnected":
                    return IsConnected;
                case "get_Options":
                    return _options;
                case "add_ConnectedAsync":
                    _connectedHandlers += (Func<MqttClientConnectedEventArgs, Task>)args![0]!;
                    return null;
                case "remove_ConnectedAsync":
                    _connectedHandlers -= (Func<MqttClientConnectedEventArgs, Task>)args![0]!;
                    return null;
                case "add_DisconnectedAsync":
                    _disconnectedHandlers += (Func<MqttClientDisconnectedEventArgs, Task>)args![0]!;
                    return null;
                case "remove_DisconnectedAsync":
                    _disconnectedHandlers -= (Func<MqttClientDisconnectedEventArgs, Task>)args![0]!;
                    return null;
                case "add_ApplicationMessageReceivedAsync":
                case "remove_ApplicationMessageReceivedAsync":
                    return null;
                case "ReconnectAsync":
                case "ConnectAsync":
                    ReconnectCalls++;
                    LastConnectCancellationToken = GetCancellationToken(args);
                    int concurrentReconnects = Interlocked.Increment(ref _concurrentReconnects);
                    MaxConcurrentReconnects = Math.Max(MaxConcurrentReconnects, concurrentReconnects);
                    if (ReconnectFailuresRemaining > 0)
                    {
                        ReconnectFailuresRemaining--;
                        Interlocked.Decrement(ref _concurrentReconnects);
                        return CreateFaultedTask(
                            targetMethod.ReturnType,
                            new InvalidOperationException("reconnect failed"));
                    }
                    if (ConnectGate != null)
                    {
                        ConnectStarted.TrySetResult(true);
                        Interlocked.Increment(ref _activeOperations);
                        return CreateBlockedTask(
                            targetMethod.ReturnType,
                            ConnectGate.Task,
                            () => IsConnected = true,
                            () =>
                            {
                                Interlocked.Decrement(ref _activeOperations);
                                Interlocked.Decrement(ref _concurrentReconnects);
                            },
                            LastConnectCancellationToken);
                    }
                    IsConnected = true;
                    Interlocked.Decrement(ref _concurrentReconnects);
                    return CreateCompletedTask(targetMethod.ReturnType);
                case "SubscribeAsync":
                    SubscribeCalls++;
                    LastSubscribeCancellationToken = GetCancellationToken(args);
                    if (SubscribeGate != null)
                    {
                        SubscribeStarted.TrySetResult(true);
                        Interlocked.Increment(ref _activeOperations);
                        return CreateBlockedTask(
                            targetMethod.ReturnType,
                            SubscribeGate.Task,
                            () => { },
                            () => Interlocked.Decrement(ref _activeOperations),
                            LastSubscribeCancellationToken);
                    }
                    return CreateCompletedTask(targetMethod.ReturnType);
                case "UnsubscribeAsync":
                    UnsubscribeCalls++;
                    return CreateCompletedTask(targetMethod.ReturnType);
                case "PublishAsync":
                    PublishCalls++;
                    LastPublishCancellationToken = GetCancellationToken(args);
                    if (PublishGate != null)
                    {
                        PublishStarted.TrySetResult(true);
                        Interlocked.Increment(ref _activeOperations);
                        return CreateBlockedTask(
                            targetMethod.ReturnType,
                            PublishGate.Task,
                            () => { },
                            () => Interlocked.Decrement(ref _activeOperations),
                            LastPublishCancellationToken);
                    }
                    return CreateCompletedTask(targetMethod.ReturnType);
                case "DisconnectAsync":
                    IsConnected = false;
                    return CreateCompletedTask(targetMethod.ReturnType);
                case "Dispose":
                    DisposedWhileOperationActive = Volatile.Read(ref _activeOperations) > 0;
                    DisposeCalls++;
                    return null;
            }

            return targetMethod?.ReturnType.IsValueType == true
                ? Activator.CreateInstance(targetMethod.ReturnType)
                : null;
        }

        private static CancellationToken GetCancellationToken(object?[]? args)
            => args?.OfType<CancellationToken>().LastOrDefault() ?? CancellationToken.None;

        private static object CreateBlockedTask(
            Type returnType,
            Task gate,
            Action onSuccess,
            Action onCompleted,
            CancellationToken cancellationToken)
        {
            if (returnType == typeof(Task))
            {
                return CompleteBlockedTaskAsync(
                    gate,
                    onSuccess,
                    onCompleted,
                    cancellationToken);
            }

            Type resultType = returnType.GetGenericArguments()[0];
            return typeof(TrackingMqttClientProxy)
                .GetMethod(
                    nameof(CompleteBlockedTaskAsyncGeneric),
                    BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(resultType)
                .Invoke(
                    null,
                    new object[]
                    {
                        gate,
                        onSuccess,
                        onCompleted,
                        cancellationToken
                    })!;
        }

        private static async Task CompleteBlockedTaskAsync(
            Task gate,
            Action onSuccess,
            Action onCompleted,
            CancellationToken cancellationToken)
        {
            try
            {
                await gate.WaitAsync(cancellationToken);
                onSuccess();
            }
            finally
            {
                onCompleted();
            }
        }

        private static async Task<T> CompleteBlockedTaskAsyncGeneric<T>(
            Task gate,
            Action onSuccess,
            Action onCompleted,
            CancellationToken cancellationToken)
        {
            await CompleteBlockedTaskAsync(
                gate,
                onSuccess,
                onCompleted,
                cancellationToken);
            return default!;
        }

        private static object CreateCompletedTask(Type returnType)
        {
            if (returnType == typeof(Task))
            {
                return Task.CompletedTask;
            }
            Type resultType = returnType.GetGenericArguments()[0];
            object? result = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            return typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, new[] { result })!;
        }

        private static object CreateFaultedTask(Type returnType, Exception exception)
        {
            if (returnType == typeof(Task))
            {
                return Task.FromException(exception);
            }
            Type resultType = returnType.GetGenericArguments()[0];
            return typeof(Task).GetMethods()
                .Single(method => method.Name == nameof(Task.FromException) && method.IsGenericMethod)
                .MakeGenericMethod(resultType)
                .Invoke(null, new object[] { exception })!;
        }
    }
}

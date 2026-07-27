using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlowEngineLib.MQTT;
using log4net;
using MQTTnet;

namespace FlowEngineLib;

public class MQTTHelper
{
    public static readonly ILog logger = LogManager.GetLogger(typeof(MQTTHelper));
    private const string EmptyServerMessage = "MQTT服务器地址为空。";

    private static readonly SemaphoreSlim ClientCreationGate = new SemaphoreSlim(1, 1);

    // 回调委托
    private Action<ResultData_MQTT> _Callback;

    // MQTTnet v5 核心对象
    private IMqttClient _MqttClient;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly object _subscriptionLock = new object();
    private readonly HashSet<string> _desiredTopics = new HashSet<string>(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lifecycleGate = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _clientOperationGate = new SemaphoreSlim(1, 1);
    private int _active;
    private int _callbackGeneration;
    private int _lifecycleGeneration;
    private bool _handlersAttached;
    private static string NormalizeServer(string server)
    {
        return string.IsNullOrWhiteSpace(server) ? null : server.Trim();
    }

    private static ResultData_MQTT CreateClientConnectedResult(int resultCode, string resultMessage)
    {
        return new ResultData_MQTT
        {
            ResultCode = resultCode,
            EventType = EventTypeEnum.ClientConnected,
            ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>{resultMessage}"
        };
    }

    private void NotifyCallback(ResultData_MQTT resultData)
    {
        if (Volatile.Read(ref _active) == 0)
        {
            return;
        }

        try
        {
            Volatile.Read(ref _Callback)?.Invoke(resultData);
        }
        catch (Exception ex)
        {
            logger.Error("MQTT回调执行失败", ex);
        }
    }

    private void NotifyCallback(ResultData_MQTT resultData, int expectedGeneration)
    {
        if (Volatile.Read(ref _active) == 1 &&
            Volatile.Read(ref _callbackGeneration) == expectedGeneration)
        {
            NotifyCallback(resultData);
        }
    }

    #region Client 端逻辑

    public Task<ResultData_MQTT> CreateMQTTClientAndStart(
        string mqttServerUrl,
        int port,
        string userName,
        string userPassword,
        Action<ResultData_MQTT> callback)
        => CreateMQTTClientAndStart(
            mqttServerUrl,
            port,
            userName,
            userPassword,
            callback,
            CancellationToken.None);

    public async Task<ResultData_MQTT> CreateMQTTClientAndStart(
        string mqttServerUrl,
        int port,
        string userName,
        string userPassword,
        Action<ResultData_MQTT> callback,
        CancellationToken cancellationToken)
    {
        int lifecycleGeneration = Volatile.Read(ref _lifecycleGeneration);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (lifecycleGeneration != Volatile.Read(ref _lifecycleGeneration))
            {
                return CreateClientConnectedResult(
                    -1,
                    "MQTTClient 创建请求已因连接生命周期变化而取消。");
            }

            _Callback = callback;
            Volatile.Write(ref _active, 1);
            Interlocked.Increment(ref _callbackGeneration);

            var server = NormalizeServer(mqttServerUrl);
            if (server == null)
            {
                logger.Warn("CreateMQTTClientAndStart skipped because MQTT server host is empty.");
                var invalidHostResult = CreateClientConnectedResult(-1, $"执行了开启MQTTClient_失败！{EmptyServerMessage}");
                NotifyCallback(invalidHostResult);
                return invalidHostResult;
            }

            if (_MqttClient != null)
            {
                bool connected = _MqttClient.IsConnected;
                if (!connected && MQTTClientPool.IsRegistered(_MqttClient))
                {
                    connected = await MQTTClientPool.TryReconnectNowAsync(
                        _MqttClient,
                        cancellationToken);
                }

                var existingResult = CreateClientConnectedResult(
                    connected ? 1 : -1,
                    connected
                        ? $"MQTT连接已开启！[{server}:{port}]"
                        : $"MQTT连接正在恢复！[{server}:{port}]");
                NotifyCallback(existingResult);
                return existingResult;
            }

            MQTTClientPool.SetActiveEndpoint(server, port, userName, userPassword);
            await ClientCreationGate.WaitAsync(cancellationToken);
            try
            {
                var pooledClient = MQTTClientPool.Acquire(server, port, userName, userPassword);
                if (pooledClient != null)
                {
                    AttachClient(pooledClient);
                    if (pooledClient.IsConnected)
                    {
                        await RegisterDesiredTopicsAsync(pooledClient, cancellationToken);
                        await MQTTClientPool.RestoreSubscriptionsAsync(
                            pooledClient,
                            cancellationToken);
                    }
                    else
                    {
                        await MQTTClientPool.TryReconnectNowAsync(
                            pooledClient,
                            cancellationToken);
                    }

                    var pooledResult = CreateClientConnectedResult(
                        pooledClient.IsConnected ? 1 : -1,
                        pooledClient.IsConnected
                            ? $"复用MQTT连接_成功！[{server}:{port}]"
                            : $"复用MQTT连接，正在重连！[{server}:{port}]");
                    NotifyCallback(pooledResult);
                    return pooledResult;
                }

                var optionsBuilder = BuildClientOptions(server, port, userName, userPassword);
                ResultData_MQTT createResult = await ConnectNewClientAsync(
                    optionsBuilder,
                    cancellationToken);
                IMqttClient createdClient = _MqttClient;

                if (createResult.ResultCode == 1 &&
                    createdClient != null &&
                    createdClient.IsConnected)
                {
                    if (!MQTTClientPool.Register(createdClient, server, port, userName, userPassword))
                    {
                        DetachClient(createdClient);
                        await DisconnectAndDisposeClientAsync(createdClient);
                        _MqttClient = null;

                        IMqttClient sharedClient = MQTTClientPool.Acquire(server, port, userName, userPassword);
                        if (sharedClient == null)
                        {
                            createResult = CreateClientConnectedResult(
                                -1,
                                $"执行了开启MQTTClient_失败！无法取得共享连接。[{server}:{port}]");
                        }
                        else
                        {
                            AttachClient(sharedClient);
                            if (!sharedClient.IsConnected)
                            {
                                await MQTTClientPool.TryReconnectNowAsync(
                                    sharedClient,
                                    cancellationToken);
                            }
                            createResult = CreateClientConnectedResult(
                                sharedClient.IsConnected ? 1 : -1,
                                sharedClient.IsConnected
                                    ? $"复用并发创建的MQTT连接_成功！[{server}:{port}]"
                                    : $"共享MQTT连接正在恢复！[{server}:{port}]");
                        }
                    }

                    if (_MqttClient != null && MQTTClientPool.IsRegistered(_MqttClient))
                    {
                        await RegisterDesiredTopicsAsync(
                            _MqttClient,
                            cancellationToken);
                        await MQTTClientPool.RestoreSubscriptionsAsync(
                            _MqttClient,
                            cancellationToken);
                    }
                }
                else if (createdClient != null)
                {
                    DetachClient(createdClient);
                    await DisconnectAndDisposeClientAsync(createdClient);
                    _MqttClient = null;
                }

                NotifyCallback(createResult);
                return createResult;
            }
            finally
            {
                ClientCreationGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            await CleanupCancelledCreateAsync();
            throw;
        }
        catch (Exception ex)
        {
            string server = NormalizeServer(mqttServerUrl);
            logger.Error($"执行了开启MQTTClient_失败！[{server}:{port}]", ex);
            var result = CreateClientConnectedResult(-1, $"执行了开启MQTTClient_失败！错误信息：{ex.Message}");
            NotifyCallback(result);
            return result;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private MqttClientOptionsBuilder BuildClientOptions(string mqttServerUrl, int port, string userName, string userPassword)
    {
        var server = NormalizeServer(mqttServerUrl) ?? throw new ArgumentException(EmptyServerMessage, nameof(mqttServerUrl));
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(server, port)
            .WithClientId(Guid.NewGuid().ToString("N"));

        if (!string.IsNullOrEmpty(userName))
        {
            builder.WithCredentials(userName, userPassword);
        }

        return builder;
    }

    public Task<ResultData_MQTT> CreateMQTTClientAndStart(
        MqttClientOptionsBuilder mqttClientOptionsBuilder,
        Action<ResultData_MQTT> callback)
        => CreateMQTTClientAndStart(
            mqttClientOptionsBuilder,
            callback,
            CancellationToken.None);

    public async Task<ResultData_MQTT> CreateMQTTClientAndStart(
        MqttClientOptionsBuilder mqttClientOptionsBuilder,
        Action<ResultData_MQTT> callback,
        CancellationToken cancellationToken)
    {
        int lifecycleGeneration = Volatile.Read(ref _lifecycleGeneration);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (lifecycleGeneration != Volatile.Read(ref _lifecycleGeneration))
            {
                return CreateClientConnectedResult(
                    -1,
                    "MQTTClient 创建请求已因连接生命周期变化而取消。");
            }

            _Callback = callback;
            Volatile.Write(ref _active, 1);
            int generation = Interlocked.Increment(ref _callbackGeneration);
            ResultData_MQTT resultData = await ConnectNewClientAsync(
                mqttClientOptionsBuilder,
                cancellationToken);
            NotifyCallback(resultData, generation);
            return resultData;
        }
        catch (OperationCanceledException)
        {
            await CleanupCancelledCreateAsync();
            throw;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task<ResultData_MQTT> ConnectNewClientAsync(
        MqttClientOptionsBuilder mqttClientOptionsBuilder,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = mqttClientOptionsBuilder.Build();
            IMqttClient client = new MqttClientFactory().CreateMqttClient();
            AttachClient(client);
            await client.ConnectAsync(options, cancellationToken);

            return client.IsConnected
                ? new ResultData_MQTT
                {
                    ResultCode = 1,
                    EventType = EventTypeEnum.ClientConnected,
                    ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了开启MQTTClient_成功！[{options.ChannelOptions}]"
                }
                : CreateClientConnectedResult(-1, "执行了开启MQTTClient_失败！无法连接。");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CreateClientConnectedResult(-1, $"执行了开启MQTTClient_失败！错误信息：{ex.Message}");
        }
    }

    private async Task CleanupCancelledCreateAsync()
    {
        Volatile.Write(ref _active, 0);
        Interlocked.Increment(ref _callbackGeneration);
        await _clientOperationGate.WaitAsync();
        try
        {
            try
            {
                IMqttClient client = _MqttClient;
                _MqttClient = null;
                if (client != null)
                {
                    await ReleaseClientAsync(client);
                }
            }
            finally
            {
                lock (_subscriptionLock)
                {
                    _desiredTopics.Clear();
                }
                Volatile.Write(ref _Callback, null);
            }
        }
        finally
        {
            _clientOperationGate.Release();
        }
    }

    public bool IsClientConnect()
    {
        return _MqttClient != null && _MqttClient.IsConnected;
    }

    public async Task DisconnectAsync_Client()
    {
        Interlocked.Increment(ref _lifecycleGeneration);
        Interlocked.Exchange(ref _active, 0);
        Interlocked.Increment(ref _callbackGeneration);
        Volatile.Write(ref _Callback, null);
        await _lifecycleGate.WaitAsync();
        try
        {
            Interlocked.Exchange(ref _active, 0);
            Interlocked.Increment(ref _callbackGeneration);
            await _clientOperationGate.WaitAsync();
            try
            {
                try
                {
                    IMqttClient client = _MqttClient;
                    _MqttClient = null;
                    if (client != null)
                    {
                        await ReleaseClientAsync(client);
                    }
                }
                finally
                {
                    lock (_subscriptionLock)
                    {
                        _desiredTopics.Clear();
                    }
                    Volatile.Write(ref _Callback, null);
                }
            }
            finally
            {
                _clientOperationGate.Release();
            }
        }
        catch (Exception ex)
        {
            logger.Warn("释放MQTT连接失败", ex);
        }
        finally
        {
            Volatile.Write(ref _Callback, null);
            _lifecycleGate.Release();
        }
    }

    public async Task ReconnectAsync_Client()
    {
        await TryReconnectAsync_Client();
    }

    public async Task<bool> TryReconnectAsync_Client(
        CancellationToken cancellationToken = default)
    {
        int generation = Volatile.Read(ref _callbackGeneration);
        bool success = false;
        bool shouldNotify = false;
        bool gateAcquired = false;
        string errorMessage = string.Empty;
        try
        {
            await _clientOperationGate.WaitAsync(cancellationToken);
            gateAcquired = true;
            IMqttClient client = _MqttClient;
            if (Volatile.Read(ref _active) == 0 ||
                Volatile.Read(ref _callbackGeneration) != generation ||
                client == null)
            {
                return false;
            }

            if (MQTTClientPool.IsRegistered(client))
            {
                success = await MQTTClientPool.TryReconnectNowAsync(
                    client,
                    cancellationToken);
            }
            else
            {
                if (!client.IsConnected)
                {
                    await client.ConnectAsync(client.Options, cancellationToken);
                }
                success = client.IsConnected;
            }

            bool isCurrent = Volatile.Read(ref _active) == 1 &&
                             Volatile.Read(ref _callbackGeneration) == generation &&
                             ReferenceEquals(client, _MqttClient);
            if (!isCurrent)
            {
                return false;
            }
            shouldNotify = true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            shouldNotify = Volatile.Read(ref _active) == 1 &&
                           Volatile.Read(ref _callbackGeneration) == generation;
        }
        finally
        {
            if (gateAcquired)
            {
                _clientOperationGate.Release();
            }
        }

        if (shouldNotify)
        {
            NotifyCallback(new ResultData_MQTT
            {
                ResultCode = success ? 1 : -1,
                EventType = EventTypeEnum.ClientReconnected,
                ResultMsg = success
                    ? $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了MQTTClient重连_成功！"
                    : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了MQTTClient重连_失败！错误信息：{errorMessage}"
            }, generation);
        }
        return success;
    }

    public async Task SubscribeAsync_Client(string topic)
    {
        await TrySubscribeAsync_Client(topic);
    }

    public async Task<bool> TrySubscribeAsync_Client(
        string topic,
        CancellationToken cancellationToken = default)
    {
        string normalizedTopic = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim();
        int generation = Volatile.Read(ref _callbackGeneration);
        bool success = false;
        bool gateAcquired = false;
        bool desiredTopicAdded = false;
        bool poolRegistrationAttempted = false;
        string errorMessage = string.Empty;

        try
        {
            await _clientOperationGate.WaitAsync(cancellationToken);
            gateAcquired = true;
            IMqttClient client = _MqttClient;
            if (Volatile.Read(ref _active) == 0 ||
                Volatile.Read(ref _callbackGeneration) != generation ||
                normalizedTopic == null ||
                client == null ||
                !client.IsConnected)
            {
                errorMessage = "MQTTClient未开启连接！";
            }
            else
            {
                lock (_subscriptionLock)
                {
                    desiredTopicAdded = _desiredTopics.Add(normalizedTopic);
                }

                if (MQTTClientPool.IsRegistered(client))
                {
                    poolRegistrationAttempted = true;
                    success = await MQTTClientPool.TrySubscribeAsync(
                        client,
                        _ownerId,
                        normalizedTopic,
                        cancellationToken);
                    if (!success)
                    {
                        errorMessage = "共享连接订阅未完成。";
                    }
                }
                else
                {
                    var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(filter => filter.WithTopic(normalizedTopic))
                        .Build();
                    await client.SubscribeAsync(subscribeOptions, cancellationToken);
                    success = true;
                }

                bool isCurrent = Volatile.Read(ref _active) == 1 &&
                                 Volatile.Read(ref _callbackGeneration) == generation &&
                                 ReferenceEquals(client, _MqttClient);
                if (!isCurrent)
                {
                    if (poolRegistrationAttempted)
                    {
                        await MQTTClientPool.TryUnsubscribeAsync(
                            client,
                            _ownerId,
                            normalizedTopic,
                            CancellationToken.None);
                    }
                    if (desiredTopicAdded)
                    {
                        lock (_subscriptionLock)
                        {
                            _desiredTopics.Remove(normalizedTopic);
                        }
                    }
                    success = false;
                    errorMessage = "订阅所属的 MQTT 连接已释放。";
                }
            }
        }
        catch (OperationCanceledException)
        {
            errorMessage = "订阅已取消。";
            if (desiredTopicAdded && normalizedTopic != null)
            {
                lock (_subscriptionLock)
                {
                    _desiredTopics.Remove(normalizedTopic);
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            if (gateAcquired)
            {
                _clientOperationGate.Release();
            }
        }

        NotifyCallback(new ResultData_MQTT
        {
            ResultCode = success ? 1 : -1,
            EventType = EventTypeEnum.Subscribe,
            ResultObject1 = normalizedTopic ?? topic,
            ResultMsg = success
                ? $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>MQTTClient执行了订阅'{normalizedTopic}'_成功！"
                : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>MQTTClient执行了订阅'{normalizedTopic ?? topic}'_失败！错误信息：{errorMessage}"
        }, generation);
        return success;
    }

    public async Task UnsubscribeAsync_Client(string topic)
    {
        string normalizedTopic = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim();
        int generation = Volatile.Read(ref _callbackGeneration);
        bool success = false;
        bool gateAcquired = false;
        string errorMessage = string.Empty;

        try
        {
            await _clientOperationGate.WaitAsync();
            gateAcquired = true;
            IMqttClient client = _MqttClient;
            if (Volatile.Read(ref _active) == 0 ||
                Volatile.Read(ref _callbackGeneration) != generation ||
                normalizedTopic == null ||
                client == null)
            {
                errorMessage = "MQTTClient未开启连接！";
            }
            else
            {
                lock (_subscriptionLock)
                {
                    _desiredTopics.Remove(normalizedTopic);
                }

                if (MQTTClientPool.IsRegistered(client))
                {
                    success = await MQTTClientPool.TryUnsubscribeAsync(
                        client,
                        _ownerId,
                        normalizedTopic);
                }
                else if (client.IsConnected)
                {
                    await client.UnsubscribeAsync(normalizedTopic, CancellationToken.None);
                    success = true;
                }
                else
                {
                    // The desired subscription has still been removed, so it
                    // will not be restored after reconnect.
                    success = true;
                }

                if (Volatile.Read(ref _active) == 0 ||
                    Volatile.Read(ref _callbackGeneration) != generation ||
                    !ReferenceEquals(client, _MqttClient))
                {
                    success = false;
                }
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            if (gateAcquired)
            {
                _clientOperationGate.Release();
            }
        }

        NotifyCallback(new ResultData_MQTT
        {
            ResultCode = success ? 1 : -1,
            EventType = EventTypeEnum.Unsubscribe,
            ResultObject1 = normalizedTopic ?? topic,
            ResultMsg = success
                ? $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>MQTTClient执行了退订'{normalizedTopic}'_成功！"
                : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>MQTTClient执行退订'{normalizedTopic ?? topic}'_失败！错误信息：{errorMessage}"
        }, generation);
    }

    public async Task PublishAsync_Client(string topic, string msg, bool retained)
    {
        await TryPublishAsync_Client(topic, msg, retained);
    }

    public async Task<bool> TryPublishAsync_Client(
        string topic,
        string msg,
        bool retained,
        CancellationToken cancellationToken = default)
    {
        int generation = Volatile.Read(ref _callbackGeneration);
        bool success = false;
        bool gateAcquired = false;
        string errorMessage = string.Empty;
        try
        {
            await _clientOperationGate.WaitAsync(cancellationToken);
            gateAcquired = true;
            IMqttClient client = _MqttClient;
            if (Volatile.Read(ref _active) == 1 &&
                Volatile.Read(ref _callbackGeneration) == generation &&
                client != null &&
                client.IsConnected)
            {
                var applicationMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(msg)
                    .WithRetainFlag(retained)
                    .Build();

                await client.PublishAsync(applicationMessage, cancellationToken);
                success = Volatile.Read(ref _active) == 1 &&
                          Volatile.Read(ref _callbackGeneration) == generation &&
                          ReferenceEquals(client, _MqttClient);
                if (!success)
                {
                    errorMessage = "发布所属的 MQTT 连接已释放。";
                }
            }
            else
            {
                errorMessage = "MQTTClient未开启连接！";
            }
        }
        catch (OperationCanceledException)
        {
            errorMessage = "发布已取消。";
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }
        finally
        {
            if (gateAcquired)
            {
                _clientOperationGate.Release();
            }
        }

        NotifyCallback(new ResultData_MQTT
        {
            ResultCode = success ? 1 : -1,
            EventType = EventTypeEnum.Publish,
            ResultMsg = success
                ? $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了发布信息_成功！主题:'{topic}'，信息:'{msg}'"
                : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>执行了发布信息_失败！错误信息：{errorMessage}"
        }, generation);
        return success;
    }

    private void AttachClient(IMqttClient client)
    {
        if (client == null)
        {
            return;
        }

        if (_handlersAttached && ReferenceEquals(_MqttClient, client))
        {
            return;
        }
        if (_handlersAttached && _MqttClient != null)
        {
            DetachClient(_MqttClient);
        }

        _MqttClient = client;
        client.ConnectedAsync += ConnectedHandle;
        client.DisconnectedAsync += DisconnectedHandle;
        client.ApplicationMessageReceivedAsync += ApplicationMessageReceivedHandle;
        _handlersAttached = true;
    }

    private void DetachClient(IMqttClient client)
    {
        if (client == null || !_handlersAttached)
        {
            return;
        }

        client.ConnectedAsync -= ConnectedHandle;
        client.DisconnectedAsync -= DisconnectedHandle;
        client.ApplicationMessageReceivedAsync -= ApplicationMessageReceivedHandle;
        _handlersAttached = false;
    }

    private async Task RegisterDesiredTopicsAsync(
        IMqttClient client,
        CancellationToken cancellationToken = default)
    {
        if (client == null || !MQTTClientPool.IsRegistered(client))
        {
            return;
        }

        string[] topics;
        lock (_subscriptionLock)
        {
            topics = new string[_desiredTopics.Count];
            _desiredTopics.CopyTo(topics);
        }

        foreach (string topic in topics)
        {
            await MQTTClientPool.TrySubscribeAsync(
                client,
                _ownerId,
                topic,
                cancellationToken);
        }
    }

    private async Task ReleaseClientAsync(IMqttClient client)
    {
        DetachClient(client);
        if (MQTTClientPool.IsRegistered(client))
        {
            try
            {
                await MQTTClientPool.ReleaseOwnerTopicsAsync(
                    client,
                    _ownerId,
                    CancellationToken.None);
            }
            finally
            {
                MQTTClientPool.Release(client);
            }
        }
        else
        {
            await DisconnectAndDisposeClientAsync(client);
        }
    }

    private static async Task DisconnectAndDisposeClientAsync(IMqttClient client)
    {
        if (client == null)
        {
            return;
        }

        try
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(
                    new MqttClientDisconnectOptionsBuilder()
                        .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                        .Build());
            }
        }
        catch (Exception ex)
        {
            logger.Warn("MQTT客户端断开失败", ex);
        }
        finally
        {
            try
            {
                client.Dispose();
            }
            catch (Exception ex)
            {
                logger.Warn("MQTT客户端释放失败", ex);
            }
        }
    }

    #region Client Event Handlers

    private async Task ConnectedHandle(MqttClientConnectedEventArgs arg)
    {
        IMqttClient client = _MqttClient;
        int generation = Volatile.Read(ref _callbackGeneration);
        if (Volatile.Read(ref _active) == 0 || client == null)
        {
            return;
        }

        // A newly created client raises ConnectedAsync before it has been
        // registered in the pool. Its public create method sends the single
        // authoritative connected callback after registration.
        if (!MQTTClientPool.IsRegistered(client))
        {
            return;
        }

        bool subscriptionsReady = await MQTTClientPool.RestoreSubscriptionsAsync(client);
        if (Volatile.Read(ref _active) == 0 ||
            Volatile.Read(ref _callbackGeneration) != generation ||
            !ReferenceEquals(client, _MqttClient))
        {
            return;
        }

        NotifyCallback(new ResultData_MQTT
        {
            ResultCode = subscriptionsReady ? 1 : -1,
            EventType = EventTypeEnum.ClientConnected,
            ResultMsg = subscriptionsReady
                ? $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>已连接到MQTT服务器，订阅已恢复！"
                : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>已连接到MQTT服务器，但订阅恢复失败！"
        }, generation);
    }

    private Task DisconnectedHandle(MqttClientDisconnectedEventArgs arg)
    {
        IMqttClient client = _MqttClient;
        if (Volatile.Read(ref _active) == 0 || client == null)
        {
            return Task.CompletedTask;
        }

        if (MQTTClientPool.IsRegistered(client))
        {
            MQTTClientPool.MarkDisconnected(client);
        }

        NotifyCallback(new ResultData_MQTT
        {
            ResultCode = 1,
            EventType = EventTypeEnum.ClientDisconnected,
            ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>已断开与MQTT服务器连接！"
        });
        return Task.CompletedTask;
    }

    private Task ApplicationMessageReceivedHandle(MqttApplicationMessageReceivedEventArgs arg)
    {
        if (Volatile.Read(ref _active) == 0)
        {
            return Task.CompletedTask;
        }

        string payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);

        var resultData = new ResultData_MQTT
        {
            ResultCode = 1,
            EventType = EventTypeEnum.MsgRecv,
            ResultMsg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}>>>MQTTClient'{arg.ClientId}'内容：'{payload}'；主题：'{arg.ApplicationMessage.Topic}'",
            ResultObject1 = arg.ApplicationMessage.Topic,
            ResultObject2 = payload
        };

        int generation = Volatile.Read(ref _callbackGeneration);
        _ = Task.Run(() =>
        {
            if (Volatile.Read(ref _active) == 1 &&
                Volatile.Read(ref _callbackGeneration) == generation)
            {
                NotifyCallback(resultData);
            }
        });
        return Task.CompletedTask;
    }

    #endregion

    #endregion
}

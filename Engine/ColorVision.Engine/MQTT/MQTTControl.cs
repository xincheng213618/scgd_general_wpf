#pragma warning disable CA1822,CS8603
using ColorVision.Common.MVVM;
using log4net;
using MQTTnet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.MQTT
{
    public sealed class MqttMessageTraceEntry
    {
        public DateTime Time { get; set; }

        public string Direction { get; set; }

        public string Topic { get; set; }

        public string Payload { get; set; }

        public string QualityOfServiceLevel { get; set; }

        public bool Retain { get; set; }
    }

    public sealed class MqttRuntimeDiagnostics
    {
        public bool Configured { get; init; }

        public bool Connected { get; init; }

        public int RegisteredSubscriptionCount { get; init; }

        public int ActiveSubscriptionCount { get; init; }

        public DateTimeOffset? LastConnectedAt { get; init; }

        public DateTimeOffset? LastDisconnectedAt { get; init; }

        public DateTimeOffset? LastInboundActivityAt { get; init; }

        public DateTimeOffset? LastOutboundActivityAt { get; init; }
    }

    public class MQTTControl : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MQTTControl));
        private const int MaxMessageTraceCount = 200;

        private static readonly Lazy<MQTTControl> Instance = new(() => new(new MqttClientFactory().CreateMqttClient()));
        private readonly SemaphoreSlim _clientGate = new(1, 1);
        private volatile bool _connectionWanted;
        private int _reconnecting;
        public static MQTTControl GetInstance() => Instance.Value;

        public const string Host = "127.0.0.1";
        public const int Port = 1883;

        public IMqttClient MQTTClient { get; }

        public bool IsConnect { get => _IsConnect; private set { if (_IsConnect == value) return; _IsConnect = value; MQTTConnectChanged?.Invoke(this, EventArgs.Empty); OnPropertyChanged(); } }
        private bool _IsConnect;

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

        public event EventHandler MQTTConnectChanged;

        private readonly object _messageTraceLocker = new();
        private readonly List<MqttMessageTraceEntry> _messageTraces = new();
        private readonly object _runtimeDiagnosticsLocker = new();
        private DateTimeOffset? _lastConnectedAt;
        private DateTimeOffset? _lastDisconnectedAt;
        private DateTimeOffset? _lastInboundActivityAt;
        private DateTimeOffset? _lastOutboundActivityAt;

        internal MQTTControl(IMqttClient mqttClient)
        {
            MQTTClient = mqttClient;
            MQTTClient.ConnectedAsync += MQTTClient_ConnectedAsync;
            MQTTClient.DisconnectedAsync += MQTTClient_DisconnectedAsync;
            MQTTClient.ApplicationMessageReceivedAsync += MQTTClient_ApplicationMessageReceivedAsync;
        }

        public IReadOnlyList<string> GetSubscribeTopicSnapshot()
        {
            lock (_subscribeTopicLocker)
            {
                return _subscribedTopics.ToList();
            }
        }

        public IReadOnlyList<MqttMessageTraceEntry> GetMessageTraceSnapshot()
        {
            lock (_messageTraceLocker)
            {
                return _messageTraces.ToList();
            }
        }

        public MqttRuntimeDiagnostics CaptureRuntimeDiagnostics()
        {
            int registeredSubscriptionCount;
            int activeSubscriptionCount;
            lock (_subscribeTopicLocker)
            {
                registeredSubscriptionCount = _subscribeTopicCache.Count;
                activeSubscriptionCount = _subscribedTopics.Count;
            }

            lock (_runtimeDiagnosticsLocker)
            {
                return new MqttRuntimeDiagnostics
                {
                    Configured = true,
                    Connected = MQTTClient.IsConnected,
                    RegisteredSubscriptionCount = registeredSubscriptionCount,
                    ActiveSubscriptionCount = activeSubscriptionCount,
                    LastConnectedAt = _lastConnectedAt,
                    LastDisconnectedAt = _lastDisconnectedAt,
                    LastInboundActivityAt = _lastInboundActivityAt,
                    LastOutboundActivityAt = _lastOutboundActivityAt,
                };
            }
        }

        private void AddMessageTrace(string direction, string topic, string payload, string qualityOfServiceLevel, bool retain)
        {
            lock (_messageTraceLocker)
            {
                _messageTraces.Add(new MqttMessageTraceEntry
                {
                    Time = DateTime.Now,
                    Direction = direction,
                    Topic = topic,
                    Payload = TrimTracePayload(payload),
                    QualityOfServiceLevel = qualityOfServiceLevel,
                    Retain = retain
                });

                if (_messageTraces.Count > MaxMessageTraceCount)
                {
                    _messageTraces.RemoveRange(0, _messageTraces.Count - MaxMessageTraceCount);
                }
            }
        }

        private static string TrimTracePayload(string payload)
        {
            const int maxPayloadLength = 8000;
            if (string.IsNullOrEmpty(payload) || payload.Length <= maxPayloadLength)
                return payload;

            return payload[..maxPayloadLength] + "...";
        }

        public async Task<bool> Connect(CancellationToken cancellationToken = default)
        {
            await _clientGate.WaitAsync(cancellationToken);
            try
            {
                _connectionWanted = true;
                return await ConnectCore(cancellationToken);
            }
            finally
            {
                _clientGate.Release();
            }
        }

        public async Task<bool> RecoverConnectionAsync(CancellationToken cancellationToken = default)
        {
            return await Connect(cancellationToken: cancellationToken) && IsConnectionReady();
        }

        private async Task<bool> ConnectCore(CancellationToken cancellationToken)
        {
            try
            {
                if (!MQTTClient.IsConnected)
                {
                    log.Info($"Connecting to MQTT: {Host}:{Port}");
                    var options = new MqttClientOptionsBuilder()
                        .WithTcpServer(Host, Port)
                        .WithClientId(Guid.NewGuid().ToString("N"))
                        .Build();
                    await MQTTClient.ConnectAsync(options, cancellationToken);
                }
                await ResubscribeTopics(cancellationToken);
                return MQTTClient.IsConnected;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                IsConnect = MQTTClient.IsConnected;
                return false;
            }
        }

        private bool IsConnectionReady()
        {
            lock (_subscribeTopicLocker)
            {
                return MQTTClient.IsConnected && _subscribedTopics.SetEquals(_subscribeTopicCache);
            }
        }

        private Task MQTTClient_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            lock (_subscribeTopicLocker)
            {
                _subscribedTopics.Clear();
            }

            log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT connected");
            RecordRuntimeActivity(RuntimeActivity.Connected);
            IsConnect = true;
            return Task.CompletedTask;
        }

        private async Task MQTTClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            AddMessageTrace("RECV", e.ApplicationMessage.Topic, payload, e.ApplicationMessage.QualityOfServiceLevel.ToString(), e.ApplicationMessage.Retain);
            RecordRuntimeActivity(RuntimeActivity.Inbound);

            if (log.IsDebugEnabled)
            {
                var message = $"{DateTime.Now:HH:mm:ss.fff} Received: {e.ApplicationMessage.Topic} {payload}, QoS: [{e.ApplicationMessage.QualityOfServiceLevel}], Retain: [{e.ApplicationMessage.Retain}]";
                log.Logger.Log(typeof(MQTTControl), log4net.Core.Level.Trace, message, null);
            }
            if (ApplicationMessageReceivedAsync != null)
            {
                await ApplicationMessageReceivedAsync(e);
            }
        }

        private Task MQTTClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT disconnected");
            RecordRuntimeActivity(RuntimeActivity.Disconnected);
            lock (_subscribeTopicLocker)
                _subscribedTopics.Clear();
            IsConnect = false;
            ScheduleReconnect();
            return Task.CompletedTask;
        }

        private void ScheduleReconnect()
        {
            if (_connectionWanted && !MQTTClient.IsConnected && Interlocked.CompareExchange(ref _reconnecting, 1, 0) == 0)
                _ = Task.Run(ReconnectAsync);
        }

        private async Task ReconnectAsync()
        {
            try
            {
                while (_connectionWanted && !MQTTClient.IsConnected)
                {
                    // broker 停止时限制重试频率；重连不占用 MQTTnet 的断线回调。
                    await Task.Delay(3000);
                    await _clientGate.WaitAsync();
                    try
                    {
                        if (_connectionWanted)
                            await ConnectCore(CancellationToken.None);
                    }
                    finally
                    {
                        _clientGate.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn(ex);
            }
            finally
            {
                Interlocked.Exchange(ref _reconnecting, 0);
                ScheduleReconnect();
            }
        }

        private readonly object _subscribeTopicLocker = new();
        private readonly HashSet<string> _subscribeTopicCache = new(StringComparer.Ordinal);
        private readonly HashSet<string> _subscribedTopics = new(StringComparer.Ordinal);
        public void SubscribeCache(string subscribeTopic)
        {
            if (string.IsNullOrEmpty(subscribeTopic)) return;

            lock (_subscribeTopicLocker)
            {
                _subscribeTopicCache.Add(subscribeTopic);
            }

            if (IsConnect)
            {
                _ = SubscribeAsyncClientAsync(subscribeTopic);
            }
        }

        public async Task DisconnectAsyncClient()
        {
            await _clientGate.WaitAsync();
            try
            {
                _connectionWanted = false;
                if (MQTTClient.IsConnected)
                    await MQTTClient.DisconnectAsync();
            }
            finally
            {
                _clientGate.Release();
            }
        }

        private async Task ResubscribeTopics(CancellationToken cancellationToken)
        {
            List<string> topics;
            lock (_subscribeTopicLocker)
            {
                topics = _subscribeTopicCache.ToList();
            }

            foreach (var topic in topics)
            {
                await SubscribeCore(topic, cancellationToken);
            }
        }
        public async Task SubscribeAsyncClientAsync(string topic)
        {
            if (string.IsNullOrEmpty(topic)) return;
            await _clientGate.WaitAsync();
            try
            {
                lock (_subscribeTopicLocker)
                    _subscribeTopicCache.Add(topic);
                await SubscribeCore(topic, CancellationToken.None);
            }
            finally
            {
                _clientGate.Release();
            }
        }

        private async Task SubscribeCore(string topic, CancellationToken cancellationToken)
        {
            lock (_subscribeTopicLocker)
                if (!MQTTClient.IsConnected || _subscribedTopics.Contains(topic)) return;

            try
            {
                var result = await MQTTClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build(), cancellationToken);
                if (!result.Items.Any(item => item.ResultCode <= MqttClientSubscribeResultCode.GrantedQoS2))
                {
                    log.Warn($"MQTT subscription rejected: {topic}");
                    return;
                }
                lock (_subscribeTopicLocker)
                    if (MQTTClient.IsConnected) _subscribedTopics.Add(topic);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Warn(ex);
            }
        }

        public async Task UnsubscribeAsyncClientAsync(string topic)
        {
            await _clientGate.WaitAsync();
            try
            {
                lock (_subscribeTopicLocker)
                {
                    _subscribeTopicCache.Remove(topic);
                    _subscribedTopics.Remove(topic);
                }
                if (MQTTClient.IsConnected)
                    await MQTTClient.UnsubscribeAsync(topic);
            }
            catch (Exception ex)
            {
                log.Warn(ex);
            }
            finally
            {
                _clientGate.Release();
            }
        }

        public async Task PublishAsyncClient(string topic, string msg, bool retained)
        {
            if (MQTTClient.IsConnected)
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(msg)
                    .WithRetainFlag(retained)
                    .Build();

                await MQTTClient.PublishAsync(message);
                AddMessageTrace("SEND", topic, msg, message.QualityOfServiceLevel.ToString(), message.Retain);
                RecordRuntimeActivity(RuntimeActivity.Outbound);
                log.Logger.Log(typeof(MQTTControl), log4net.Core.Level.Debug, $"{DateTime.Now:HH:mm:ss.fff} Published to '{topic}', message: '{msg}'", null);
            }
        }

        private void RecordRuntimeActivity(RuntimeActivity activity)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            lock (_runtimeDiagnosticsLocker)
            {
                switch (activity)
                {
                    case RuntimeActivity.Connected:
                        _lastConnectedAt = now;
                        break;
                    case RuntimeActivity.Disconnected:
                        _lastDisconnectedAt = now;
                        break;
                    case RuntimeActivity.Inbound:
                        _lastInboundActivityAt = now;
                        break;
                    case RuntimeActivity.Outbound:
                        _lastOutboundActivityAt = now;
                        break;
                }
            }
        }

        private enum RuntimeActivity
        {
            Connected,
            Disconnected,
            Inbound,
            Outbound,
        }
    }

}

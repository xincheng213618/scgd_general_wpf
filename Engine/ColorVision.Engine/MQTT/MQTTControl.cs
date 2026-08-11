#pragma warning disable CA1822,CS8603
using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using MQTTnet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public class MQTTControl : ViewModelBase, IConfigReloadParticipant
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MQTTControl));
        private const int MaxMessageTraceCount = 200;
        private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);

        private static MQTTControl _instance;
        private static readonly object _locker = new();
        public static MQTTControl GetInstance() { lock (_locker) { return _instance ??= new MQTTControl(); } }

        public static MQTTConfig Config => MQTTSetting.Instance.MQTTConfig;
        public static MQTTSetting Setting => MQTTSetting.Instance;

        private readonly MqttClientLifecycle _clientLifecycle;
        private readonly object _configOwnerLocker = new();
        private MqttConfigOwnerIdentity? _currentConfigOwner;
        private MQTTConfig? _currentOwnedConfig;
        private long _configOwnerGeneration;

        public IMqttClient MQTTClient
        {
            get => _clientLifecycle.Client!;
            set => _clientLifecycle.ReplaceClient(value);
        }

        public string ConfigReloadName => nameof(MQTTControl);

        public int ConfigReloadOrder => 100;

        internal Task<bool> CurrentConfigBindTask
        {
            get => _clientLifecycle.CurrentConfigBindTask;
        }

        internal Action? BeforeConnectTransition
        {
            get => _clientLifecycle.BeforeConnectTransition;
            set => _clientLifecycle.BeforeConnectTransition = value;
        }

        public bool IsConnect
        {
            get => _clientLifecycle.IsConnected;
        }

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

        public event EventHandler MQTTConnectChanged;

        private readonly object _messageTraceLocker = new();
        private readonly List<MqttMessageTraceEntry> _messageTraces = new();

        private MQTTControl()
            : this(() => new MqttClientFactory().CreateMqttClient())
        {
        }

        internal MQTTControl(Func<IMqttClient> clientFactory)
        {
            _clientLifecycle = new MqttClientLifecycle(
                log,
                clientFactory,
                MQTTClient_ConnectedAsync,
                MQTTClient_DisconnectedAsync,
                MQTTClient_ApplicationMessageReceivedAsync,
                RaiseConnectionStateChanged);
        }

        public void BindCurrentConfig(IConfigService currentConfig)
        {
            ArgumentNullException.ThrowIfNull(currentConfig);

            MQTTSetting currentSetting = currentConfig.GetRequiredService<MQTTSetting>();
            MqttConnectionConfig connectionConfig = MqttConnectionConfig.Capture(currentSetting.MQTTConfig);

            _clientLifecycle.Bind(
                connectionConfig,
                () => InstallCurrentConfigOwner(currentSetting));
        }

        internal MqttConfigOwnerIdentity CaptureCurrentConfigOwner()
        {
            lock (_configOwnerLocker)
            {
                return EnsureCurrentConfigOwnerNoLock();
            }
        }

        internal bool TrySelectCurrentConfig(MqttConfigOwnerIdentity configOwner, MQTTConfig mqttConfig)
        {
            ArgumentNullException.ThrowIfNull(configOwner);
            ArgumentNullException.ThrowIfNull(mqttConfig);

            lock (_configOwnerLocker)
            {
                if (!ReferenceEquals(configOwner, EnsureCurrentConfigOwnerNoLock()))
                    return false;

                configOwner.Setting.MQTTConfig = mqttConfig;
                _currentOwnedConfig = mqttConfig;
                return true;
            }
        }

        internal Task<bool> ConnectOwnedConfig(
            MqttConfigOwnerIdentity configOwner,
            MQTTConfig mqttConfig)
        {
            ArgumentNullException.ThrowIfNull(configOwner);
            ArgumentNullException.ThrowIfNull(mqttConfig);
            return _clientLifecycle.Connect(() => CaptureOwnedConnectionConfig(configOwner, mqttConfig));
        }

        internal Task<bool> TestConnectOwnedConfig(
            MqttConfigOwnerIdentity configOwner,
            MQTTConfig mqttConfig)
        {
            ArgumentNullException.ThrowIfNull(configOwner);
            ArgumentNullException.ThrowIfNull(mqttConfig);

            MqttConnectionConfig? connectionConfig = CaptureOwnedConnectionConfig(configOwner, mqttConfig);
            return connectionConfig == null
                ? Task.FromResult(false)
                : TestConnect(connectionConfig);
        }

        public IReadOnlyList<string> GetSubscribeTopicSnapshot()
        {
            lock (_subscribeTopicLocker)
            {
                return SubscribeTopic.ToList();
            }
        }

        public IReadOnlyList<MqttMessageTraceEntry> GetMessageTraceSnapshot()
        {
            lock (_messageTraceLocker)
            {
                return _messageTraces.ToList();
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

        public Task<bool> Connect() => _clientLifecycle.Connect(CaptureCurrentConnectionConfig);

        public Task<bool> Connect(MQTTConfig mqttConfig)
        {
            ArgumentNullException.ThrowIfNull(mqttConfig);

            // The authoritative selection is owned by this control, not by the publicly mutable
            // MQTTSetting.Instance.MQTTConfig property. A stale settings window therefore cannot
            // make C1 current merely by writing that property before calling Connect.
            return _clientLifecycle.Connect(() => CaptureOwnedConnectionConfig(null, mqttConfig));
        }

        private void InstallCurrentConfigOwner(MQTTSetting currentSetting)
        {
            lock (_configOwnerLocker)
            {
                MQTTSetting.Instance = currentSetting;
                _currentOwnedConfig = currentSetting.MQTTConfig;
                _currentConfigOwner = new MqttConfigOwnerIdentity(
                    currentSetting,
                    ++_configOwnerGeneration);
            }
        }

        private MqttConnectionConfig? CaptureCurrentConnectionConfig()
        {
            lock (_configOwnerLocker)
            {
                _ = EnsureCurrentConfigOwnerNoLock();
                return _currentOwnedConfig == null
                    ? null
                    : MqttConnectionConfig.Capture(_currentOwnedConfig);
            }
        }

        private MqttConnectionConfig? CaptureOwnedConnectionConfig(
            MqttConfigOwnerIdentity? expectedOwner,
            MQTTConfig mqttConfig)
        {
            lock (_configOwnerLocker)
            {
                MqttConfigOwnerIdentity currentOwner = EnsureCurrentConfigOwnerNoLock();
                if ((expectedOwner != null && !ReferenceEquals(expectedOwner, currentOwner))
                    || !ReferenceEquals(mqttConfig, _currentOwnedConfig))
                {
                    return null;
                }

                return MqttConnectionConfig.Capture(mqttConfig);
            }
        }

        private MqttConfigOwnerIdentity EnsureCurrentConfigOwnerNoLock()
        {
            if (_currentConfigOwner != null)
                return _currentConfigOwner;

            MQTTSetting currentSetting = MQTTSetting.Instance;
            _currentOwnedConfig = currentSetting.MQTTConfig;
            _currentConfigOwner = new MqttConfigOwnerIdentity(
                currentSetting,
                ++_configOwnerGeneration);
            return _currentConfigOwner;
        }

        private async Task MQTTClient_ConnectedAsync(MqttClientBinding binding, MqttClientConnectedEventArgs arg)
        {
            if (!_clientLifecycle.IsCurrent(binding))
                return;

            lock (_subscribeTopicLocker)
            {
                foreach (var topic in SubscribeTopic)
                {
                    AddSubscribeTopicCache(topic);
                }
                SubscribeTopic.Clear();
            }

            if (!_clientLifecycle.IsCurrent(binding))
                return;

            log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT connected");
            if (!_clientLifecycle.TrySetConnectionState(binding, true))
                return;

            await ResubscribeTopics(binding).ConfigureAwait(false);
        }

        private async Task MQTTClient_ApplicationMessageReceivedAsync(MqttClientBinding binding, MqttApplicationMessageReceivedEventArgs e)
        {
            if (!_clientLifecycle.IsCurrent(binding))
                return;

            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            AddMessageTrace("RECV", e.ApplicationMessage.Topic, payload, e.ApplicationMessage.QualityOfServiceLevel.ToString(), e.ApplicationMessage.Retain);

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

        private async Task MQTTClient_DisconnectedAsync(MqttClientBinding binding, MqttClientDisconnectedEventArgs arg)
        {
            if (!_clientLifecycle.TryScheduleReconnect(binding, out CancellationToken cancellationToken))
                return;

            log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT disconnected");
            if (!_clientLifecycle.TrySetConnectionState(binding, false))
                return;

            try
            {
                await Task.Delay(ReconnectDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _clientLifecycle.Reconnect(binding);
        }

        private void RaiseConnectionStateChanged()
        {
            EventHandler? handlers = MQTTConnectChanged;
            if (handlers != null)
            {
                foreach (EventHandler handler in handlers.GetInvocationList().Cast<EventHandler>())
                {
                    try
                    {
                        handler(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        log.Error("An MQTT connection-state subscriber failed.", ex);
                    }
                }
            }

            try
            {
                OnPropertyChanged(nameof(IsConnect));
            }
            catch (Exception ex)
            {
                log.Error("An MQTT IsConnect property-change subscriber failed.", ex);
            }
        }

        public Task<bool> TestConnect(MQTTConfig mqttConfig)
        {
            ArgumentNullException.ThrowIfNull(mqttConfig);
            return TestConnect(MqttConnectionConfig.Capture(mqttConfig));
        }

        private async Task<bool> TestConnect(MqttConnectionConfig connectionConfig)
        {
            var mqttClient = _clientLifecycle.CreateStandaloneClient();
            bool isConnected = false;

            try
            {
                var options = MqttClientLifecycle.BuildClientOptions(connectionConfig);
                await mqttClient.ConnectAsync(options);
                isConnected = mqttClient.IsConnected;
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                mqttClient.Dispose();
            }

            return isConnected;
        }

        internal sealed class MqttConfigOwnerIdentity
        {
            internal MqttConfigOwnerIdentity(MQTTSetting setting, long generation)
            {
                Setting = setting;
                Generation = generation;
            }

            internal MQTTSetting Setting { get; }

            internal long Generation { get; }
        }

        private readonly object _subscribeTopicLocker = new();
        private  List<string> _subscribeTopicCache = new();
        public void SubscribeCache(string subscribeTopic)
        {
            if (string.IsNullOrEmpty(subscribeTopic)) return;

            lock (_subscribeTopicLocker)
            {
                AddSubscribeTopicCache(subscribeTopic);
            }

            if (IsConnect)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SubscribeAsyncClientAsync(subscribeTopic);
                    }
                    catch (Exception ex)
                    {
                        log.Warn(ex);
                    }
                });
            }
        }

        public Task DisconnectAsyncClient() => _clientLifecycle.DisconnectAsync();

        public ObservableCollection<string> SubscribeTopic { get; } = new ObservableCollection<string>();

        private async Task ResubscribeTopics(MqttClientBinding binding)
        {
            List<string> topics;
            lock (_subscribeTopicLocker)
            {
                topics = _subscribeTopicCache.ToList();
            }

            foreach (var topic in topics)
            {
                await SubscribeAsyncClientAsync(binding, topic).ConfigureAwait(false);
            }
        }

        public async Task SubscribeAsyncClientAsync(string topic)
        {
            MqttClientBinding? binding = _clientLifecycle.GetCurrentBinding();
            if (binding == null)
                return;

            await SubscribeAsyncClientAsync(binding, topic).ConfigureAwait(false);
        }

        private async Task SubscribeAsyncClientAsync(MqttClientBinding binding, string topic)
        {
            if (string.IsNullOrEmpty(topic)) return;

            try
            {
                bool isSubscribed;
                lock (_subscribeTopicLocker)
                {
                    AddSubscribeTopicCache(topic);
                    isSubscribed = SubscribeTopic.Contains(topic);
                }

                if (!IsConnect || isSubscribed || !_clientLifecycle.IsCurrent(binding))
                    return;

                var topicFilter = new MqttTopicFilterBuilder().WithTopic(topic).Build();
                await binding.Client.SubscribeAsync(topicFilter).ConfigureAwait(false);

                if (!_clientLifecycle.IsCurrent(binding))
                    return;

                lock (_subscribeTopicLocker)
                {
                    if (!SubscribeTopic.Contains(topic))
                    {
                        SubscribeTopic.Add(topic);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn(ex);
            }
        }

        public async Task UnsubscribeAsyncClientAsync(string topic)
        {
            MqttClientBinding? binding = _clientLifecycle.GetCurrentBinding();
            if (binding?.Client.IsConnected == true)
            {
                try
                {
                    await binding.Client.UnsubscribeAsync(topic).ConfigureAwait(false);
                    if (!_clientLifecycle.IsCurrent(binding))
                        return;

                    lock (_subscribeTopicLocker)
                    {
                        SubscribeTopic.Remove(topic);
                        _subscribeTopicCache.Remove(topic);
                    }
                }
                catch (Exception ex)
                {
                    log.Warn(ex);
                }
            }
            else
            {
            }
        }

        private void AddSubscribeTopicCache(string topic)
        {
            if (!string.IsNullOrEmpty(topic) && !_subscribeTopicCache.Contains(topic))
            {
                _subscribeTopicCache.Add(topic);
            }
        }

        public async Task PublishAsyncClient(string topic, string msg, bool retained)
        {
            MqttClientBinding? binding = _clientLifecycle.GetCurrentBinding();
            if (binding?.Client.IsConnected == true)
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(msg)
                    .WithRetainFlag(retained)
                    .Build();

                await binding.Client.PublishAsync(message).ConfigureAwait(false);
                if (!_clientLifecycle.IsCurrent(binding))
                    return;

                AddMessageTrace("SEND", topic, msg, message.QualityOfServiceLevel.ToString(), message.Retain);
                log.Logger.Log(typeof(MQTTControl), log4net.Core.Level.Debug, $"{DateTime.Now:HH:mm:ss.fff} Published to '{topic}', message: '{msg}'", null);
            }
            return;
        }

    }

}

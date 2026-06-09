#pragma warning disable CA1822,CS8603
using ColorVision.Common.MVVM;
using log4net;
using MQTTnet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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

    public class MQTTControl : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MQTTControl));
        private const int MaxMessageTraceCount = 200;

        private static MQTTControl _instance;
        private static readonly object _locker = new();
        public static MQTTControl GetInstance() { lock (_locker) { return _instance ??= new MQTTControl(); } }

        public static MQTTConfig Config => MQTTSetting.Instance.MQTTConfig;
        public static MQTTSetting Setting => MQTTSetting.Instance;

        public IMqttClient MQTTClient { get; set; }

        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; MQTTConnectChanged?.Invoke(this, new EventArgs()); OnPropertyChanged(); } }
        private bool _IsConnect;

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

        public event EventHandler MQTTConnectChanged;

        private readonly object _messageTraceLocker = new();
        private readonly List<MqttMessageTraceEntry> _messageTraces = new();

        private MQTTControl()
        {
            MQTTClient = new MqttClientFactory().CreateMqttClient();
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

        private static string NormalizeHost(string host)
        {
            return string.IsNullOrWhiteSpace(host) ? null : host.Trim();
        }

        private static MqttClientOptions BuildClientOptions(MQTTConfig mqttConfig)
        {
            var host = NormalizeHost(mqttConfig.Host);

            return new MqttClientOptionsBuilder()
                .WithTcpServer(host, mqttConfig.Port)
                .WithCredentials(mqttConfig.UserName, mqttConfig.UserPwd)
                .WithClientId(Guid.NewGuid().ToString("N"))
                .Build();
        }

        public async Task<bool> Connect()=> await Connect(Config);
        public async Task<bool> Connect(MQTTConfig mqttConfig)
        {
            log.Info($"Connecting to MQTT: {mqttConfig}");

            IsConnect = false;

            try
            {
                MQTTClient.ConnectedAsync -= MQTTClient_ConnectedAsync;
                MQTTClient.DisconnectedAsync -= MQTTClient_DisconnectedAsync;
                MQTTClient.ApplicationMessageReceivedAsync -= MQTTClient_ApplicationMessageReceivedAsync;
                await MQTTClient.DisconnectAsync();
                MQTTClient?.Dispose();
                MQTTClient = new MqttClientFactory().CreateMqttClient();

                var options = BuildClientOptions(mqttConfig);

                MQTTClient.ConnectedAsync += MQTTClient_ConnectedAsync;
                MQTTClient.DisconnectedAsync += MQTTClient_DisconnectedAsync;
                MQTTClient.ApplicationMessageReceivedAsync += MQTTClient_ApplicationMessageReceivedAsync;
                await MQTTClient.ConnectAsync(options);
                IsConnect = true;
                return true;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                IsConnect = false;
                return false;
            }
        }

        private async Task MQTTClient_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            lock (_subscribeTopicLocker)
            {
                foreach (var topic in SubscribeTopic)
                {
                    AddSubscribeTopicCache(topic);
                }
                SubscribeTopic.Clear();
            }

            log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT connected");
            IsConnect = true;
            await ResubscribeTopics();
        }

        private async Task MQTTClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
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

        private async Task MQTTClient_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT disconnected");
            IsConnect = false;
            await Task.Delay(3000);
            _ = Connect();
        }

        public async Task<bool> TestConnect(MQTTConfig mqttConfig)
        {
            var mqttClient = new MqttClientFactory().CreateMqttClient();
            bool isConnected = false;

            try
            {
                var options = BuildClientOptions(mqttConfig);
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

        public async Task DisconnectAsyncClient()
        {
            if (MQTTClient?.IsConnected == true)
            {
                await MQTTClient.DisconnectAsync();
                MQTTClient.Dispose();
            }
        }

        public ObservableCollection<string> SubscribeTopic { get; } = new ObservableCollection<string>();

        private async Task ResubscribeTopics()
        {
            List<string> topics;
            lock (_subscribeTopicLocker)
            {
                topics = _subscribeTopicCache.ToList();
            }

            foreach (var topic in topics)
            {
                await SubscribeAsyncClientAsync(topic);
            }
        }
        public async Task SubscribeAsyncClientAsync(string topic)
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

                if (!IsConnect || isSubscribed)
                    return;

                var topicFilter = new MqttTopicFilterBuilder().WithTopic(topic).Build();
                await MQTTClient.SubscribeAsync(topicFilter);

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
            if (MQTTClient?.IsConnected == true)
            {
                try
                {
                    await MQTTClient.UnsubscribeAsync(topic);
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
            if (MQTTClient == null) return;
            if (MQTTClient.IsConnected)
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(msg)
                    .WithRetainFlag(retained)
                    .Build();

                await MQTTClient.PublishAsync(message);
                AddMessageTrace("SEND", topic, msg, message.QualityOfServiceLevel.ToString(), message.Retain);
                log.Logger.Log(typeof(MQTTControl), log4net.Core.Level.Debug, $"{DateTime.Now:HH:mm:ss.fff} Published to '{topic}', message: '{msg}'", null);
            }
            return;
        }
    }

}

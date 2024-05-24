using ColorVision.Common.MVVM;
using log4net;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.MQTT
{
    public class MQTTControl : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MQTTControl));
        private static MQTTControl _instance;
        private static readonly object _locker = new();
        public static MQTTControl GetInstance() { lock (_locker) { return _instance ??= new MQTTControl(); } }

        public event MQTTMsgHandler MQTTMsgChanged;

        public static MQTTConfig Config => MQTTSetting.Instance.MQTTConfig;
        public static MQTTSetting Setting => MQTTSetting.Instance;

        public IMqttClient MQTTClient { get; set; }
        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; MQTTConnectChanged?.Invoke(this, new EventArgs()); NotifyPropertyChanged(); } }
        private bool _IsConnect;
        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

        public event EventHandler MQTTConnectChanged;

        private MQTTControl()
        {

        }

        public async Task<bool> Connect()=> await Connect(Config);
        public async Task<bool> Connect(MQTTConfig mqttConfig)
        {
            log.Info($"Connecting to MQTT: {mqttConfig}");

            IsConnect = false;
            MQTTClient?.Dispose();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttConfig.Host, mqttConfig.Port)
                .WithCredentials(mqttConfig.UserName, mqttConfig.UserPwd)
                .WithClientId(Guid.NewGuid().ToString("N"))
                .Build();

            MQTTClient = new MqttFactory().CreateMqttClient();
            MQTTClient.ConnectedAsync += async e =>
            {
                log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT connected");
                MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} MQTT connected"));
                IsConnect = true;
                await ResubscribeTopics();
            };

            MQTTClient.DisconnectedAsync += async e =>
            {
                log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT disconnected");
                MQTTMsgChanged?.Invoke(new MQMsg(-1, $"{DateTime.Now:HH:mm:ss.fff} MQTT disconnected"));
                IsConnect = false;
                await Task.Delay(3000);
                await ReConnectAsync();
            };

            MQTTClient.ApplicationMessageReceivedAsync += async e =>
            {
                var message = $"{DateTime.Now:HH:mm:ss.fff} Received: {e.ApplicationMessage.Topic} {Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)}, QoS: [{e.ApplicationMessage.QualityOfServiceLevel}], Retain: [{e.ApplicationMessage.Retain}]";
                MQTTMsgChanged?.Invoke(new MQMsg(1, message, e.ApplicationMessage.Topic, Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)));
                if (ApplicationMessageReceivedAsync != null)
                {
                    await ApplicationMessageReceivedAsync(e);
                }
            };

            try
            {
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

        private async Task ReConnectAsync()
        {
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(Config.Host, Config.Port)
                .WithCredentials(Config.UserName, Config.UserPwd)
                .WithClientId(Guid.NewGuid().ToString("N"))
                .Build();

            await MQTTClient.ConnectAsync(options);
        }

        public async Task<bool> TestConnect(MQTTConfig mqttConfig)
        {
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttConfig.Host, mqttConfig.Port)
                .WithCredentials(mqttConfig.UserName, mqttConfig.UserPwd)
                .WithClientId(Guid.NewGuid().ToString("N"))
                .Build();

            var mqttClient = new MqttFactory().CreateMqttClient();
            bool isConnected = false;

            try
            {
                await mqttClient.ConnectAsync(options);
                isConnected = mqttClient.IsConnected;
                MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} MQTTTest connected"));
            }
            catch (Exception ex)
            {
                log.Error(ex);
                MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} MQTTTest connection failed"));
            }
            finally
            {
                mqttClient.Dispose();
            }

            return isConnected;
        }

        private readonly List<string> _subscribeTopicCache = new();
        public void SubscribeCache(string subscribeTopic)
        {
            if (string.IsNullOrEmpty(subscribeTopic)) return;

            if (IsConnect)
            {
                Task.Run(() => SubscribeAsyncClientAsync(subscribeTopic));
            }
            else
            {
                _subscribeTopicCache.Add(subscribeTopic);
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
            foreach (var topic in _subscribeTopicCache)
            {
                await SubscribeAsyncClientAsync(topic);
            }
            _subscribeTopicCache.Clear();
        }
        public async Task SubscribeAsyncClientAsync(string topic)
        {
            if (IsConnect && !SubscribeTopic.Contains(topic))
            {
                SubscribeTopic.Add(topic);
                try
                {
                    var topicFilter = new MqttTopicFilterBuilder().WithTopic(topic).Build();
                    await MQTTClient.SubscribeAsync(topicFilter);
                    MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} Subscribed to {topic}"));
                }
                catch (Exception ex)
                {
                    log.Warn(ex);
                    MQTTMsgChanged?.Invoke(new MQMsg(-1, $"{DateTime.Now:HH:mm:ss.fff} Subscription to {topic} failed"));
                }
            }
        }

        public async Task UnsubscribeAsyncClientAsync(string topic)
        {
            if (MQTTClient?.IsConnected == true)
            {
                try
                {
                    await MQTTClient.UnsubscribeAsync(topic);
                    SubscribeTopic.Remove(topic);
                    MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} Unsubscribed from {topic}"));
                }
                catch (Exception ex)
                {
                    log.Warn(ex);
                    MQTTMsgChanged?.Invoke(new MQMsg(-1, $"{DateTime.Now:HH:mm:ss.fff} Unsubscription from {topic} failed"));
                }
            }
            else
            {
                MQTTMsgChanged?.Invoke(new MQMsg(-1, $"{DateTime.Now:HH:mm:ss.fff} MQTTClient is not connected, unsubscription from {topic} failed"));
            }
        }

        public async Task PublishAsyncClient(string topic, string msg, bool retained)
        {
            if (MQTTClient == null) return;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(msg)
                .WithRetainFlag(retained)
                .Build();

            if (MQTTClient.IsConnected)
            {
                await MQTTClient.PublishAsync(message);
                MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} Published to '{topic}', message: '{msg}'", topic, msg));
            }
            else
            {
                MQTTMsgChanged?.Invoke(new MQMsg(-1, $"{DateTime.Now:HH:mm:ss.fff} MQTTClient is not connected", topic, msg));
            }
            return;
        }
    }

}

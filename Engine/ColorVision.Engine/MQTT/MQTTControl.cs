using ColorVision.Common.MVVM;
using log4net;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.MQTT
{
    public class MQTTControl : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MQTTControl));

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

        private MQTTControl()
        {
            MQTTClient = new MqttFactory().CreateMqttClient();
        }

        public async Task<bool> Connect()=> await Connect(Config);
        public async Task<bool> Connect(MQTTConfig mqttConfig)
        {
            log.Info($"Connecting to MQTT: {mqttConfig}");

            IsConnect = false;

            MQTTClient.ConnectedAsync -= MQTTClient_ConnectedAsync;
            MQTTClient.DisconnectedAsync -= MQTTClient_DisconnectedAsync;
            MQTTClient.ApplicationMessageReceivedAsync -= MQTTClient_ApplicationMessageReceivedAsync;
            await MQTTClient.DisconnectAsync();
            MQTTClient?.Dispose();
            MQTTClient = new MqttFactory().CreateMqttClient();
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(mqttConfig.Host, mqttConfig.Port)
                .WithCredentials(mqttConfig.UserName, mqttConfig.UserPwd)
                .WithClientId(Guid.NewGuid().ToString("N"))
                .Build();
            MQTTClient.ConnectedAsync += MQTTClient_ConnectedAsync;
            MQTTClient.DisconnectedAsync += MQTTClient_DisconnectedAsync;
            MQTTClient.ApplicationMessageReceivedAsync += MQTTClient_ApplicationMessageReceivedAsync;

            try
            {
                await MQTTClient.ConnectAsync(options);
                IsConnect = true;
                log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT connected");
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
            _subscribeTopicCache.AddRange(SubscribeTopic);
            SubscribeTopic.Clear();

            log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT connected");
            IsConnect = true;
            await ResubscribeTopics();
        }

        private async Task MQTTClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
             if (log.IsDebugEnabled)
            {
                var message = $"{DateTime.Now:HH:mm:ss.fff} Received: {e.ApplicationMessage.Topic} {Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)}, QoS: [{e.ApplicationMessage.QualityOfServiceLevel}], Retain: [{e.ApplicationMessage.Retain}]";
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
            Connect();
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

        private  List<string> _subscribeTopicCache = new();
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
                }
                catch (Exception ex)
                {
                    log.Warn(ex);
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
                log.Logger.Log(typeof(MQTTControl), log4net.Core.Level.Trace, $"{DateTime.Now:HH:mm:ss.fff} Published to '{topic}', message: '{msg}'", null);
            }
            return;
        }
    }

}

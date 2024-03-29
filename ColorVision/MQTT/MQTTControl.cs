﻿using ColorVision.Common.MVVM;
using ColorVision.Settings;
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

        public SoftwareConfig SoftwareConfig { get; set; }
        public MQTTConfig Config { get => SoftwareConfig.MQTTConfig; }
        public MQTTSetting Setting { get => SoftwareConfig.MQTTSetting; }

        public IMqttClient MQTTClient { get; set; }


        private MQTTControl()
        {
            SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
        }

        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; MQTTConnectChanged?.Invoke(this, new EventArgs());  NotifyPropertyChanged(); } }
        private bool _IsConnect;

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

        public event EventHandler MQTTConnectChanged;


        public async Task<bool> Connect()=> await Connect(Config);
        public async Task<bool> Connect(MQTTConfig MQTTConfig)
        {

            log.Info($"正在连接MQTT:{MQTTConfig}");


            IsConnect = false;
            MQTTClient?.Dispose();

            MqttClientOptionsBuilder OptionsBuilder = new MqttClientOptionsBuilder();
            OptionsBuilder.WithTcpServer(MQTTConfig.Host, MQTTConfig.Port); // 设置MQTT服务器地址
            if (!string.IsNullOrWhiteSpace(MQTTConfig.UserName))
                OptionsBuilder.WithCredentials(MQTTConfig.UserName, MQTTConfig.UserPwd);  // 设置鉴权参数
            OptionsBuilder.WithClientId(Guid.NewGuid().ToString("N"));  // 设置客户端序列号
            MqttClientOptions options = OptionsBuilder.Build();

            MQTTClient = new MqttFactory().CreateMqttClient();
            MQTTClient.ConnectedAsync += (arg) => {
                log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT连接成功");
                MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} MQTT连接成功"));
                IsConnect = true; return Task.CompletedTask; }; 
            MQTTClient.DisconnectedAsync += (arg) => {
                log.Info($"{DateTime.Now:HH:mm:ss.fff} MQTT失去连接");
                MQTTMsgChanged?.Invoke(new MQMsg(-1, $"{DateTime.Now:HH:mm:ss.fff} MQTT失去连接")); RunReconnectTask();
                IsConnect = false; return Task.CompletedTask; };
            MQTTClient.ApplicationMessageReceivedAsync += (arg) => {
                MQTTMsgChanged?.Invoke(new MQMsg(1,
                    $"{DateTime.Now:HH:mm:ss.fff} 接收：{arg.ApplicationMessage.Topic} {Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment)},消息等级Qos：[{arg.ApplicationMessage.QualityOfServiceLevel}]，是否保留：[{arg.ApplicationMessage.Retain}]",
                    arg.ApplicationMessage.Topic, Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment)));
                ApplicationMessageReceivedAsync?.Invoke(arg); return Task.CompletedTask; };
            try
            {
                await MQTTClient.ConnectAsync(options);
                IsConnect = true;
                foreach (var item in SubscribeTopicCache)
                    SubscribeAsyncClientAsync(item);
                foreach (var item in SubscribeTopic)
                    SubscribeAsyncClientAsync(item);
                SubscribeTopicCache.Clear();
                return IsConnect;
            }
            catch (Exception ex)
            {
                log.Error(ex);
                IsConnect = false;
                return IsConnect;
            }
        }
        private void RunReconnectTask()
        {
            Task.Run(async () => { Thread.Sleep(3000); await ReConnectAsync(); });
        }


        private async Task ReConnectAsync()
        {
            MqttClientOptionsBuilder mqttClientOptionsBuilder = new MqttClientOptionsBuilder();
            mqttClientOptionsBuilder.WithTcpServer(Config.Host, Config.Port);          // 设置MQTT服务器地址
            if (!string.IsNullOrEmpty(Config.UserName))
                mqttClientOptionsBuilder.WithCredentials(Config.UserName, Config.UserPwd);  // 设置鉴权参数
            mqttClientOptionsBuilder.WithClientId(Guid.NewGuid().ToString("N"));  // 设置客户端序列号
            MqttClientOptions options = mqttClientOptionsBuilder.Build();
            await MQTTClient.ConnectAsync(options);
        }
        public static async Task<bool> TestConnect(MQTTConfig MQTTConfig)
        {
            MqttClientOptionsBuilder mqttClientOptionsBuilder = new MqttClientOptionsBuilder();
            mqttClientOptionsBuilder.WithTcpServer(MQTTConfig.Host, MQTTConfig.Port);          // 设置MQTT服务器地址
            if (!string.IsNullOrEmpty(MQTTConfig.UserName))
                mqttClientOptionsBuilder.WithCredentials(MQTTConfig.UserName, MQTTConfig.UserPwd);  // 设置鉴权参数
            mqttClientOptionsBuilder.WithClientId(Guid.NewGuid().ToString("N"));  // 设置客户端序列号
            MqttClientOptions options = mqttClientOptionsBuilder.Build();

            IMqttClient MqttClient = new MqttFactory().CreateMqttClient();
            bool IsConnected =false;
            try
            {
                await MqttClient.ConnectAsync(options);
                IsConnected = MqttClient.IsConnected;
                GetInstance().MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} MQTTTest连接成功"));
            }
            catch (Exception ex)
            {
                log.Error(ex);
                GetInstance().MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} MQTTTest连接失败"));
            }
            finally
            {
                MqttClient.Dispose();
            }
            return IsConnected;
        }


        List<string> SubscribeTopicCache = new List<string>();
        public void SubscribeCache(string SubscribeTopic)
        {
            if (string.IsNullOrEmpty(SubscribeTopic))
                return;
            if (IsConnect)
            {
                SubscribeAsyncClientAsync(SubscribeTopic);
            }
            else
            {
                SubscribeTopicCache.Add(SubscribeTopic);
            }

        }

        public async Task DisconnectAsyncClient()
        {
            if (MQTTClient != null && MQTTClient.IsConnected)
            {
                await MQTTClient.DisconnectAsync();
                MQTTClient.Dispose();
            }
        }

        public ObservableCollection<string> SubscribeTopic { get; set; } = new ObservableCollection<string>();

        public async void SubscribeAsyncClientAsync(string topic) 
        {
            if (IsConnect)
            {
                if (!SubscribeTopic.Contains(topic))
                    SubscribeTopic.Add(topic);

                try
                {
                    MqttTopicFilter topicFilter = new MqttTopicFilterBuilder().WithTopic(topic).Build();
                    await MQTTClient.SubscribeAsync(topicFilter, CancellationToken.None);
                    MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} 订阅{topic}成功"));
                }
                catch (Exception ex)
                {
                    log.Warn(ex);
                    MQTTMsgChanged?.Invoke(new MQMsg(-1, $"{DateTime.Now:HH:mm:ss.fff} 订阅{topic}失败"));
                }

            }
        }

        public async Task UnsubscribeAsyncClientAsync(string topic)
        {
            if (MQTTClient.IsConnected)
            {
                try
                {
                    await MQTTClient.UnsubscribeAsync(topic, CancellationToken.None);
                    SubscribeTopic.Remove(topic);
                    MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} 取消订阅{topic}成功"));
                }
                catch (Exception ex)
                {
                    log.Warn(ex);
                    MQTTMsgChanged?.Invoke(new MQMsg(-1, $"{DateTime.Now:HH:mm:ss.fff} 取消订阅{topic}失败"));
                }
            }
            else
            {
                MQTTMsgChanged?.Invoke(new MQMsg(-1, $"{DateTime.Now:HH:mm:ss.fff} MQTTClient未开启连接，取消订阅{topic}失败"));
            }
        }

        public async Task PublishAsyncClient(string topic, string msg, bool retained)
        {
            if (MQTTClient ==null)
                return;
            MqttApplicationMessageBuilder mqttApplicationMessageBuilder = new MqttApplicationMessageBuilder();
            mqttApplicationMessageBuilder.WithTopic(topic)          // 主题
                                        .WithPayload(msg)           // 信息
                                        .WithRetainFlag(retained);  // 保留

            MqttApplicationMessage messageObj = mqttApplicationMessageBuilder.Build();
            if (MQTTClient.IsConnected)
            {
                await MQTTClient.PublishAsync(messageObj, CancellationToken.None);
                MQTTMsgChanged?.Invoke(new MQMsg(1, $"{DateTime.Now:HH:mm:ss.fff} 主题:'{topic}',信息:'{msg}'", topic, msg));
            }
            else
            {
                MQTTMsgChanged?.Invoke(new MQMsg(-1,$"{DateTime.Now:HH:mm:ss.fff} MQTTClient未开启连接",topic, msg));
            }
            return;
        }
    }

}

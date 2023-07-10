using ColorVision.Extension;
using ColorVision.MVVM;
using ColorVision.SettingUp;
using MQTTnet;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace ColorVision.MQTT
{
    public delegate void MQTTMsgHandler(ResultDataMQTT resultDataMQTT);

    public class MQTTControl : ViewModelBase
    {
        private static MQTTControl _instance;
        private static readonly object _locker = new();
        public static MQTTControl GetInstance() { lock (_locker) { return _instance ??= new MQTTControl(); } }

        private MQTTHelper _MQTTHelper;
        public MQTTHelper MQTTHelper { get => _MQTTHelper; set => _MQTTHelper = value; }

        public event MQTTMsgHandler MQTTMsgChanged;

        public SoftwareConfig SoftwareConfig { get; set; }
        public MQTTConfig MQTTConfig { get => SoftwareConfig.MQTTConfig; }

        private MQTTControl()
        {
            SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
            MQTTHelper = new MQTTHelper();
            Task.Run(() => Connect(MQTTConfig));
            MQTTHelper.MsgHandle += (s) => { MQTTMsgChanged?.Invoke(s); };
        }

        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        public string ConnectSign { get => _ConnectSign; private set { _ConnectSign = value; NotifyPropertyChanged(); } }
        private string _ConnectSign = "未连接";

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

        public event EventHandler Connected;


        private bool IsRun;
        public async Task<bool> Connect(MQTTConfig MQTTConfig)
        {
            MQTTHelper.MqttClient?.Dispose();

            if (IsRun) return false;
            IsRun = true;


            ConnectSign = "未连接";

            ResultDataMQTT resultDataMQTT = await MQTTHelper.CreateMQTTClientAndStart(MQTTConfig.Host, MQTTConfig.Port, MQTTConfig.UserName, MQTTConfig.UserPwd);
            if (resultDataMQTT.ResultCode != 1)
            {
                IsConnect = false;
                IsRun = false;
                return false;
            }
            if (MQTTHelper.MqttClient != null)
            {
                MQTTHelper.MqttClient.ApplicationMessageReceivedAsync += (arg) =>
                {
                    ApplicationMessageReceivedAsync.Invoke(arg); return Task.CompletedTask;
                };
                MQTTHelper.MqttClient.DisconnectedAsync += (arg) =>
                {
                    IsConnect = false; return Task.CompletedTask;
                };
            }

            IsConnect = true;
            ConnectSign = "已连接";
            Connected?.Invoke(this, new EventArgs());
            IsRun = false;
            foreach (var item in SubscribeTopicCache)
                SubscribeAsyncClient(item);
            foreach (var item in SubscribeTopic)
                SubscribeAsyncClient(item);
            SubscribeTopicCache.Clear();
            return IsConnect;
        }

        public static async Task<bool> TestConnect(MQTTConfig MQTTConfig)
        {
            MqttClientOptionsBuilder mqttClientOptionsBuilder = new MqttClientOptionsBuilder();
            mqttClientOptionsBuilder.WithTcpServer(MQTTConfig.Host, MQTTConfig.Port);          // 设置MQTT服务器地址
            if (!string.IsNullOrEmpty(MQTTConfig.UserName))
            {
                mqttClientOptionsBuilder.WithCredentials(MQTTConfig.UserName, MQTTConfig.UserPwd);  // 设置鉴权参数
            }
            mqttClientOptionsBuilder.WithClientId(Guid.NewGuid().ToString("N"));  // 设置客户端序列号
            MqttClientOptions options = mqttClientOptionsBuilder.Build();

            IMqttClient MqttClient = new MqttFactory().CreateMqttClient();
            bool IsConnected =false;
            try
            {
                await MqttClient.ConnectAsync(options);
                 IsConnected = MqttClient.IsConnected;
            }
            catch
            {

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
            if (IsConnect)
            {
                SubscribeAsyncClient(SubscribeTopic);
            }
            else
            {
                SubscribeTopicCache.Add(SubscribeTopic);
            }

        }




        public async Task DisconnectAsyncClient() => await MQTTHelper.DisconnectAsyncClient();

        public ObservableCollection<string> SubscribeTopic { get; set; } = new ObservableCollection<string>();

        public void SubscribeAsyncClient(string topic) 
        {
            if (IsConnect)
            {
                MQTTHelper.SubscribeAsyncClient(topic);
                if (!SubscribeTopic.Contains(topic))
                    SubscribeTopic.Add(topic);
            }
        }

        public void UnsubscribeAsyncClient(string topic)
        {
            if (IsConnect)
            {
                MQTTHelper.UnsubscribeAsyncClient(topic);
                if (SubscribeTopic == null) return;
                SubscribeTopic.Remove(topic);
            }
        }
           
        public async Task  PublishAsyncClient(string topic, string msg, bool retained) => await MQTTHelper.PublishAsyncClient(topic, msg, retained);
    }


}

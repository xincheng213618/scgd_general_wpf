using ColorVision.MVVM;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        private MQTTControl()
        {
            MQTTHelper = new MQTTHelper();
            IP = "192.168.3.225";
            Port = 1883;
            UName = "";
            uPwd = "";
        }

        private string _IP;
        public string IP { get => _IP; set { _IP = value; NotifyPropertyChanged(); } }

        private int _Port;
        public int Port { get => _Port; set { _Port = value; NotifyPropertyChanged(); } }

        private string _uName;
        public string UName { get => _uName; set { _uName = value; NotifyPropertyChanged(); } }

        private string _uPwd;
        public string uPwd { get => _uPwd; set { _uPwd = value; NotifyPropertyChanged(); } }

        private bool _IsConnect;
        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

        public async Task<bool> Connect()
        {
            await MQTTHelper.CreateMQTTClientAndStart(IP, Port, UName, uPwd, ShowLog);
            MQTTHelper._MqttClient.ApplicationMessageReceivedAsync += (arg) =>
            {
                ApplicationMessageReceivedAsync.Invoke(arg); return Task.CompletedTask;
            };
            MQTTHelper._MqttClient.DisconnectedAsync += async (arg) =>
            {
                IsConnect = false;
                await MQTTHelper.CreateMQTTClientAndStart(IP, Port, UName, uPwd, ShowLog);
                IsConnect = true;
            };
            IsConnect = true;
            SubscribeTopic = new ObservableCollection<string>();
            return IsConnect;
        }

        public event MQTTMsgHandler MQTTMsgChanged;

        private void ShowLog(ResultDataMQTT resultData_MQTT)
        {
            MQTTMsgChanged?.Invoke(resultData_MQTT);
        }

        public async Task DisconnectAsyncClient() => await MQTTHelper.DisconnectAsync_Client();

        public ObservableCollection<string> SubscribeTopic { get; set; }

        public void SubscribeAsyncClient(string topic) 
        {
            MQTTHelper.SubscribeAsync_Client(topic);
            if (!SubscribeTopic.Contains(topic))
                SubscribeTopic.Add(topic);
        }


        public void UnsubscribeAsyncClient(string topic)
        {
            MQTTHelper.UnsubscribeAsync_Client(topic);
            SubscribeTopic.Remove(topic);
        }

        public async Task  PublishAsyncClient(string topic, string msg, bool retained) => await MQTTHelper.PublishAsync_Client(topic, msg, retained);
    }


}

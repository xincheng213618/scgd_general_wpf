using ColorVision.MVVM;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;

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
            UPwd = "";
            Task.Run(() => Connect());
        }

        public string IP { get => _IP;
            set
            {
                Regex reg = new Regex("^(?:[0-9]{1,3}\\.){3}[0-9]{1,3}$");
                if (reg.IsMatch(value))
                {
                    _IP = value; NotifyPropertyChanged();
                }
            }
        }
        private string _IP;

        public int Port { get => _Port; 
            set {
                Regex reg = new Regex("([0-9]|[1-9]\\d{1,3}|[1-5]\\d{4}|6[0-4]\\d{3}|65[0-4]\\d{2}|655[0-2]\\d|6553[0-5])");
                if (reg.IsMatch(value.ToString()))
                {
                    _Port = value; NotifyPropertyChanged();
                }

               } }
        private int _Port;

        public string UName { get => _uName; set { _uName = value; NotifyPropertyChanged(); } }
        private string _uName;

        public string UPwd { get => _uPwd; set { _uPwd = value; NotifyPropertyChanged(); } }
        private string _uPwd;

        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

        public event EventHandler Connected;


        public async Task<bool> Connect()
        {
            if (IsConnect)
            {
                Connected?.Invoke(this, new EventArgs());
                return true;
            }

            ResultDataMQTT resultDataMQTT = await MQTTHelper.CreateMQTTClientAndStart(IP, Port, UName, UPwd, ShowLog);
            if (resultDataMQTT.ResultCode !=1)
            {
                IsConnect = false;
                return false;
            }

            MQTTHelper._MqttClient.ApplicationMessageReceivedAsync += (arg) =>
            {
                ApplicationMessageReceivedAsync.Invoke(arg); return Task.CompletedTask;
            };
            MQTTHelper._MqttClient.DisconnectedAsync += async (arg) =>
            {
                IsConnect = false;
                await MQTTHelper.CreateMQTTClientAndStart(IP, Port, UName, UPwd, ShowLog);
                IsConnect = true;
            };
            IsConnect = true;
            SubscribeTopic = new ObservableCollection<string>();
            Connected?.Invoke(this, new EventArgs());
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
            if (IsConnect)
            {
                MQTTHelper.UnsubscribeAsync_Client(topic);
                SubscribeTopic.Remove(topic);
            }

        }
           
        public async Task  PublishAsyncClient(string topic, string msg, bool retained) => await MQTTHelper.PublishAsync_Client(topic, msg, retained);
    }


}

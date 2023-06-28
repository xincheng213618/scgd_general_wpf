using ColorVision.MVVM;
using ColorVision.SettingUp;
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

        public event MQTTMsgHandler MQTTMsgChanged;

        public MQTTConfig MQTTConfig { get; set; }

        private MQTTControl()
        {
            MQTTConfig =GlobalSetting.GetInstance().SoftwareConfig.MQTTConfig;
            MQTTHelper = new MQTTHelper();
            Task.Run(() => Connect());
            MQTTHelper.MsgHandle += (s) => { MQTTMsgChanged?.Invoke(s); };
        }


        //public string IP { get => MQTTConfig.IP;
        //    set
        //    {
        //        Regex reg = new Regex("^(?:[0-9]{1,3}\\.){3}[0-9]{1,3}$");
        //        if (reg.IsMatch(value))
        //        {
        //            MQTTConfig.IP = value; NotifyPropertyChanged();
        //        }
        //    }
        //}


        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        public string ConnectSign { get => _ConnectSign; private set { _ConnectSign = value; NotifyPropertyChanged(); } }
        private string _ConnectSign = "未连接";

        public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

        public event EventHandler Connected;


        public async Task<bool> Connect()
        {
            if (IsConnect)
            {
                Connected?.Invoke(this, new EventArgs());
                return true;
            }

            ResultDataMQTT resultDataMQTT = await MQTTHelper.CreateMQTTClientAndStart(MQTTConfig.Host, MQTTConfig.Port, MQTTConfig.UserName, MQTTConfig.UserPwd);
            if (resultDataMQTT.ResultCode != 1)
            {
                IsConnect = false;
                return false;
            }

            MQTTHelper.MqttClient.ApplicationMessageReceivedAsync += (arg) =>
            {
                ApplicationMessageReceivedAsync.Invoke(arg); return Task.CompletedTask;
            };
            MQTTHelper.MqttClient.DisconnectedAsync += async (arg) =>
            {
                IsConnect = false;
                await MQTTHelper.CreateMQTTClientAndStart(MQTTConfig.Host, MQTTConfig.Port, MQTTConfig.UserName, MQTTConfig.UserPwd);
                IsConnect = true;
            };
            IsConnect = true;
            _ConnectSign = "已连接";
            SubscribeTopic = new ObservableCollection<string>();
            Connected?.Invoke(this, new EventArgs());
            return IsConnect;
        }



        public async Task DisconnectAsyncClient() => await MQTTHelper.DisconnectAsyncClient();

        public ObservableCollection<string> SubscribeTopic { get; set; }

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

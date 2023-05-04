#pragma warning disable CS4014
using ColorVision.MVVM;
using ColorVision.Util;
using MQTTnet.Client;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision
{

    public class MQTTCamera:IDisposable
    {

        private static MQTTCamera _instance;
        private static readonly object _locker = new();
        public static MQTTCamera GetInstance() { lock (_locker) { return _instance ??= new MQTTCamera(); } }


        private MQTTControl MQTTControl;

        private string SubscribeTopic;

        private MQTTCamera()
        {
            MQTTControl = MQTTControl.GetInstance();
            Task.Run(MQTTControlInit);
        }

        private async void MQTTControlInit()
        {
            if (!MQTTControl.IsConnect)
                await MQTTControl.Connect();
            SubscribeTopic = "topic2";
            MQTTControl.SubscribeAsyncClient(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
        }



        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
                if (Msg == "InitCamere")
                {
                    MessageBox.Show("InitCamere");
                }
                else if (Msg == "AddCalibration")
                {
                    MessageBox.Show("AddCalibration");
                }
                else if (Msg == "OpenCamere")
                {
                    MessageBox.Show("OpenCamere");
                }
                else if (Msg == "GetData")
                {
                    MessageBox.Show("GetData");
                }
            }
            return Task.CompletedTask;
        }

        public bool InitCamere()
        {
            MQTTControl.PublishAsyncClient("topic1", "InitCamere", false);
            return true;
        }
        public bool AddCalibration()
        {
            MQTTControl.PublishAsyncClient("topic1", "AddCalibration", false);
            return true;
        }
        public bool OpenCamera()
        {
            MQTTControl.PublishAsyncClient("topic1", "OpenCamere", false);
            return true;
        }

        public bool GetData()
        {
            MQTTControl.PublishAsyncClient("topic1", "GetData", false);
            return true;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }


    public delegate void MQTTMsgHandler(ResultDataMQTT resultDataMQTT);

    public class MQTTControl:ViewModelBase
    {
        private static MQTTControl _instance;
        private static readonly object _locker = new();
        public static MQTTControl GetInstance() { lock (_locker) { return _instance ??= new MQTTControl(); } }

        private MQTTHelper _MQTTHelper;
        public MQTTHelper MQTTHelper { get => _MQTTHelper; set => _MQTTHelper = value; }

        private MQTTControl()
        {
            MQTTHelper = new MQTTHelper();
            this.IP = "192.168.3.225";
            this.Port = 1883;
            this.uName = "";
            this.uPwd = "";
        }

        private string _IP;
        public string IP { get => _IP; set { _IP = value; NotifyPropertyChanged(); } }

        private int _Port;
        public int Port { get => _Port; set { _Port = value; NotifyPropertyChanged(); } }

        private string _uName;
        public string uName { get => _uName; set { _uName = value; NotifyPropertyChanged(); } }

        private string _uPwd;
        public string uPwd { get => _uPwd; set { _uPwd = value; NotifyPropertyChanged(); } }

        private bool _IsConnect;
        public bool IsConnect { get => _IsConnect; }

         public event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync;

        public async Task<bool> Connect()
        {
            await MQTTHelper.CreateMQTTClientAndStart(IP, Port, uName, uPwd, ShowLog);
            MQTTHelper._MqttClient.ApplicationMessageReceivedAsync += (arg) => { 
                ApplicationMessageReceivedAsync.Invoke(arg); return Task.CompletedTask; };
            _IsConnect = true;
            return IsConnect;
        }

        public event MQTTMsgHandler MQTTMsgChanged;

        private void ShowLog(ResultDataMQTT resultData_MQTT)
        {
            MQTTMsgChanged?.Invoke(resultData_MQTT);
        }

        public void DisconnectAsyncClient() => MQTTHelper.DisconnectAsync_Client();

        public void SubscribeAsyncClient(string topic) => MQTTHelper.SubscribeAsync_Client(topic);

        public void UnsubscribeAsyncClient(string topic) => MQTTHelper.UnsubscribeAsync_Client(topic);

        public void PublishAsyncClient(string topic, string msg, bool retained) => MQTTHelper.PublishAsync_Client(topic, msg, retained);
    }



    public partial class MQTTDemo : Window
    {

        MQTTControl mQTTControl;
        public MQTTDemo()
        {
            InitializeComponent();
            mQTTControl = MQTTControl.GetInstance();

            this.DataContext = mQTTControl;
        }


        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (!mQTTControl.IsConnect)
                await mQTTControl.Connect();
            mQTTControl.MQTTMsgChanged += ShowLog;
        }

        private void ShowLog(ResultDataMQTT resultData_MQTT)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                TextBoxResult.Text = $"\r\n返回结果：{resultData_MQTT.ResultCode}，返回信息：{resultData_MQTT.ResultMsg}" + TextBoxResult.Text;
            }));
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            mQTTControl.DisconnectAsyncClient();
        }

        private void Subscribe_Click(object sender, RoutedEventArgs e)
        {
            mQTTControl.SubscribeAsyncClient(TextBoxSubscribe.Text);
        }

        private void UnSubscribe_Click(object sender, RoutedEventArgs e)
        {
            mQTTControl.UnsubscribeAsyncClient(TextBoxSubscribe.Text);
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            mQTTControl.PublishAsyncClient(TextBoxSubscribe1.Text, TextBoxSend.Text, CheckBoxRetained.IsChecked==true);
        }

    }
}

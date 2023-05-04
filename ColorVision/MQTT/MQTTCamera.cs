#pragma warning disable CS4014, CS0612
using MQTTnet.Client;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.MQTT
{
    public class MQTTCamera : IDisposable
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
}

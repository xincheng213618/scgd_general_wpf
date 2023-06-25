using ColorVision.MVVM;
using HslCommunication.MQTT;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media.Media3D;

namespace ColorVision.MQTT
{
    public class FlowControlData : ViewModelBase
    {
        public string Version { get => _Version; set { _Version = value; NotifyPropertyChanged(); } }
        private string _Version;
        public string ServiceName { get => _ServiceName; set { _ServiceName = value; NotifyPropertyChanged(); } }
        private string _ServiceName;

        public string EventName { get => _EventName; set { _EventName = value; NotifyPropertyChanged(); } }
        private string _EventName;

        public int ServiceID { get => _ServiceID; set { _ServiceID = value; NotifyPropertyChanged(); } }
        private int _ServiceID;

        public string SerialNumber { get => _SerialNumber; set { _SerialNumber = value; NotifyPropertyChanged(); } }
        private string _SerialNumber;

        public string MsgID { get => _MsgID; set { _MsgID = value; NotifyPropertyChanged(); } }
        private string _MsgID;

        [JsonProperty("params")]
        public dynamic Params { get => _Params; set { _Params = value; NotifyPropertyChanged(); } }
        private dynamic _Params;
    }

    public class FlowControl
    {

        public FlowControlData FlowControlData { get; set; }

        MQTTControl MQTTControl;


        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        public FlowControl(MQTTControl mQTTControl,string topic)
        {
            this.MQTTControl = mQTTControl;
            this.SendTopic = "SYS.CMD." +topic;
            this.SubscribeTopic = "SYS.STATUS." + topic;
            MQTTControl.SubscribeAsyncClient(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MQTTControl_ApplicationMessageReceivedAsync;
        }


        public void Start()
        {
            string svrName = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            FlowEngineLib.CVBaseDataFlow baseEvent = new FlowEngineLib.CVBaseDataFlow(svrName, "Start", "CV202306211100006");

            string Msg = JsonConvert.SerializeObject(baseEvent);
            Application.Current.Dispatcher.Invoke(() => FlowMsg?.Invoke(Msg, new EventArgs()));
            Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, Msg, false));
        }



        public event EventHandler FlowCompleted;
        public event EventHandler FlowMsg;
        public event EventHandler FlowData;



        private Task MQTTControl_ApplicationMessageReceivedAsync(MQTTnet.Client.MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                Application.Current.Dispatcher.Invoke(() => FlowMsg?.Invoke(Msg, new EventArgs()));
                try
                {
                    FlowControlData json = JsonConvert.DeserializeObject<FlowControlData>(Msg);
                    if (json == null)
                        return Task.CompletedTask;
                    FlowControlData = json;
                    Application.Current.Dispatcher.Invoke(() => FlowData?.Invoke(FlowControlData, new EventArgs()));
                    if (FlowControlData.EventName == "Completed")
                    {
                        Application.Current.Dispatcher.Invoke(() => FlowCompleted?.Invoke(FlowControlData, new EventArgs()));
                    }
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            return Task.CompletedTask;
        }



    }
}

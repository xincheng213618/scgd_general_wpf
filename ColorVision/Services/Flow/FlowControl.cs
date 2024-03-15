using ColorVision.MQTT;
using ColorVision.MVVM;
using log4net;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Services.Flow
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
        private static readonly ILog logger = LogManager.GetLogger(typeof(FlowControl));

        string svrName = "FlowControl";
        string devName = "DEV01";
        public FlowControlData FlowControlData { get; set; }

        private MQTTControl MQTTControl;
        private FlowEngineLib.FlowEngineControl flowEngine;

        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }
        public string SerialNumber { get; set; }

        public FlowControl(MQTTControl mQTTControl, string topic)
        {
            MQTTControl = mQTTControl;
            SendTopic = "FLOW/CMD/" + topic;
            SubscribeTopic = "FLOW/STATUS/" + topic;
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MQTTControl_ApplicationMessageReceivedAsync;
        }

        public FlowControl(MQTTControl mQTTControl, FlowEngineLib.FlowEngineControl flowEngine) : this(mQTTControl, flowEngine.GetStartNodeName())
        {
            this.flowEngine = flowEngine;
        }

        public void Stop()
        {
            if (flowEngine == null)
            {
                FlowEngineLib.Base.CVBaseDataFlow baseEvent = new FlowEngineLib.Base.CVBaseDataFlow(svrName, devName, "Stop", SerialNumber, string.Empty);

                string Msg = JsonConvert.SerializeObject(baseEvent);
                Application.Current.Dispatcher.Invoke(() => FlowMsg?.Invoke(Msg, new EventArgs()));
                Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, Msg, false));

            }
            else
            {
                flowEngine.StopNode(SerialNumber);
            }
        }

        public void Start(string sn)
        {
            SerialNumber = sn;
            if (flowEngine == null)
            {
                FlowEngineLib.Base.CVBaseDataFlow baseEvent = new FlowEngineLib.Base.CVBaseDataFlow(svrName, devName, "Start", sn, string.Empty);

                string Msg = JsonConvert.SerializeObject(baseEvent);
                Application.Current.Dispatcher.Invoke(() => FlowMsg?.Invoke(Msg, new EventArgs()));
                Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, Msg, false));

            }
            else
            {
                flowEngine.StartNode(sn, ServiceManager.GetInstance().ServiceTokens);
            }
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
                    if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                    {
                        Application.Current.Dispatcher.Invoke(() => FlowCompleted?.Invoke(FlowControlData, new EventArgs()));
                    }
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Application.Current.MainWindow, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                }
            }

            return Task.CompletedTask;
        }
    }
}

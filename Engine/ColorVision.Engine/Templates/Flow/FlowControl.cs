using ColorVision.Common.MVVM;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using FlowEngineLib;
using log4net;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow
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
    public class FlowControl : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowControl));
        private MQTTControl MQTTControl;
        private FlowEngineControl flowEngine;

        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }
        public string SerialNumber { get; set; }

        public FlowControl(MQTTControl mQTTControl, string topic)
        {
            MQTTControl = mQTTControl;
            SendTopic = "MQTTRCService/Flow/RC_local";
            SubscribeTopic = "FLOW/STATUS/" + topic;
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MQTTControl_ApplicationMessageReceivedAsync;
        }

        public FlowControl(MQTTControl mQTTControl, FlowEngineControl flowEngine) : this(mQTTControl, flowEngine.GetStartNodeName())
        {
            this.flowEngine = flowEngine;
        }

        private readonly object _lock = new object();
        private bool _IsFlowRun;

        public bool IsFlowRun
        {
            get
            {
                lock (_lock)
                {
                    return _IsFlowRun;
                }
            }
            set
            {
                lock (_lock)
                {
                    _IsFlowRun = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public void Stop()
        {
            FlowCompleted = null;
            FlowMsg = null;
            FlowData = null;
            flowEngine.Finished -= Finished;

            flowEngine.StopNode(SerialNumber);
            IsFlowRun = false;
            FlowConfig.Instance.FlowRun = false;
        }

        public void Start(string sn)
        {
            IsFlowRun = true;
            FlowConfig.Instance.FlowRun = true;
            SerialNumber = sn;

            var tol = MqttRCService.GetInstance().ServiceTokens;
            flowEngine.Finished -= Finished;
            flowEngine.Finished += Finished;
            flowEngine.StartNode(sn, tol);
        }
        public void Finished(object sender, FlowEngineEventArgs e)
        {
            IsFlowRun = false;
            FlowControlData data = new FlowControlData();
            data.EventName = e.Status.ToString();
            data.Params = e.Message;
            data.SerialNumber =e.SerialNumber;
            Application.Current.Dispatcher.Invoke(() => FlowCompleted?.Invoke(data, new EventArgs()));
            FlowConfig.Instance.FlowRun = false;
        }


        public event EventHandler? FlowCompleted;
        public event EventHandler? FlowMsg;
        public event EventHandler? FlowData;

        private Task MQTTControl_ApplicationMessageReceivedAsync(MQTTnet.Client.MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                log.Info(Msg);
                Application.Current.Dispatcher.Invoke(() => FlowMsg?.Invoke(Msg, new EventArgs()));
                try
                {
                    FlowControlData json = JsonConvert.DeserializeObject<FlowControlData>(Msg);
                    if (json == null)
                        return Task.CompletedTask;
                    IsFlowRun = false;
                    Application.Current.Dispatcher.Invoke(() => FlowData?.Invoke(json, new EventArgs()));
                    if (json.EventName == "Completed" || json.EventName == "Canceled" || json.EventName == "OverTime" || json.EventName == "Failed")
                    {
                         Application.Current.Dispatcher.Invoke(() => FlowCompleted?.Invoke(json, new EventArgs()));
                    }
                    FlowConfig.Instance.FlowRun = false;
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
                }
            }
            return Task.CompletedTask;
        }
    }
}

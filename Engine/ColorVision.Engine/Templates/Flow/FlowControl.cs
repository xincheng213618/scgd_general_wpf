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
    public class FlowControl : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowControl));

        string svrName = "FlowControl";
        string devName = "DEV01";
        public FlowControlData FlowControlData { get; set; }

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
            if (flowEngine == null)
            {
                FlowEngineLib.Base.CVBaseDataFlow baseEvent = new(svrName, devName, "Stop", SerialNumber, string.Empty);

                string Msg = JsonConvert.SerializeObject(baseEvent);
                Application.Current.Dispatcher.Invoke(() => FlowMsg?.Invoke(Msg, new EventArgs()));
                Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, Msg, false));

            }
            else
            {
                ClearEventHandler();
                flowEngine.StopNode(SerialNumber);
            }
            IsFlowRun = false;
            FlowConfig.Instance.FlowRun = false;
        }

        public void Start(string sn)
        {
            IsFlowRun = true;
            FlowConfig.Instance.FlowRun = true;
            SerialNumber = sn;
            if (flowEngine == null)
            {
                FlowEngineLib.Base.CVBaseDataFlow baseEvent = new(svrName, devName, "Start", sn, string.Empty);

                string Msg = JsonConvert.SerializeObject(baseEvent);
                Application.Current.Dispatcher.Invoke(() => FlowMsg?.Invoke(Msg, new EventArgs()));
                Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, Msg, false));

            }
            else
            {
                var tol = MqttRCService.GetInstance().ServiceTokens;
                flowEngine.StartNode(sn, tol);
                flowEngine.Finished -= Finished;
                flowEngine.Finished += Finished;
            }
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

        public void ClearEventHandler()
        {
            FlowCompleted = null;
            FlowMsg = null;
            FlowData = null;
            flowEngine.Finished -= Finished;
        }

        private Task MQTTControl_ApplicationMessageReceivedAsync(MQTTnet.Client.MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                log.Debug(Msg);
                Application.Current.Dispatcher.Invoke(() => FlowMsg?.Invoke(Msg, new EventArgs()));
                try
                {
                    FlowControlData json = JsonConvert.DeserializeObject<FlowControlData>(Msg);
                    if (json == null)
                        return Task.CompletedTask;
                    FlowControlData = json;
                    IsFlowRun = false;
                    Application.Current.Dispatcher.Invoke(() => FlowData?.Invoke(FlowControlData, new EventArgs()));
                    if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                    {
                         Application.Current.Dispatcher.Invoke(() => FlowCompleted?.Invoke(FlowControlData, new EventArgs()));
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

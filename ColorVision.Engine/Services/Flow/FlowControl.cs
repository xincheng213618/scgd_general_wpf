using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MQTT;
using ColorVision.Services.Types;
using FlowEngineLib;
using log4net;
using MQTTMessageLib.Flow;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Services.Flow
{

    public class FlowControl  :ViewModelBase
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
            //SendTopic = "FLOW/CMD/" + topic;
            SendTopic = "MQTTRCService/Flow/RC_local";
            SubscribeTopic = "FLOW/STATUS/" + topic;
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MQTTControl_ApplicationMessageReceivedAsync;
        }

        public FlowControl(MQTTControl mQTTControl, FlowEngineLib.FlowEngineControl flowEngine) : this(mQTTControl, flowEngine.GetStartNodeName())
        {
            this.flowEngine = flowEngine;
        }

        public bool IsFlowRun { get => _IsFlowRun;set { _IsFlowRun = value; NotifyPropertyChanged(); } }
        private bool _IsFlowRun;

        public void Stop()
        {
            IsFlowRun = false;
            if (flowEngine == null)
            {
                FlowEngineLib.Base.CVBaseDataFlow baseEvent = new(svrName, devName, "Stop", SerialNumber, string.Empty);

                string Msg = JsonConvert.SerializeObject(baseEvent);
                Application.Current.Dispatcher.Invoke(() => FlowMsg?.Invoke(Msg, new EventArgs()));
                Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, Msg, false));

            }
            else
            {
                flowEngine.StopNode(SerialNumber);
            }
            ClearEventHandler();
        }
        public void Stop(FlowParam flowParam)
        {
            var serviceInfo = ServiceManager.GetInstance().GetServiceInfo(ServiceTypes.Flow, string.Empty);
            if (serviceInfo == null)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到"); return;
            }
            devName = serviceInfo.Devices.First().Key;
            MQTTFlowStop req = new(serviceInfo.ServiceCode, devName, SerialNumber, serviceInfo.Token);
            string Msg = JsonConvert.SerializeObject(req);
            Application.Current.Dispatcher.Invoke(() => FlowMsg?.Invoke(Msg, new EventArgs()));
            Task.Run(() => MQTTControl.PublishAsyncClient(serviceInfo.PublishTopic, Msg, false));
        }

        public void Start(string sn)
        {
            IsFlowRun = true;
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
                var tol = ServiceManager.GetInstance().ServiceTokens;
                flowEngine.StartNode(sn, tol);
            }
        }

        public void Start(string sn, FlowParam flowParam)
        {
            var serviceInfo = ServiceManager.GetInstance().GetServiceInfo(ServiceTypes.Flow, string.Empty);
            if (serviceInfo != null)
            {
                SerialNumber = sn;
                devName = serviceInfo.Devices.First().Key;
                DeviceFlowRunParam<MQTTServiceInfo> data = new()
                {
                    Services = ServiceManager.GetInstance().GetServiceJsonList(),
                    TemplateParam = new MQTTMessageLib.CVTemplateParam() { ID = flowParam.Id, Name = flowParam.Name }
                };
                MQTTFlowRun<MQTTServiceInfo> req = new(serviceInfo.ServiceCode, devName, sn, serviceInfo.Token, data);
                string Msg = JsonConvert.SerializeObject(req);
                Application.Current.Dispatcher.Invoke(() => FlowMsg?.Invoke(Msg, new EventArgs()));
                Task.Run(() => MQTTControl.PublishAsyncClient(serviceInfo.PublishTopic, Msg, false));
            }
        }

        public event EventHandler? FlowCompleted;
        public event EventHandler? FlowMsg;
        public event EventHandler? FlowData;

        public void ClearEventHandler()
        {
            FlowCompleted = null;
            FlowMsg = null;
            FlowData = null;
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

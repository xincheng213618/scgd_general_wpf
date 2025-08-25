using ColorVision.Common.MVVM;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow
{
    public enum FlowStatus
    {
        Ready,
        Runing,
        Paused,
        Failed,
        Canceled,
        OverTime,
        Completed
    }

    public class FlowControlData : ViewModelBase
    {
        public string Version { get => _Version; set { _Version = value; OnPropertyChanged(); } }
        private string _Version;
        public string ServiceName { get => _ServiceName; set { _ServiceName = value; OnPropertyChanged(); } }
        private string _ServiceName;

        public string EventName { get => _EventName; set { _EventName = value; OnPropertyChanged(); } }
        private string _EventName;

        public int ServiceID { get => _ServiceID; set { _ServiceID = value; OnPropertyChanged(); } }
        private int _ServiceID;

        public string SerialNumber { get => _SerialNumber; set { _SerialNumber = value; OnPropertyChanged(); } }
        private string _SerialNumber;
        public string StartNodeName { get; set; }
        public string Message { get; set; }

        public StatusTypeEnum Status { get; set; }

        public long TotalTime { get; set; }

        public string Params { get => _Params; set { _Params = value; OnPropertyChanged(); } }
        private string _Params;
    }


    public class FlowControl : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowControl));
        private FlowEngineControl flowEngine;
        public event EventHandler<FlowControlData> FlowCompleted;

        public string SerialNumber { get; set; }

        public FlowControl(MQTTControl mQTTControl)
        {
        }

        public FlowControl(MQTTControl mQTTControl, FlowEngineControl flowEngine) : this(mQTTControl)
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
                    OnPropertyChanged();
                }
            }
        }
        public void Stop()
        {
            FlowCompleted = null;
            flowEngine.Finished -= FinishedAsync;

            flowEngine.StopNode(SerialNumber);
            IsFlowRun = false;
        }

        public void Start(string sn)
        {
            IsFlowRun = true;
            SerialNumber = sn;

            var tol = MqttRCService.GetInstance().ServiceTokens;
            flowEngine.Finished -= FinishedAsync;
            flowEngine.Finished += FinishedAsync;
            flowEngine.StartNode(sn, tol);
        }

        public async void FinishedAsync(object sender, FlowEngineEventArgs e)
        {
            try
            {
                IsFlowRun = false;
                FlowControlData data = new FlowControlData() { StartNodeName =e.StartNodeName ,SerialNumber =e.SerialNumber, EventName =e.Status.ToString(), Params =e.Message };                
                log.Info("清理流程中，等待100ms");
                flowEngine.FlowClear();
                await Task.Delay(100);
                log.Info("流程清理完成");
                Application.Current.Dispatcher.Invoke(() => FlowCompleted?.Invoke(this, data));
            }
            catch (Exception ex)
            {
                log.Error("流程完成事件异常", ex);
            }
        }
    }
}

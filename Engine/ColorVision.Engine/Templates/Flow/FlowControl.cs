using ColorVision.Common.MVVM;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using System;
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


        public FlowStatus FlowStatus
        {
            get => Status switch
            {
                StatusTypeEnum.Runing => FlowStatus.Runing,
                StatusTypeEnum.Failed => FlowStatus.Failed,
                StatusTypeEnum.Completed => FlowStatus.Completed,
                StatusTypeEnum.Canceled => FlowStatus.Canceled,
                StatusTypeEnum.OverTime => FlowStatus.OverTime,
                StatusTypeEnum.Paused => FlowStatus.Paused,

                _ => FlowStatus.Ready,
            };
        }
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

        private bool _IsFlowRun;

        public bool IsFlowRun
        {
            get
            {
                return _IsFlowRun;

            }
            set
            {
                _IsFlowRun = value;
                Application.Current.Dispatcher.Invoke(() => OnPropertyChanged());
            }
        }
        public void Stop()
        {
            FlowCompleted = null;
            flowEngine.Finished -= FinishedAsync;
            if (!string.IsNullOrWhiteSpace(SerialNumber))
            {
                flowEngine.StopNode(SerialNumber);
            }
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

        public void FinishedAsync(object sender, FlowEngineEventArgs e)
        {
            IsFlowRun = false;
            FlowControlData data = new FlowControlData() { StartNodeName = e.StartNodeName, SerialNumber = e.SerialNumber, EventName = e.Status.ToString() , Status= e.Status, Params = e.Message };
            try
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    FlowCompleted?.Invoke(this, data);
                });
            }
            catch (Exception ex)
            {
                log.Error("流程完成事件异常", ex);
            }
        }
    }
}

using ColorVision.Common.MVVM;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing
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
        public string ErrorNodeName { get; set; }
        public string ErrorNodeId { get; set; }
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
        private static readonly TimeSpan StartReadyTimeout = TimeSpan.FromSeconds(5);
        private FlowEngineControl flowEngine;
        private readonly object lifecycleLock = new object();
        public event EventHandler<FlowControlData> FlowCompleted;

        public string? SerialNumber { get; set; }

        public FlowControl(MQTTControl mQTTControl)
        {
        }

        public FlowControl(MQTTControl mQTTControl, FlowEngineControl flowEngine) : this(mQTTControl)
        {
            this.flowEngine = flowEngine;
        }

        private int _isFlowRun;

        public bool IsFlowRun
        {
            get => Volatile.Read(ref _isFlowRun) != 0;
            set
            {
                int nextValue = value ? 1 : 0;
                if (Interlocked.Exchange(ref _isFlowRun, nextValue) == nextValue)
                    return;

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null)
                    OnPropertyChanged();
                else
                    dispatcher.BeginInvoke(() => OnPropertyChanged());
            }
        }
        public void Stop()
        {
            string? serialNumber;
            lock (lifecycleLock)
            {
                flowEngine.Finished -= FinishedAsync;
                serialNumber = SerialNumber;
                SerialNumber = null;
                IsFlowRun = false;
            }
            if (!string.IsNullOrWhiteSpace(serialNumber))
                flowEngine.StopNode(serialNumber);
        }

        public async Task<bool> TryStartAsync(string sn, CancellationToken cancellationToken = default)
        {
            string startNodeName = flowEngine.GetStartNodeName();
            return await TryStartAsync(startNodeName, sn, cancellationToken);
        }

        public async Task<bool> TryStartAsync(string startNodeName, string sn, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(startNodeName))
                return false;

            bool readyBefore = flowEngine.IsStartNodeReady(startNodeName);
            bool readinessSucceeded = false;
            bool started = false;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                readinessSucceeded = await flowEngine.EnsureStartNodeReadyAsync(startNodeName, StartReadyTimeout, cancellationToken);
                if (readinessSucceeded)
                    started = TryStart(startNodeName, sn);
                return started;
            }
            finally
            {
                stopwatch.Stop();
                log.InfoFormat(
                    "流程启动准备[{0}] => StartNode={1}, ReadyBefore={2}, ReadinessSucceeded={3}, Started={4}, Elapsed={5}ms",
                    sn,
                    startNodeName,
                    readyBefore,
                    readinessSucceeded,
                    started,
                    stopwatch.ElapsedMilliseconds);
            }
        }

        private bool TryStart(string startNodeName, string sn)
        {
            lock (lifecycleLock)
            {
                if (IsFlowRun)
                    return false;
                if (!flowEngine.CanStartNode(startNodeName))
                    return false;

                IsFlowRun = true;
                SerialNumber = sn;

                var tol = MqttRCService.GetInstance().ServiceTokens;
                flowEngine.Finished -= FinishedAsync;
                flowEngine.Finished += FinishedAsync;
                try
                {
                    if (!flowEngine.TryStartNode(startNodeName, sn, tol))
                    {
                        flowEngine.Finished -= FinishedAsync;
                        SerialNumber = null;
                        IsFlowRun = false;
                        return false;
                    }
                    return true;
                }
                catch
                {
                    flowEngine.Finished -= FinishedAsync;
                    SerialNumber = null;
                    IsFlowRun = false;
                    throw;
                }
            }
        }

        public void FinishedAsync(object sender, FlowEngineEventArgs e)
        {
            FlowControlData data;
            lock (lifecycleLock)
            {
                if (!string.Equals(e.SerialNumber, SerialNumber, StringComparison.Ordinal))
                    return;

                flowEngine.Finished -= FinishedAsync;
                SerialNumber = null;
                IsFlowRun = false;
                data = new FlowControlData() { StartNodeName = e.StartNodeName, ErrorNodeName = e.ErrorNodeName, ErrorNodeId = e.ErrorNodeId, SerialNumber = e.SerialNumber, EventName = e.Status.ToString() , Status= e.Status, TotalTime = e.TotalTime, Message = e.Message, Params = e.Message };
            }
            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.CheckAccess())
                    FlowCompleted?.Invoke(this, data);
                else
                    dispatcher.BeginInvoke(() => FlowCompleted?.Invoke(this, data));
            }
            catch (Exception ex)
            {
                log.Error("流程完成事件异常", ex);
            }
        }
    }
}

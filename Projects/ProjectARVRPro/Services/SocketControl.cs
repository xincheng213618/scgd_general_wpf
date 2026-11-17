#pragma warning disable CS4014,CS8602,CS8603,CS8625
using ColorVision.Engine.Templates.Flow;
using ColorVision.SocketProtocol;
using log4net;
using Newtonsoft.Json;
using ProjectARVRPro.PluginConfig;
using System.Diagnostics;
using System.Net.Sockets;
using System.Windows;

namespace ProjectARVRPro.Services
{
    public class SocketControl
    {
        public static SocketControl Current { get; set; } = new SocketControl();
        public NetworkStream Stream { get; set; }
    }
    
    public class FlowInit : ISocketJsonHandler
    {
        public string EventName => "ProjectARVRInit";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            (string resolvedSerialNumber, int firstEnabledIndex) = Application.Current.Dispatcher.Invoke(() =>
            {
                bool needsShow = ProjectWindowInstance.WindowInstance == null;
                ARVRWindow window = ProjectARVRWindowHost.GetOrCreate();
                if (window.IsCompletingFlowBatch)
                    return (request.SerialNumber?.Trim() ?? string.Empty, -2);
                SocketControl.Current.Stream = stream;
                if (needsShow)
                    window.Show();

                if (window.IsResultExportPending || window.IsJsonVidRunning)
                    return (request.SerialNumber?.Trim() ?? string.Empty, -2);
                var processMetas = Process.ProcessManager.GetInstance().ProcessMetas;
                int firstEnabledInternalIndex = -1;
                for (int i = 0; i < processMetas.Count; i++)
                {
                    if (processMetas[i].IsEnabled)
                    {
                        firstEnabledInternalIndex = i;
                        break;
                    }
                }
                if (firstEnabledInternalIndex < 0)
                    return (request.SerialNumber?.Trim() ?? string.Empty, firstEnabledInternalIndex);

                string resolvedSerialNumber = window.InitTest(request.SerialNumber);
                int externalIndex = firstEnabledInternalIndex + GetProcessEnableSocket.GetIndexOffset();
                return (resolvedSerialNumber, externalIndex);
            });
            if (firstEnabledIndex == -2)
            {
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = -4,
                    Msg = "ARVR test is busy",
                    SerialNumber = resolvedSerialNumber,
                };
            }
            if (firstEnabledIndex < 0)
            {
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = -2,
                    Msg = "No enabled ARVR flow",
                    SerialNumber = resolvedSerialNumber,
                };
            }
            //现在先切换PG

            return new SocketResponse() { MsgID = request.MsgID, EventName = "SwitchPG", SerialNumber = resolvedSerialNumber, Data = new SwitchPG() { ARVRTestType = firstEnabledIndex } };
        }
    }

    public class SwitchPGSocket: ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SwitchPGSocket));
        public string EventName => "SwitchPGCompleted";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            bool measure = log.IsInfoEnabled;
            long queuedAt = measure ? Stopwatch.GetTimestamp() : 0;
            long startedAt = 0, finishedAt = 0;
            int? batchIdBeforeDispatch = null;
            string outcome = "Forwarded";
            SocketResponse response = Application.Current.Dispatcher.Invoke<SocketResponse>(() =>
            {
                if (measure) startedAt = Stopwatch.GetTimestamp();
                try
                {
                    ARVRWindow window = ProjectWindowInstance.WindowInstance;
                    if (measure) batchIdBeforeDispatch = window?.CurrentFlowResultBatchId;
                    if (window?.IsCompletingFlowBatch == true)
                    {
                        outcome = "BatchCompleting";
                        return null;
                    }
                    SocketControl.Current.Stream = stream;
                    if (window == null)
                    {
                        outcome = "WindowUnavailable";
                        return new SocketResponse { MsgID = request.MsgID, SerialNumber = request.SerialNumber, Code = -3, Msg = "ProjectARVR Wont Open", EventName = EventName };
                    }

                    log.Debug("PG切换结束");
                    window.SwitchPGCompleted();
                    return null;
                }
                finally { if (measure) finishedAt = Stopwatch.GetTimestamp(); }
            });
            if (measure)
            {
                log.Info(JsonConvert.SerializeObject(new
                {
                    Event = "ARVRSwitchDispatchTiming", request.MsgID, request.SerialNumber,
                    BatchIdBeforeDispatch = batchIdBeforeDispatch, Outcome = outcome,
                    DispatcherQueueMs = Math.Round(Stopwatch.GetElapsedTime(queuedAt, startedAt).TotalMilliseconds, 3),
                    DispatcherCallbackMs = Math.Round(Stopwatch.GetElapsedTime(startedAt, finishedAt).TotalMilliseconds, 3)
                }));
            }
            return response;
        }
    }




    public class FlowSocketMsgHandle : ISocketJsonHandler
    {
        public string EventName => "  ";
        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                ARVRWindow window = ProjectWindowInstance.WindowInstance;
                if (window?.IsCompletingFlowBatch == true)
                    return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -4, Msg = "ARVR test is busy", SerialNumber = request.SerialNumber };
                if (window == null)
                {
                    SocketControl.Current.Stream = stream;
                    return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -3, Msg = "ProjectARVR Wont Open" };
                }
                if (TemplateFlow.Params.FirstOrDefault(a => a.Key == request.Params)?.Value is not FlowParam)
                {
                    SocketControl.Current.Stream = stream;
                    return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -2, Msg = $"Cant Find Flow {request.Params}" };
                }

                _ = Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (!ReferenceEquals(window, ProjectWindowInstance.WindowInstance) || window.IsCompletingFlowBatch)
                        return;
                    SocketControl.Current.Stream = stream;
                    _ = window.RunTemplate();
                });
                return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = 0, Msg = $"Run {request.Params}" };
            });
        }
    }
}

#pragma warning disable CS4014,CS8602,CS8603,CS8625
using ColorVision.Engine.Templates.Flow;
using ColorVision.SocketProtocol;
using log4net;
using Newtonsoft.Json;
using ProjectARVRPro.PluginConfig;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace ProjectARVRPro.Services
{
    public class SocketControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SocketControl));
        public static SocketControl Current { get; set; } = new SocketControl();
        public NetworkStream Stream { get; set; }

        internal static void WriteSwitchResponse(NetworkStream stream, SocketResponse response, Action afterSend, bool recordEndpoint)
        {
            string? endpoint = null;
            if (recordEndpoint)
            {
                try { endpoint = stream.Socket.RemoteEndPoint?.ToString(); }
                catch (ObjectDisposedException) { }
                catch (SocketException) { }
            }

            string content = JsonConvert.SerializeObject(response);
            stream.Write(Encoding.UTF8.GetBytes(content));
            afterSend();

            try
            {
                log.Info(content);
                SocketMessageManager.GetInstance().AddMessage(new SocketMessage
                {
                    ClientEndPoint = endpoint,
                    Direction = SocketMessageDirection.Sent,
                    Content = content,
                    MessageTime = DateTime.Now,
                    EventName = response.EventName,
                    MsgID = response.MsgID,
                    ResponseCode = response.Code
                });
            }
            catch (Exception ex)
            {
                log.Error("普通切图已发送，但记录 Socket 历史失败", ex);
            }
        }
    }
    
    public class FlowInit : ISocketJsonHandler
    {
        // The bundle also runs on hosts released before the send callback was added.
        private static readonly Action<SocketResponse, Action>? SetAfterSend = typeof(SocketResponse)
            .GetProperty(nameof(SocketResponse.AfterSend))?.SetMethod?.CreateDelegate<Action<SocketResponse, Action>>();

        public string EventName => "ProjectARVRInit";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            (string resolvedSerialNumber, int firstEnabledIndex, Action? afterSend) =
                Application.Current.Dispatcher.Invoke<(string, int, Action?)>(() =>
            {
                bool needsShow = ProjectWindowInstance.WindowInstance == null;
                ARVRWindow window = ProjectARVRWindowHost.GetOrCreate();
                if (needsShow)
                    window.Show();

                return window.PrepareSocketTestSession(stream, request.SerialNumber);
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
            var response = new SocketResponse
            {
                MsgID = request.MsgID,
                EventName = "SwitchPG",
                SerialNumber = resolvedSerialNumber,
                Data = new SwitchPG { ARVRTestType = firstEnabledIndex }
            };
            if (SetAfterSend != null)
            {
                SetAfterSend(response, afterSend!);
                return response;
            }

            SocketControl.WriteSwitchResponse(stream, response, afterSend!, recordEndpoint: true);
            return null;
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
            string outcome = "Ignored";
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
                    if (window == null)
                    {
                        outcome = "WindowUnavailable";
                        return new SocketResponse { MsgID = request.MsgID, SerialNumber = request.SerialNumber, Code = -3, Msg = "ProjectARVR Wont Open", EventName = EventName };
                    }

                    if (window.TryAcknowledgeSocketSwitch(stream))
                        outcome = "Acknowledged";
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
                    return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -3, Msg = "ProjectARVR Wont Open" };
                }
                if (TemplateFlow.Params.FirstOrDefault(a => a.Key == request.Params)?.Value is not FlowParam)
                {
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

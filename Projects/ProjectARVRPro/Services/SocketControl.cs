#pragma warning disable CS4014,CS8602,CS8603,CS8625
using ColorVision.Engine.Templates.Flow;
using ColorVision.SocketProtocol;
using log4net;
using ProjectARVRPro.PluginConfig;
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
            SocketControl.Current.Stream = stream;
            if (ProjectWindowInstance.WindowInstance == null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectWindowInstance.WindowInstance = new ARVRWindow
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    ProjectWindowInstance.WindowInstance.Closed += (s, e) => ProjectWindowInstance.WindowInstance = null;
                    ProjectWindowInstance.WindowInstance.Show();
                });

            }

            (string resolvedSerialNumber, int firstEnabledIndex) = Application.Current.Dispatcher.Invoke(() =>
            {
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

                string resolvedSerialNumber = ProjectWindowInstance.WindowInstance.InitTest(request.SerialNumber);
                int externalIndex = firstEnabledInternalIndex + GetProcessEnableSocket.GetIndexOffset();
                return (resolvedSerialNumber, externalIndex);
            });
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
            SocketControl.Current.Stream = stream;
            if (ProjectWindowInstance.WindowInstance != null)
            {
                log.Info("PG切换结束");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectWindowInstance.WindowInstance.SwitchPGCompleted();
                });
                return null;
            }
            else
            {
                return new SocketResponse { MsgID = request.MsgID, SerialNumber =request.SerialNumber,Code = -3, Msg = $"ProjectARVR Wont Open", EventName = EventName };
            }
        }
    }




    public class FlowSocketMsgHandle : ISocketJsonHandler
    {
        public string EventName => "  ";
        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            SocketControl.Current.Stream = stream;
            if (ProjectWindowInstance.WindowInstance != null)
            {
                if (TemplateFlow.Params.FirstOrDefault(a => a.Key == request.Params)?.Value is FlowParam flowParam)
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        ProjectWindowInstance.WindowInstance.RunTemplate();
                    });
                    return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = 0, Msg = $"Run {request.Params}"};
                }
                else
                {
                    return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -2, Msg = $"Cant Find Flow {request.Params}" };
                }
            }
            else
            {
                return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -3, Msg = $"ProjectARVR Wont Open" };
            }
        }
    }
}

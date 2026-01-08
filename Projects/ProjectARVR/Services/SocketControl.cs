using ColorVision.Engine.Templates.Flow;
using ColorVision.SocketProtocol;
using ProjectARVR.PluginConfig;
using System.Net.Sockets;
using System.Windows;

namespace ProjectARVR.Services
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
            if (ProjectWindowInstance.WindowInstance != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectWindowInstance.WindowInstance.InitTest(request.SerialNumber);
                });
                //现在先切换PG
                return new SocketResponse() { MsgID = request.MsgID, EventName = "SwitchPG", Data = new SwitchPG() { ARVRTestType = (ARVRTestType)1 } };
            }
            else
            {
                return new SocketResponse { Code = -3, MsgID = request.MsgID, Msg = $"ProjectARVR Wont Open", EventName = EventName };
            }
        }
    }

    public class SwitchPGSocket: ISocketJsonHandler
    {
        public string EventName => "SwitchPGCompleted";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            SocketControl.Current.Stream = stream;
            if (ProjectWindowInstance.WindowInstance != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectWindowInstance.WindowInstance.SwitchPGCompleted();
                });
                return null;
            }
            else
            {
                return new SocketResponse { MsgID = request.MsgID, SerialNumber = request.SerialNumber, Code = -3, Msg = $"ProjectARVR Wont Open", EventName = EventName };
            }
        }
    }




    public class FlowSocketMsgHandle : ISocketJsonHandler
    {
        public string EventName => "ProjectARVR";
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

                    return new SocketResponse { Code = 0, Msg = $"Run {request.Params}", EventName = EventName};
                }
                else
                {
                    return new SocketResponse { Code = -2, Msg = $"Cant Find Flow {request.Params}", EventName = EventName };
                }
            }
            else
            {
                return new SocketResponse { Code = -3, Msg = $"ProjectARVR Wont Open", EventName = EventName };
            }
        }
    }
}

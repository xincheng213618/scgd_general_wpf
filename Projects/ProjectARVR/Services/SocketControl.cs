using ColorVision.Engine.Templates.Flow;
using ColorVision.UI.SocketProtocol;
using ProjectARVR.PluginConfig;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace ProjectARVR.Services
{
    public class SocketControl
    {
        public static SocketControl Current { get; set; } = new SocketControl();
        public NetworkStream Stream { get; set; }
    }

    public class FlowSocketMsgHandle : ISocketEventHandler
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
                    return new SocketResponse { Code = 0, Msg = $"Run {request.Params}", EventName = EventName };
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

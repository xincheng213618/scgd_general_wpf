using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using ColorVision.UI.SocketProtocol;
using log4net;
using ProjectARVR.PluginConfig;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;

namespace ProjectARVR.Services
{
    public class FlowSocketMsgHandle : ISocketMsgHandle
    {
        public int Order => 0;

        public bool Handle(NetworkStream stream, string message)
        {
            var strings = message.Split(",");
            if (strings.Length > 1 && strings[0] == "ProjectARVR")
            {
                if (ProjectWindowInstance.WindowInstance != null)
                {
                    if (TemplateFlow.Params.FirstOrDefault(a => a.Key == strings[1])?.Value is FlowParam flowParam)
                    {

                        byte[] response1 = Encoding.ASCII.GetBytes($"Run {strings[1]}");
                        stream.Write(response1, 0, response1.Length);
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            ProjectWindowInstance.WindowInstance.RunTemplate();
                            byte[] response1 = Encoding.ASCII.GetBytes($"Run {strings[1]}");
                            stream.Write(response1, 0, response1.Length);
                        });
                        return true;
                    }
                    else
                    {
                        byte[] response = Encoding.ASCII.GetBytes($"Cant Find Flow {strings[1]}");
                        stream.Write(response, 0, response.Length);
                    }
                    return true;
                }
                else
                {
                    byte[] response = Encoding.ASCII.GetBytes($"ProjectARVR Wont Open");
                    stream.Write(response, 0, response.Length);
                    return true;
                }
            }
            return false;
        }
    }
}

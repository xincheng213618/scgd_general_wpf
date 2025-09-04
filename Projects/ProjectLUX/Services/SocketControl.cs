using ColorVision.Engine.Templates.Flow;
using ColorVision.SocketProtocol;
using Dm.util;
using ProjectLUX.PluginConfig;
using SqlSugar;
using System.Net.Sockets;
using System.Windows;
using static iText.Svg.SvgConstants;

namespace ProjectLUX.Services
{
    public class SocketControl
    {
        public static SocketControl Current { get; set; } = new SocketControl();
        public NetworkStream Stream { get; set; }
    }

    public class TestSocket:ISocketTextDispatcher
    {
        public string Handle(NetworkStream stream, string request)
        {
            var list = request.split(",");

            if (list.Length == 2)
            {
                string code = list[0];
                string sn = list[1];
                sn = sn.TrimEnd(';');
                if (code.startsWith("T00"))
                {

                    // 取 code 的最后两位（如果 code 长度足够）
                    string lastTwo = code.Length >= 2 ? code.Substring(code.Length - 2, 2) : code;

                    // 拼接到 H030 上
                    string h030x = "H03" + lastTwo;

                    List<string> strings = new List<string>();
                    strings.Add(h030x);
                    strings.Add(sn);
                    strings.Add("00");
                    return string.Join(",", strings) + ";";
                }
                else
                {
                    return $"No right Code {code}";
                }
            }
            else
            {
                return "No SN";
            }
        }
    }


    public class FlowInit : ISocketJsonHandler
    {
        public string EventName => "ProjectLUXInit";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            SocketControl.Current.Stream = stream;
            if (ProjectWindowInstance.WindowInstance != null)
            {
                ProjectWindowInstance.WindowInstance.InitTest(request.SerialNumber);
                //现在先切换PG
                return new SocketResponse() { MsgID = request.MsgID, EventName = "SwitchPG", Data = new SwitchPG() { ARVRTestType = ARVRTestType.White } };
            }
            else
            {
                return new SocketResponse { Code = -3, MsgID = request.MsgID, Msg = $"ProjectLUX Wont Open", EventName = EventName };
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
                return new SocketResponse { MsgID = request.MsgID, SerialNumber = request.SerialNumber, Code = -3, Msg = $"ProjectLUX Wont Open", EventName = EventName };
            }
        }
    }




    public class FlowSocketMsgHandle : ISocketJsonHandler
    {
        public string EventName => "ProjectLUX";
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
                return new SocketResponse { Code = -3, Msg = $"ProjectLUX Wont Open", EventName = EventName };
            }
        }
    }
}

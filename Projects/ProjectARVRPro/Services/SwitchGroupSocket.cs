using ColorVision.SocketProtocol;
using log4net;
using ProjectARVRPro.PluginConfig;
using System.Net.Sockets;
using System.Windows;

namespace ProjectARVRPro.Services
{
    public class SwitchGroupSocket : ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SwitchGroupSocket));
        public string EventName => "SwitchGroup";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            SocketControl.Current.Stream = stream;
            try
            {
                string groupName = request.Params;
                if (string.IsNullOrWhiteSpace(groupName))
                {
                    return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -1, Msg = "GroupName is empty" };
                }

                var processManager = Process.ProcessManager.GetInstance();
                int idx = -1;
                for (int i = 0; i < processManager.ProcessGroups.Count; i++)
                {
                    if (string.Equals(processManager.ProcessGroups[i].Name, groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx < 0)
                {
                    return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -2, Msg = $"Group not found: {groupName}" };
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    processManager.ActiveGroupIndex = idx;
                });

                log.Info($"Socket切换组: {groupName}");
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = 0,
                    Msg = $"Switched to {groupName}",
                    Data = new { GroupName = groupName, MetaCount = processManager.ProcessMetas.Count }
                };
            }
            catch (Exception ex)
            {
                log.Error("SwitchGroup异常", ex);
                return new SocketResponse { MsgID = request.MsgID, EventName = EventName, Code = -99, Msg = ex.Message };
            }
        }
    }
}

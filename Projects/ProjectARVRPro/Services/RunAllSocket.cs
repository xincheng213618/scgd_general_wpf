#pragma warning disable CS8602,CS8625
using ColorVision.SocketProtocol;
using log4net;
using ProjectARVRPro.PluginConfig;
using System.Net.Sockets;
using System.Windows;

namespace ProjectARVRPro.Services
{
    public class RunAllSocket : ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(RunAllSocket));
        public string EventName => "RunAll";

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

            (bool accepted, string resolvedSerialNumber) = Application.Current.Dispatcher.Invoke(() =>
            {
                bool accepted = ProjectWindowInstance.WindowInstance.TryPrepareRunAllSession(request.SerialNumber, out string resolvedSerialNumber);
                if (accepted)
                    _ = ProjectWindowInstance.WindowInstance.RunAllAsync();
                return (accepted, resolvedSerialNumber);
            });
            if (!accepted)
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

            log.Info($"RunAll triggered via Socket, SN={resolvedSerialNumber}");
            return new SocketResponse
            {
                MsgID = request.MsgID,
                EventName = EventName,
                Code = 0,
                Msg = "RunAll started",
                SerialNumber = resolvedSerialNumber
            };
        }
    }
}

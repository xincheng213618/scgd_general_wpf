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
            (bool accepted, string resolvedSerialNumber) = Application.Current.Dispatcher.Invoke(() =>
            {
                bool needsShow = ProjectWindowInstance.WindowInstance == null;
                ARVRWindow window = ProjectARVRWindowHost.GetOrCreate();
                if (needsShow)
                    window.Show();

                bool accepted = window.TryPrepareRunAllSession(request.SerialNumber, out string resolvedSerialNumber);
                if (accepted)
                {
                    SocketControl.Current.Stream = stream;
                    _ = window.RunAllAsync();
                }
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

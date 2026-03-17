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
            if (ProjectWindowInstance.WindowInstance != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectWindowInstance.WindowInstance.InitTest(request.SerialNumber);
                });

                Application.Current.Dispatcher.BeginInvoke(async () =>
                {
                    await ProjectWindowInstance.WindowInstance.RunAllAsync();
                });

                log.Info($"RunAll triggered via Socket, SN={request.SerialNumber}");
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = 0,
                    Msg = "RunAll started",
                    SerialNumber = request.SerialNumber
                };
            }
            else
            {
                // Auto-open window
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectWindowInstance.WindowInstance = new ARVRWindow
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    ProjectWindowInstance.WindowInstance.Closed += (s, e) => ProjectWindowInstance.WindowInstance = null;
                    ProjectWindowInstance.WindowInstance.Show();
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectWindowInstance.WindowInstance.InitTest(request.SerialNumber);
                });

                Application.Current.Dispatcher.BeginInvoke(async () =>
                {
                    await ProjectWindowInstance.WindowInstance.RunAllAsync();
                });

                log.Info($"RunAll triggered via Socket (window opened), SN={request.SerialNumber}");
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = 0,
                    Msg = "RunAll started (window opened)",
                    SerialNumber = request.SerialNumber
                };
            }
        }
    }
}

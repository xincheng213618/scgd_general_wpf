using ColorVision.SocketProtocol;
using log4net;
using System.Net.Sockets;
using System.Windows;

namespace Spectrum.Socket
{
    /// <summary>
    /// Socket指令处理器：光谱仪连接/断开
    /// 
    /// 连接请求:
    /// {"EventName":"SpectrumConnect","MsgID":"1","Version":"1.0","Params":"connect"}
    /// 
    /// 断开请求:
    /// {"EventName":"SpectrumConnect","MsgID":"2","Version":"1.0","Params":"disconnect"}
    /// </summary>
    public class SpectrumConnectSocketHandler : ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumConnectSocketHandler));

        public string EventName => "SpectrumConnect";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            var mainWindow = MainWindow.Instance;
            if (mainWindow == null)
            {
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = -1,
                    Msg = "光谱仪窗口未打开"
                };
            }

            string action = request.Params?.Trim().ToLowerInvariant() ?? "connect";

            try
            {
                if (action == "disconnect")
                {
                    log.Info("Socket指令: 断开光谱仪");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SpectrometerManager.Instance.Disconnect();
                    });

                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = 200,
                        Msg = "光谱仪已断开"
                    };
                }
                else
                {
                    if (SpectrometerManager.Instance.IsConnected)
                    {
                        return new SocketResponse
                        {
                            MsgID = request.MsgID,
                            EventName = EventName,
                            Code = 200,
                            Msg = "光谱仪已经连接"
                        };
                    }

                    log.Info("Socket指令: 连接光谱仪");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SpectrometerManager.Instance.Connect();
                    });

                    bool isConnected = SpectrometerManager.Instance.IsConnected;
                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = isConnected ? 200 : -2,
                        Msg = isConnected ? "光谱仪连接成功" : "光谱仪连接失败",
                        Data = new { IsConnected = isConnected }
                    };
                }
            }
            catch (Exception ex)
            {
                log.Error($"Socket光谱仪{action}异常", ex);
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = -99,
                    Msg = $"操作异常: {ex.Message}"
                };
            }
        }
    }
}

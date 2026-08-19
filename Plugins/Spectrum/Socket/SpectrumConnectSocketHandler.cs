using ColorVision.SocketProtocol;
using log4net;
using System.Net.Sockets;

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
            string action = request.Params?.Trim().ToLowerInvariant() ?? "connect";
            SpectrometerManager manager = SpectrometerManager.Instance;

            try
            {
                if (action == "disconnect")
                {
                    log.Info("Socket指令: 断开光谱仪");
                    int result = manager.Disconnect();

                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = result == 1 ? 200 : -2,
                        Msg = result == 1 ? "光谱仪已断开" : $"光谱仪断开失败: {manager.GetOperationErrorMessage(result)}",
                        Data = new { manager.IsConnected, manager.IsCalibrationReady, manager.CalibrationStatus }
                    };
                }
                else
                {
                    log.Info("Socket指令: 连接光谱仪");
                    int result = manager.Connect();
                    bool isConnected = result == 1 && manager.IsConnected;
                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = isConnected ? 200 : result == SpectrometerManager.OperationBusy ? -4 : -2,
                        Msg = isConnected
                            ? manager.IsCalibrationReady
                                ? "光谱仪连接成功，标定已就绪"
                                : $"光谱仪已连接，但暂不可测量：{manager.CalibrationStatus}"
                            : result == SpectrometerManager.OperationBusy
                                ? "光谱仪驱动正被其他会话使用，或上次释放失败"
                                : $"光谱仪连接失败: {manager.GetOperationErrorMessage(result)}",
                        Data = new { IsConnected = isConnected, manager.IsCalibrationReady, manager.CalibrationStatus }
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

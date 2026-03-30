using ColorVision.SocketProtocol;
using log4net;
using System.Net.Sockets;

namespace Spectrum.Socket
{
    /// <summary>
    /// Socket指令处理器：获取光谱仪状态
    /// 返回当前光谱仪的连接状态、配置参数等信息
    /// 
    /// 请求示例:
    /// {"EventName":"SpectrumStatus","MsgID":"1","Version":"1.0","Params":""}
    /// 
    /// 响应示例:
    /// {"EventName":"SpectrumStatus","MsgID":"1","Code":200,"Msg":"OK",
    ///  "Data":{"IsConnected":true,"IntTime":100,"Average":1,"SerialNumber":"SP100-001",...}}
    /// </summary>
    public class SpectrumStatusSocketHandler : ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumStatusSocketHandler));

        public string EventName => "SpectrumStatus";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            try
            {
                var manager = SpectrometerManager.Instance;

                var data = new
                {
                    manager.IsConnected,
                    manager.IntTime,
                    manager.Average,
                    manager.SerialNumber,
                    EnableAutodark = manager.EnableAutodark,
                    EnableAutoIntegration = manager.EnableAutoIntegration,
                    EnableAdaptiveAutoDark = manager.EnableAdaptiveAutoDark,
                    MeasurementInterval = manager.MeasurementInterval,
                    MeasurementNum = manager.MeasurementNum,
                    WindowOpen = MainWindow.Instance != null
                };

                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = 200,
                    Msg = "OK",
                    Data = data
                };
            }
            catch (Exception ex)
            {
                log.Error("Socket获取光谱仪状态异常", ex);
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = -99,
                    Msg = $"获取状态异常: {ex.Message}"
                };
            }
        }
    }
}

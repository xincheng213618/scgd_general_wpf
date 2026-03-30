using ColorVision.SocketProtocol;
using log4net;
using System.Net.Sockets;

namespace Spectrum.Socket
{
    /// <summary>
    /// Socket指令处理器：自动积分时间
    /// 执行自动积分时间获取，并返回结果
    /// 
    /// 请求示例:
    /// {"EventName":"SpectrumAutoIntTime","MsgID":"1","Version":"1.0","Params":""}
    /// 
    /// 响应示例:
    /// {"EventName":"SpectrumAutoIntTime","MsgID":"1","Code":200,"Msg":"自动积分时间获取成功",
    ///  "Data":{"IntTime":150.5}}
    /// </summary>
    public class SpectrumAutoIntTimeSocketHandler : ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumAutoIntTimeSocketHandler));

        public string EventName => "SpectrumAutoIntTime";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            if (MainWindow.Instance == null)
            {
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = -1,
                    Msg = "光谱仪窗口未打开"
                };
            }

            var manager = SpectrometerManager.Instance;
            if (!manager.IsConnected)
            {
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = -2,
                    Msg = "光谱仪未连接"
                };
            }

            try
            {
                log.Info("Socket指令: 获取自动积分时间");

                float? result = manager.GetAutoIntegrationTime();

                if (result.HasValue)
                {
                    manager.IntTime = result.Value;

                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = 200,
                        Msg = "自动积分时间获取成功",
                        Data = new { IntTime = result.Value }
                    };
                }
                else
                {
                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = -3,
                        Msg = "自动积分时间获取失败"
                    };
                }
            }
            catch (Exception ex)
            {
                log.Error("Socket自动积分时间异常", ex);
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

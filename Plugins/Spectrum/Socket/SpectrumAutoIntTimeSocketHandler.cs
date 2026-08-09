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

                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
                (bool entered, float? result) = manager.TryGetAutoIntegrationTimeAsync(timeout.Token).GetAwaiter().GetResult();

                if (!entered)
                {
                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = -4,
                        Msg = "光谱仪正在执行其他操作"
                    };
                }

                if (result.HasValue)
                {
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
            catch (OperationCanceledException)
            {
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = -4,
                    Msg = "自动积分时间操作超时或已取消"
                };
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

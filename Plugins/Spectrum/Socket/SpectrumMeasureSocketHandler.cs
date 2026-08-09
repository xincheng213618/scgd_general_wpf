using ColorVision.SocketProtocol;
using log4net;
using System.Net.Sockets;

namespace Spectrum.Socket
{
    public class SpectrumMeasureSocketHandler : ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumMeasureSocketHandler));

        public string EventName => "SpectrumMeasure";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            SpectrometerManager manager = SpectrometerManager.Instance;
            if (!manager.IsConnected)
                return Response(request, -2, "光谱仪未连接");

            try
            {
                log.Info("Socket指令: 执行光谱测量");
                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));
                SpectrumMeasurementResult measurement = manager.MeasureAsync(timeout.Token).GetAwaiter().GetResult();
                if (measurement.IsBusy)
                    return Response(request, -4, "光谱仪正在执行其他操作");
                if (!measurement.IsSuccess || measurement.Result == null)
                    return Response(request, -3, measurement.ErrorMessage ?? "测量失败");

                var result = measurement.Result;
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = 200,
                    Msg = "测量完成",
                    Data = new
                    {
                        result.Lv,
                        x = result.fx,
                        y = result.fy,
                        u = result.fu,
                        v = result.fv,
                        CCT = result.fCCT,
                        Duv = result.dC,
                        DominantWavelength = result.fLd,
                        PeakWavelength = result.fLp,
                        HalfBandwidth = result.fHW,
                        ColorPurity = result.fPur,
                        Ra = result.fRa,
                        result.IP,
                        result.Blue,
                        manager.IntTime
                    }
                };
            }
            catch (OperationCanceledException)
            {
                return Response(request, -4, "测量超时或已取消");
            }
            catch (Exception ex)
            {
                log.Error("Socket光谱测量异常", ex);
                return Response(request, -99, $"测量异常: {ex.GetBaseException().Message}");
            }
        }

        private SocketResponse Response(SocketRequest request, int code, string message) => new()
        {
            MsgID = request.MsgID,
            EventName = EventName,
            Code = code,
            Msg = message
        };
    }
}

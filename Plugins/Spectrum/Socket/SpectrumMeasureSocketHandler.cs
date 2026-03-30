using ColorVision.SocketProtocol;
using log4net;
using System.Net.Sockets;
using System.Windows;

namespace Spectrum.Socket
{
    /// <summary>
    /// Socket指令处理器：光谱测量
    /// 触发单次光谱测量并返回测量结果（色度参数）
    /// 
    /// 请求示例:
    /// {"EventName":"SpectrumMeasure","MsgID":"1","Version":"1.0","Params":""}
    /// 
    /// 响应示例:
    /// {"EventName":"SpectrumMeasure","MsgID":"1","Code":200,"Msg":"测量完成",
    ///  "Data":{"Lv":123.45,"x":0.312,"y":0.329,"u":0.198,"v":0.468,"CCT":6504,"Duv":0.003,...}}
    /// </summary>
    public class SpectrumMeasureSocketHandler : ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumMeasureSocketHandler));

        public string EventName => "SpectrumMeasure";

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

            if (!SpectrometerManager.Instance.IsConnected)
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
                log.Info("Socket指令: 执行光谱测量");

                // Record count before measurement to detect new results
                int countBefore = MainWindow.ViewResultSpectrums.Count;

                // Measure() runs on background thread and dispatches UI updates internally
                var measureTask = Task.Run(async () => await mainWindow.Measure());
                if (!measureTask.Wait(TimeSpan.FromSeconds(60)))
                {
                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = -4,
                        Msg = "测量超时"
                    };
                }

                int countAfter = MainWindow.ViewResultSpectrums.Count;

                if (countAfter > countBefore)
                {
                    // Get the latest measurement result
                    var latest = Application.Current.Dispatcher.Invoke(() =>
                    {
                        var results = MainWindow.ViewResultSpectrums;
                        return MainWindow.ViewResultManager.Config.OrderByType == SqlSugar.OrderByType.Desc
                            ? results.FirstOrDefault()
                            : results.LastOrDefault();
                    });

                    if (latest != null)
                    {
                        var data = new
                        {
                            latest.Lv,
                            x = latest.fx,
                            y = latest.fy,
                            u = latest.fu,
                            v = latest.fv,
                            CCT = latest.fCCT,
                            Duv = latest.dC,
                            DominantWavelength = latest.fLd,
                            PeakWavelength = latest.fLp,
                            HalfBandwidth = latest.fHW,
                            ColorPurity = latest.fPur,
                            Ra = latest.fRa,
                            latest.IP,
                            latest.Blue,
                            IntTime = SpectrometerManager.Instance.IntTime
                        };

                        return new SocketResponse
                        {
                            MsgID = request.MsgID,
                            EventName = EventName,
                            Code = 200,
                            Msg = "测量完成",
                            Data = data
                        };
                    }
                }

                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = -3,
                    Msg = "测量完成但未获取到结果数据"
                };
            }
            catch (Exception ex)
            {
                log.Error("Socket光谱测量异常", ex);
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = -99,
                    Msg = $"测量异常: {ex.Message}"
                };
            }
        }
    }
}

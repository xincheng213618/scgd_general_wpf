using ColorVision.SocketProtocol;
using cvColorVision;
using log4net;
using System.Net.Sockets;
using System.Windows;

namespace Spectrum.Socket
{
    /// <summary>
    /// Socket指令处理器：光谱仪校零
    /// 执行暗电流校准（Dark Calibration），支持自动快门控制
    /// 
    /// 请求示例:
    /// {"EventName":"SpectrumDarkCalibration","MsgID":"1","Version":"1.0","Params":""}
    /// </summary>
    public class SpectrumDarkCalibrationSocketHandler : ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumDarkCalibrationSocketHandler));

        public string EventName => "SpectrumDarkCalibration";

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
                log.Info("Socket指令: 执行光谱仪校零");

                var task = Task.Run(async () =>
                {
                    if (manager.ShutterController.IsConnected)
                    {
                        log.Debug("开启快门进行校零");
                        await manager.ShutterController.OpenShutter();
                    }

                    int ret = Spectrometer.CM_Emission_DarkStorage(
                        manager.Handle, manager.IntTime, manager.Average, 0, manager.fDarkData);

                    if (manager.ShutterController.IsConnected)
                    {
                        log.Debug("关闭快门");
                        await manager.ShutterController.CloseShutter();
                    }

                    return ret;
                });

                if (!task.Wait(TimeSpan.FromSeconds(30)))
                {
                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = -4,
                        Msg = "校零操作超时"
                    };
                }

                int result = task.Result;
                if (result == 1)
                {
                    log.Info("校零完成");
                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = 200,
                        Msg = "校零成功"
                    };
                }
                else
                {
                    string errorMsg = Spectrometer.GetErrorMessage(result);
                    log.Error($"校零失败: {errorMsg}");
                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = -3,
                        Msg = $"校零失败: {errorMsg}"
                    };
                }
            }
            catch (Exception ex)
            {
                log.Error("Socket校零异常", ex);
                return new SocketResponse
                {
                    MsgID = request.MsgID,
                    EventName = EventName,
                    Code = -99,
                    Msg = $"校零异常: {ex.Message}"
                };
            }
        }
    }
}

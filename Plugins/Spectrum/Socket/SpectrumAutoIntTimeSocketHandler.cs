using ColorVision.SocketProtocol;
using cvColorVision;
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

                float fIntTime = 0;
                int ret;

                if (manager.IntTimeConfig.IsOldVersion)
                {
                    ret = Spectrometer.CM_Emission_GetAutoTime(
                        manager.Handle, ref fIntTime,
                        manager.IntTimeConfig.IntLimitTime,
                        manager.IntTimeConfig.AutoIntTimeB,
                        (int)manager.IntTimeConfig.MaxPercent);
                }
                else
                {
                    ret = Spectrometer.CM_Emission_GetAutoTimeEx(
                        manager.Handle, ref fIntTime,
                        manager.IntTimeConfig.IntLimitTime,
                        manager.IntTimeConfig.AutoIntTimeB,
                        manager.IntTimeConfig.Max, null);
                }

                if (ret == 1)
                {
                    // Apply sync frequency adjustment if enabled
                    if (manager.GetDataConfig.IsSyncFrequencyEnabled)
                    {
                        float syncIntTime = fIntTime;
                        COLOR_PARA cOLOR_PARA = new COLOR_PARA();
                        int syncRet = Spectrometer.CM_Emission_GetDataSyncfreq(
                            manager.Handle, 0,
                            manager.GetDataConfig.Syncfreq,
                            manager.GetDataConfig.SyncfreqFactor,
                            ref syncIntTime, manager.Average,
                            manager.GetDataConfig.FilterBW,
                            manager.fDarkData, 0, 0,
                            manager.GetDataConfig.SetWL1,
                            manager.GetDataConfig.SetWL2,
                            ref cOLOR_PARA);

                        if (syncRet == 1)
                        {
                            log.Info($"同步频率调整积分时间: {fIntTime}ms → {syncIntTime}ms");
                            fIntTime = syncIntTime;
                        }
                    }

                    manager.IntTime = fIntTime;
                    log.Info($"自动积分时间获取成功: {fIntTime}ms");

                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = 200,
                        Msg = "自动积分时间获取成功",
                        Data = new { IntTime = fIntTime }
                    };
                }
                else
                {
                    string errorMsg = Spectrometer.GetErrorMessage(ret);
                    log.Warn($"自动积分时间获取失败: {errorMsg}");
                    return new SocketResponse
                    {
                        MsgID = request.MsgID,
                        EventName = EventName,
                        Code = -3,
                        Msg = $"自动积分时间获取失败: {errorMsg}"
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

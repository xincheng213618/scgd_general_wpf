using cvColorVision;
using log4net;
using Quartz;
using System.ComponentModel;

namespace Spectrum.Job
{
    /// <summary>
    /// 光谱仪校零定时任务
    /// 执行暗电流校准（Dark Calibration），支持自动快门控制
    /// </summary>
    [DisplayName("光谱仪校零")]
    public class SpectrumDarkCalibrationJob : IJob
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumDarkCalibrationJob));

        public async Task Execute(IJobExecutionContext context)
        {
            var manager = SpectrometerManager.Instance;

            if (MainWindow.Instance == null)
            {
                log.Warn("光谱仪窗口未打开，无法执行校零任务");
                throw new JobExecutionException("光谱仪窗口未打开");
            }

            if (!manager.IsConnected)
            {
                log.Warn("光谱仪未连接，无法执行校零任务");
                throw new JobExecutionException("光谱仪未连接");
            }

            log.Info("开始执行光谱仪校零任务");

            int ret = await manager.PerformDarkCalibrationAsync();

            if (ret == 1)
            {
                log.Info("校零任务执行成功");
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Error($"校零任务执行失败: {errorMsg}");
                throw new JobExecutionException($"校零失败: {errorMsg}");
            }
        }
    }
}

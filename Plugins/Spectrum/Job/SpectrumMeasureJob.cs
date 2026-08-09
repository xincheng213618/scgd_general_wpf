using ColorVision.Scheduler;
using log4net;
using Quartz;
using System.ComponentModel;

namespace Spectrum.Job
{
    /// <summary>
    /// 光谱仪测量定时任务
    /// 支持单次和多次测量，可通过配置指定测量次数和间隔
    /// </summary>
    [DisplayName("光谱仪测量")]
    public class SpectrumMeasureJob : IJob, IConfigurableJob
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumMeasureJob));

        public Type ConfigType => typeof(SpectrumMeasureJobConfig);

        public IJobConfig CreateDefaultConfig() => new SpectrumMeasureJobConfig();

        public async Task Execute(IJobExecutionContext context)
        {
            SpectrometerManager manager = SpectrometerManager.Instance;
            if (!manager.IsConnected)
            {
                log.Warn("光谱仪未连接，无法执行测量任务");
                throw new JobExecutionException("光谱仪未连接");
            }

            int measureCount = 1;
            int measureInterval = 100;

            if (context.JobDetail.JobDataMap["SchedulerInfo"] is SchedulerInfo schedulerInfo
                && schedulerInfo.Config is SpectrumMeasureJobConfig config)
            {
                measureCount = config.MeasureCount > 0 ? config.MeasureCount : 1;
                measureInterval = config.MeasureInterval > 0 ? config.MeasureInterval : 100;
            }

            log.Info($"开始执行光谱测量任务: 测量次数={measureCount}, 间隔={measureInterval}ms");

            for (int i = 0; i < measureCount; i++)
            {
                SpectrumMeasurementResult result = await manager.MeasureAsync(context.CancellationToken);
                if (!result.IsSuccess)
                    throw new JobExecutionException(result.ErrorMessage ?? "光谱测量失败");

                if (i < measureCount - 1)
                    await Task.Delay(measureInterval, context.CancellationToken);
            }

            log.Info("光谱测量任务执行完成");
        }
    }
}

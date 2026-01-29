using ColorVision.Scheduler;
using Quartz;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;


namespace ColorVision.Engine.Services.Devices.Spectrum.Job
{
    [DisplayName("光谱仪单次测试")]
    public class SpectrumGetDataJob : IJob, IConfigurableJob
    {
        public Type ConfigType => typeof(SpectrumGetDataJobConfig);

        public IJobConfig CreateDefaultConfig()
        {
            var config = new SpectrumGetDataJobConfig();
            // Set default to first available device
            var firstDevice = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault();
            if (firstDevice != null)
            {
                config.DeviceSpectrumName = firstDevice.Config.Name;
            }
            return config;
        }

        public Task Execute(IJobExecutionContext context)
        {            
            if (context.JobDetail.JobDataMap["SchedulerInfo"] is not SchedulerInfo schedulerInfo)
            {
                return Task.CompletedTask;
            }
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                DeviceSpectrum deviceSpectrum = null;
                
                if (schedulerInfo.Config is SpectrumGetDataJobConfig config && !string.IsNullOrEmpty(config.DeviceSpectrumName))
                {
                    deviceSpectrum = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>()
                        .FirstOrDefault(d => d.Config.Name == config.DeviceSpectrumName);
                }
                deviceSpectrum?.DService.GetData();
            });
            return Task.CompletedTask;
        }
    }
}

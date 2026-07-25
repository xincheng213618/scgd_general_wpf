using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices;
using ColorVision.Scheduler;
using Quartz;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Devices.Spectrum.Job
{
    [Display(Name = "Engine_PG_SingleSpectrumTest", ResourceType = typeof(Properties.Resources))]
    [DisallowConcurrentExecution]
    public class SpectrumGetDataJob : IJob, IConfigurableJob
    {
        public Type ConfigType => typeof(SpectrumGetDataJobConfig);

        public IJobConfig CreateDefaultConfig()
        {
            var config = new SpectrumGetDataJobConfig();
            var firstDevice = ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault();
            if (firstDevice != null)
            {
                config.DeviceSpectrumName = firstDevice.Config.Name;
            }
            return config;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken cancellationToken = context.CancellationToken;
            cancellationToken.ThrowIfCancellationRequested();

            SchedulerInfo schedulerInfo = ScheduledDeviceJobHelper.GetSchedulerInfo(context);
            Dispatcher dispatcher = ScheduledDeviceJobHelper.GetApplicationDispatcher();
            MsgRecord? msgRecord = await dispatcher.InvokeAsync(() =>
            {
                DeviceSpectrum? deviceSpectrum = null;
                if (schedulerInfo.Config is SpectrumGetDataJobConfig config &&
                    !string.IsNullOrEmpty(config.DeviceSpectrumName))
                {
                    deviceSpectrum = ServiceManager.GetInstance().DeviceServices
                        .OfType<DeviceSpectrum>()
                        .FirstOrDefault(d => d.Config.Name == config.DeviceSpectrumName);
                }

                if (deviceSpectrum == null)
                {
                    throw new JobExecutionException(Properties.Resources.Failure);
                }

                return deviceSpectrum.DService.GetData();
            }, DispatcherPriority.Normal, cancellationToken).Task;

            if (msgRecord == null)
            {
                throw new JobExecutionException(Properties.Resources.Failure);
            }

            MsgRecordState terminalState;
            try
            {
                terminalState = await ScheduledDeviceJobHelper.WaitForTerminalStateAsync(
                    msgRecord,
                    ScheduledDeviceJobHelper.GetTimeout(schedulerInfo),
                    cancellationToken);
            }
            catch (TimeoutException ex)
            {
                throw new JobExecutionException(Properties.Resources.Timeout, ex);
            }

            if (terminalState != MsgRecordState.Success)
            {
                throw ScheduledDeviceJobHelper.CreateTerminalStateException(msgRecord, terminalState);
            }
        }
    }
}

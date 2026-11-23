using ColorVision.Engine.Messages;
using ColorVision.Scheduler;
using Quartz;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Devices.Spectrum.Job
{
    [Display(Name = nameof(Properties.Resources.Engine_PG_SingleSpectrumTest), ResourceType = typeof(Properties.Resources))]
    [DisallowConcurrentExecution]
    public class SpectrumGetDataJob : IJob, IConfigurableJob
    {
        private readonly Func<IEnumerable<DeviceSpectrum>> devices;
        private readonly Func<DeviceSpectrum, MsgRecord?> capture;

        public SpectrumGetDataJob() : this(
            () => ServiceManager.GetInstance().DeviceServices.OfType<DeviceSpectrum>(),
            device => device.DService.GetData()) { }

        internal SpectrumGetDataJob(Func<IEnumerable<DeviceSpectrum>> devices, Func<DeviceSpectrum, MsgRecord?> capture)
        {
            this.devices = devices;
            this.capture = capture;
        }

        public Type ConfigType => typeof(SpectrumGetDataJobConfig);

        public IJobConfig CreateDefaultConfig()
        {
            var config = new SpectrumGetDataJobConfig();
            var firstDevice = devices().FirstOrDefault(device => !string.IsNullOrWhiteSpace(device.Code));
            if (firstDevice != null)
            {
                config.DeviceSpectrumCode = firstDevice.Code;
            }
            return config;
        }

        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SchedulerInfo schedulerInfo = ScheduledDeviceJobHelper.GetSchedulerInfo(context);
            Dispatcher dispatcher = ScheduledDeviceJobHelper.GetApplicationDispatcher();
            MsgRecord? msgRecord = await dispatcher.InvokeAsync(() =>
            {
                if (schedulerInfo.Config is not SpectrumGetDataJobConfig config)
                    throw ScheduledDeviceJobHelper.CreateDeviceSelectionException(ambiguous: false);

                var device = ScheduledDeviceJobHelper.ResolveDevice(devices(), config.DeviceSpectrumCode, config.DeviceSpectrumName, out bool ambiguous)
                    ?? throw ScheduledDeviceJobHelper.CreateDeviceSelectionException(ambiguous);
                return ScheduledDeviceJobHelper.DispatchCapture(device, capture);
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
                throw new JobExecutionException(EngineLocalization.Get("Scheduler_DeviceReplyWaitTimedOut"), ex);
            }

            if (terminalState != MsgRecordState.Success)
            {
                throw ScheduledDeviceJobHelper.CreateTerminalStateException(msgRecord, terminalState);
            }
        }
    }
}

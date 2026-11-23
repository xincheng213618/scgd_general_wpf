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

namespace ColorVision.Engine.Services.Devices.Camera.Job
{
    [Display(Name = "Engine_PG_CameraCaptureJob", ResourceType = typeof(Properties.Resources))]
    [DisallowConcurrentExecution]
    public class CameraCaptureJob : IJob, IConfigurableJob
    {
        private readonly Func<IEnumerable<DeviceCamera>> devices;
        private readonly Func<DeviceCamera, MsgRecord?> capture;

        public CameraCaptureJob() : this(
            () => ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>(),
            device => device.DisplayCameraControlLazy.Value.GetData()) { }

        internal CameraCaptureJob(Func<IEnumerable<DeviceCamera>> devices, Func<DeviceCamera, MsgRecord?> capture)
        {
            this.devices = devices;
            this.capture = capture;
        }

        public Type ConfigType => typeof(CameraCaptureJobConfig);

        public IJobConfig CreateDefaultConfig()
        {
            var config = new CameraCaptureJobConfig();
            var firstDevice = devices().FirstOrDefault(device => !string.IsNullOrWhiteSpace(device.Code));
            if (firstDevice != null)
            {
                config.DeviceCameraCode = firstDevice.Code;
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
                if (schedulerInfo.Config is not CameraCaptureJobConfig config)
                    throw ScheduledDeviceJobHelper.CreateDeviceSelectionException(ambiguous: false);

                var device = ScheduledDeviceJobHelper.ResolveDevice(devices(), config.DeviceCameraCode, config.DeviceCameraName, out bool ambiguous)
                    ?? throw ScheduledDeviceJobHelper.CreateDeviceSelectionException(ambiguous);
                return ScheduledDeviceJobHelper.DispatchCapture(device, capture);
            }, DispatcherPriority.Normal, cancellationToken).Task;

            if (msgRecord == null)
            {
                throw new JobExecutionException(Properties.Resources.CameraGetDataReturnedEmpty);
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

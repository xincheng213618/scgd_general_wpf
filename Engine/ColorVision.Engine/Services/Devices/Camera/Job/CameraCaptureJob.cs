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

namespace ColorVision.Engine.Services.Devices.Camera.Job
{
    [Display(Name = "Engine_PG_CameraCaptureJob", ResourceType = typeof(Properties.Resources))]
    [DisallowConcurrentExecution]
    public class CameraCaptureJob : IJob, IConfigurableJob
    {
        public Type ConfigType => typeof(CameraCaptureJobConfig);

        public IJobConfig CreateDefaultConfig()
        {
            var config = new CameraCaptureJobConfig();
            var firstDevice = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().FirstOrDefault();
            if (firstDevice != null)
            {
                config.DeviceCameraName = firstDevice.Config.Name;
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
                DeviceCamera? deviceCamera = null;
                if (schedulerInfo.Config is CameraCaptureJobConfig config &&
                    !string.IsNullOrEmpty(config.DeviceCameraName))
                {
                    deviceCamera = ServiceManager.GetInstance().DeviceServices
                        .OfType<DeviceCamera>()
                        .FirstOrDefault(d => d.Config.Name == config.DeviceCameraName);
                }

                if (deviceCamera == null)
                {
                    throw new JobExecutionException(Properties.Resources.NoAvailableCameraDevice);
                }

                return deviceCamera.DisplayCameraControlLazy.Value.GetData();
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
                throw new JobExecutionException(Properties.Resources.Timeout, ex);
            }

            if (terminalState != MsgRecordState.Success)
            {
                throw ScheduledDeviceJobHelper.CreateTerminalStateException(msgRecord, terminalState);
            }
        }
    }
}

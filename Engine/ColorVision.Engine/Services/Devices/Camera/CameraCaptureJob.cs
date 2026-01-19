using ColorVision.Scheduler;
using Quartz;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera
{
    /// <summary>
    /// Configuration for camera capture job
    /// </summary>
    public class CameraCaptureJobConfig : JobConfigBase
    {
        [Category("相机设置")]
        [DisplayName("相机设备名称")]
        [Description("输入要使用的相机设备名称")]
        public string DeviceCameraName { get => _DeviceCameraName; set { _DeviceCameraName = value; OnPropertyChanged(); } }
        private string _DeviceCameraName;
    }

    [DisplayName("相机拍摄任务")]
    public class CameraCaptureJob : IJob, IConfigurableJob
    {
        public System.Type ConfigType => typeof(CameraCaptureJobConfig);

        public IJobConfig CreateDefaultConfig()
        {
            var config = new CameraCaptureJobConfig();
            // Set default to first available device
            var firstDevice = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().FirstOrDefault();
            if (firstDevice != null)
            {
                config.DeviceCameraName = firstDevice.Config.Name;
            }
            return config;
        }

        public Task Execute(IJobExecutionContext context)
        {
            var schedulerInfo = QuartzSchedulerManager.GetInstance().TaskInfos.First(x => x.JobName == context.JobDetail.Key.Name && x.GroupName == context.JobDetail.Key.Group);
            schedulerInfo.RunCount++;
            Application.Current.Dispatcher.Invoke(() =>
            {
                schedulerInfo.Status = SchedulerStatus.Running;
            });

            // 定时任务逻辑
            Application.Current.Dispatcher.Invoke(() =>
            {
                DeviceCamera deviceCamera = null;

                // Try to get device from config
                if (schedulerInfo.Config is CameraCaptureJobConfig config && !string.IsNullOrEmpty(config.DeviceCameraName))
                {
                    deviceCamera = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>()
                        .FirstOrDefault(d => d.Config.Name == config.DeviceCameraName);
                }

                // Fallback to first device if config not found
                if (deviceCamera == null)
                {
                    deviceCamera = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().FirstOrDefault();
                }

                deviceCamera?.DisplayCameraControlLazy.Value.GetData();

                schedulerInfo.Status = SchedulerStatus.Ready;
            });
            return Task.CompletedTask;
        }
    }
}


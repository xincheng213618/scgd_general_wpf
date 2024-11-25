using ColorVision.Scheduler;
using Quartz;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public class CameraCaptureJob : IJob
    {
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
                var lsit = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList();
                DeviceCamera deviceCamera = lsit.FirstOrDefault();
                deviceCamera?.DisplayCameraControlLazy.Value.GetData();


                schedulerInfo.Status = SchedulerStatus.Ready;
            });
            return Task.CompletedTask;
        }
    }
}


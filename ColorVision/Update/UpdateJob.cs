#pragma warning disable CS8602
using ColorVision.Scheduler;
using Quartz;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public class UpdateJob : IJob
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
            Application.Current.Dispatcher.Invoke(async () =>
            {
                AutoUpdater.DeleteAllCachedUpdateFiles();
                AutoUpdater autoUpdater = AutoUpdater.GetInstance();
                await autoUpdater.CheckAndUpdate(true);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    schedulerInfo.Status = SchedulerStatus.Ready;
                });
            });
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (schedulerInfo.IsCron && schedulerInfo.RunCount > schedulerInfo.RepeatCount)
                {
                    schedulerInfo.DeleteCommand.RaiseExecute(context);

                }
            });


            return Task.CompletedTask;
        }
    }
}

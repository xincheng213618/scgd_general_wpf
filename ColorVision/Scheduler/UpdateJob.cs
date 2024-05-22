using ColorVision.Update;
using Quartz;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Scheduler
{
    public class UpdateJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            // 定时任务逻辑
            Application.Current.Dispatcher.Invoke(() =>
            {
                AutoUpdater.DeleteAllCachedUpdateFiles();
                AutoUpdater autoUpdater = AutoUpdater.GetInstance();
                autoUpdater.CheckAndUpdate(true);
            });
            return Task.CompletedTask;
        }
    }
}

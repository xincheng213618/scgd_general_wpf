#pragma warning disable CA1822,CS0168,CS0219,CS4014,CS8601
using ColorVision.Scheduler;
using log4net;
using ProjectARVRPro.PluginConfig;
using Quartz;
using System.Windows;

namespace ProjectARVRPro
{
    public class ProjectARVRLitetestJob : IJob
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectARVRLitetestJob));

        public Task Execute(IJobExecutionContext context)
        {
            var schedulerInfo = QuartzSchedulerManager.GetInstance().TaskInfos.First(x => x.JobName == context.JobDetail.Key.Name && x.GroupName == context.JobDetail.Key.Group);
            schedulerInfo.RunCount++;
            Application.Current.Dispatcher.Invoke(() =>
            {
                schedulerInfo.Status = SchedulerStatus.Running;
            });
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProjectWindowInstance.WindowInstance.SwitchPGCompleted();

                ProjectWindowInstance.WindowInstance.RunTemplate();

                schedulerInfo.Status = SchedulerStatus.Ready;
            });
            return Task.CompletedTask;
        }
    }
}

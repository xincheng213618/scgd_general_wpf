using ColorVision.Common.MVVM;
using ColorVision.Update;
using Quartz;
using Quartz.Impl;
using Quartz.Listener;
using System;
using System.Threading;
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

    public class TaskExecutionListener : JobListenerSupport
    {
        public override string Name => "TaskExecutionListener";

        public event Action<IJobExecutionContext> JobExecutedEvent;

        public override Task JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException, CancellationToken cancellationToken = default)
        {
            base.JobWasExecuted(context, jobException, cancellationToken);
            JobExecutedEvent?.Invoke(context);
            return Task.CompletedTask;
        }
    }

    public class TaskInfo: ViewModelBase
    {
        public string JobName { get => _JobName; set { _JobName = value; NotifyPropertyChanged(); } }
        private string _JobName;
        public string GroupName { get => _GroupName; set { _GroupName = value; NotifyPropertyChanged(); } }
        private string _GroupName;
        public string NextFireTime { get => _NextFireTime; set { _NextFireTime = value; NotifyPropertyChanged(); } }
        private string _NextFireTime;
        public string PreviousFireTime { get => _PreviousFireTime; set { _PreviousFireTime = value; NotifyPropertyChanged(); } }
        private string _PreviousFireTime;
    }

    public static class QuartzScheduler
    {
        private static IScheduler _scheduler;
        public static TaskExecutionListener Listener { get; set; }

        public static async Task Start()
        {
            // 创建调度器
            _scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            await _scheduler.Start();

            Listener = new TaskExecutionListener();
            _scheduler.ListenerManager.AddJobListener(Listener);

            //// 定义任务
            //IJobDetail job = JobBuilder.Create<UpdateJob>()
            //    .WithIdentity("updateJob", "group1")
            //    .Build();

            //// 定义触发器
            //ITrigger trigger = TriggerBuilder.Create()
            //    .WithIdentity("updateTrigger", "group1")
            //    .StartNow()
            //    .WithSimpleSchedule(x => x
            //        .WithIntervalInSeconds(10)
            //        .RepeatForever())
            //    .Build();

            //// 调度任务
            //await _scheduler.ScheduleJob(job, trigger);
        }

        public static async Task Stop()
        {
            if (_scheduler != null)
            {
                await _scheduler.Shutdown();
            }
        }
    }
}

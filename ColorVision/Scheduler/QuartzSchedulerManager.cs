using Quartz;
using Quartz.Impl;
using System.Threading.Tasks;

namespace ColorVision.Scheduler
{
    public class QuartzSchedulerManager
    {
        private static QuartzSchedulerManager _instance;
        private static readonly object _locker = new();
        public static QuartzSchedulerManager GetInstance() { lock (_locker) { return _instance ??= new QuartzSchedulerManager(); } }

        public QuartzSchedulerManager()
        {
            Task.Run(() => Start());
        }

        private IScheduler _scheduler;
        public TaskExecutionListener Listener { get; set; }

        public async Task Start()
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

        public async Task Stop()
        {
            if (_scheduler != null)
            {
                await _scheduler.Shutdown();
            }
        }
    }
}

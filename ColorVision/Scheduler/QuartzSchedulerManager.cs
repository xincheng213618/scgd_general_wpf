using ColorVision.Solution.Searches;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

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

        public Dictionary<string,Type> Jobs { get; set; }

        public async Task Start()
        {
            // 创建调度器
            _scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            await _scheduler.Start();

            Listener = new TaskExecutionListener();
            _scheduler.ListenerManager.AddJobListener(Listener);

            Jobs = new Dictionary<string, Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IJob).IsAssignableFrom(type) && !type.IsInterface)
                    {
                        Jobs.Add(type.Name, type);
                    }
                }
            }

        }
        public async Task StopJob(string jobName, string groupName)
        {
            JobKey jobKey = new JobKey(jobName, groupName);
            if (await _scheduler.CheckExists(jobKey))
            {
                await _scheduler.PauseJob(jobKey);
            }
        }
        public async Task<SchedulerInfo?> CreateJob(string jobName, string groupName, string cronExpression, string selectedJobName)
        {
            var selectedJobType = Jobs[selectedJobName];

            var scheduler = await StdSchedulerFactory.GetDefaultScheduler();

            // 动态创建Job实例
            var job = JobBuilder.Create(selectedJobType)
                .WithIdentity(jobName, groupName)
                .UsingJobData("scriptPath", "path\\to\\your\\script.cmd")
                .Build();

            // 创建触发器
            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{jobName}-trigger", groupName)
                .WithCronSchedule(cronExpression)
                .Build();

            // 调度Job
            await scheduler.ScheduleJob(job, trigger);

            // 添加任务信息到列表  
            var taskInfo = new SchedulerInfo
            {
                JobName = jobName,
                GroupName = groupName,
                NextFireTime = trigger.GetNextFireTimeUtc()?.ToLocalTime().ToString() ?? "N/A",
                PreviousFireTime = trigger.GetPreviousFireTimeUtc()?.ToLocalTime().ToString() ?? "N/A"
            };
            MessageBox.Show("Task created successfully.");
            return taskInfo;
        }
        public async Task RemoveJob(string jobName, string groupName)
        {
            JobKey jobKey = new JobKey(jobName, groupName);
            if (await _scheduler.CheckExists(jobKey))
            {
                await _scheduler.DeleteJob(jobKey);
            }
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

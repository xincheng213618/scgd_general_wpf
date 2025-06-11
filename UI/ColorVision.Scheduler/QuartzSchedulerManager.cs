using ColorVision.Common.MVVM;
using ColorVision.UI;
using Quartz;
using Quartz.Impl;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Scheduler
{
    public class QuartzSchedulerConfig : IConfig
    {
        public static QuartzSchedulerConfig Instance => ConfigService.Instance.GetRequiredService<QuartzSchedulerConfig>();
        public ObservableCollection<SchedulerInfo> TaskInfos { get; set; } = new ObservableCollection<SchedulerInfo>();

    }

    public class QuartzSchedulerManager
    {
        private static QuartzSchedulerManager _instance;
        private static readonly object _locker = new();
        public static QuartzSchedulerManager GetInstance() { lock (_locker) { return _instance ??= new QuartzSchedulerManager(); } }
        public ObservableCollection<SchedulerInfo> TaskInfos => QuartzSchedulerConfig.Instance.TaskInfos;
        public IScheduler Scheduler { get; set; }
        public TaskExecutionListener Listener { get; set; }

        public Dictionary<string, Type> Jobs { get; set; }

        public RelayCommand PauseAllCommand { get; set; }
        public RelayCommand ResumeAllCommand { get; set; }
        public RelayCommand StartCommand { get; set; }
        public RelayCommand ShutdownCommand { get; set; }


        public QuartzSchedulerManager()
        {
            Task.Run(() => Start());
        }

        public string GetNewJobName(string jobName)
        {
            if (TaskInfos.Any(x => x.JobName == jobName))
                return jobName;
            for (int i = 1; i < 999; i++)
            {
                if (TaskInfos.Any(x => x.JobName == $"{jobName}{i}"))
                    return $"{jobName}{i}";
            }
            return jobName;
        }

        public string GetNewGroupName(string groupName)
        {
            if (TaskInfos.Any(x => x.GroupName == groupName))
                return groupName;
            for (int i = 1; i < 999; i++)
            {
                if (TaskInfos.Any(x => x.GroupName == $"{groupName}{i}"))
                    return $"{groupName}{i}";
            }
            return groupName;
        }

        public async Task PauseAll()
        {
            await Scheduler.PauseAll();
            foreach (var item in TaskInfos)
            {
                item.Status = SchedulerStatus.Paused;
            }
        }

        public async Task ResumeAll()
        {
            await Scheduler.ResumeAll();
            foreach (var item in TaskInfos)
            {
                item.Status = SchedulerStatus.Ready;
            }
        }

        public async Task Start()
        {
            Scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            PauseAllCommand = new RelayCommand(async a => await PauseAll(), a => Scheduler.IsStarted);
            ResumeAllCommand = new RelayCommand(async a => await ResumeAll(), a => Scheduler.IsStarted);
            StartCommand = new RelayCommand(async a => await Scheduler.Start(), a => true);
            ShutdownCommand = new RelayCommand(async a => await Scheduler.Shutdown(), a => Scheduler.IsStarted);

            // 创建调度器
            await Scheduler.Start();

            Listener = new TaskExecutionListener();
            Scheduler.ListenerManager.AddJobListener(Listener);
            Jobs = new Dictionary<string, Type>();

            foreach (var assembly in AssemblyService.Instance.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IJob).IsAssignableFrom(type) && !type.IsInterface)
                    {
                        Jobs.Add(type.Name, type);
                    }
                }
            }
            //5s 后恢复任务
            await Task.Delay(5000);
            foreach (var item in QuartzSchedulerConfig.Instance.TaskInfos)
            {
                await CreateJob(item);
            }
        }
        public async Task StopJob(string jobName, string groupName)
        {
            JobKey jobKey = new JobKey(jobName, groupName);
            if (await Scheduler.CheckExists(jobKey))
            {
                await Scheduler.PauseJob(jobKey);
            }
        }

        public async Task RemoveJob(string jobName, string groupName)
        {
            JobKey jobKey = new JobKey(jobName, groupName);
            if (await Scheduler.CheckExists(jobKey))
            {
                await Scheduler.DeleteJob(jobKey);
            }
        }
        public async Task ResumeJob(string jobName, string groupName)
        {
            JobKey jobKey = new JobKey(jobName, groupName);
            if (await Scheduler.CheckExists(jobKey))
            {
                await Scheduler.ResumeJob(jobKey);
            }
        }

        public async Task CreateJob(SchedulerInfo schedulerInfo)
        {
            var selectedJobType = schedulerInfo.JobType;

            var scheduler = await StdSchedulerFactory.GetDefaultScheduler();
            // 动态创建Job实例
            var job = JobBuilder.Create(selectedJobType)
                .WithIdentity(schedulerInfo.JobName, schedulerInfo.GroupName)
                .Build();

            // 创建触发器
            TriggerBuilder triggerBuilder = TriggerBuilder.Create().WithIdentity($"{schedulerInfo.JobName}-trigger", schedulerInfo.GroupName);

            switch (schedulerInfo.JobStartMode)
            {
                case JobStartMode.Immediate:
                    triggerBuilder.StartNow();
                    break;
                case JobStartMode.Delayed:
                    triggerBuilder.StartAt(DateBuilder.FutureDate((int)schedulerInfo.Delay.TotalSeconds, IntervalUnit.Second));
                    break;
                default:
                    break;
            }
            switch (schedulerInfo.Mode)
            {
                case JobExecutionMode.Simple:



                    triggerBuilder
                        .WithSimpleSchedule(x => 
                        {
                            switch (schedulerInfo.RepeatMode)
                            {
                                case JobRepeatMode.Multiple:
                                    x.WithInterval(schedulerInfo.Interval);
                                    x.WithRepeatCount(schedulerInfo.RepeatCount);
                                    break;
                                case JobRepeatMode.Forever:
                                    x.WithInterval(schedulerInfo.Interval);
                                    x.RepeatForever();
                                    break;
                            case JobRepeatMode.Once:
                            default:
                                    break;
                            }
                        });
                    break;
                case JobExecutionMode.Calendar:
                    triggerBuilder.WithCalendarIntervalSchedule(x => x.WithIntervalInDays(1)); // 每天执行一次
                    break;
                case JobExecutionMode.Interval:
                    triggerBuilder.WithDailyTimeIntervalSchedule(x =>
                    {
                        x.WithInterval((int)schedulerInfo.Interval.TotalSeconds ,IntervalUnit.Second);
                        switch (schedulerInfo.RepeatMode)
                        {
                            case JobRepeatMode.Multiple:
                                x.WithRepeatCount(schedulerInfo.RepeatCount);
                                break;
                            case JobRepeatMode.Forever:
                            case JobRepeatMode.Once:
                                x.WithRepeatCount(0);
                                break;
                            default:
                                break;
                        }
                    });
                    break;
                case JobExecutionMode.Cron:
                    triggerBuilder.WithCronSchedule(schedulerInfo.CronExpression);
                    break;
                default:
                    break;
            }

            if (triggerBuilder != null)
            {
                ITrigger trigger = triggerBuilder.Build();
                // 调度Job
                await scheduler.ScheduleJob(job, trigger);
                schedulerInfo.NextFireTime = trigger.GetNextFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A";
                if (!TaskInfos.Contains(schedulerInfo))
                {
                    TaskInfos.Add(schedulerInfo);

                }
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
                NextFireTime = trigger.GetNextFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A",
                PreviousFireTime = trigger.GetPreviousFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A"
            };
            MessageBox.Show("Task created successfully.");
            return taskInfo;
        }


        public async Task Stop()
        {
            if (Scheduler != null)
            {
                await Scheduler.Shutdown();
            }
        }
    }
}

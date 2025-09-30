using ColorVision.Common.MVVM;
using ColorVision.UI;
using Quartz;
using Quartz.Impl;
using System.Collections.ObjectModel;
using System.Windows;
using System.IO;
using Newtonsoft.Json;

namespace ColorVision.Scheduler
{
    public interface ISchedulerService
    {
        ObservableCollection<SchedulerInfo> TaskInfos { get; }
        Task PauseAll();
        Task ResumeAll();
        Task Start();
        Task Stop();
        Task StopJob(string jobName, string groupName);
        Task RemoveJob(string jobName, string groupName);
        Task ResumeJob(string jobName, string groupName);
        Task CreateJob(SchedulerInfo schedulerInfo);
        Task UpdateJob(SchedulerInfo schedulerInfo);
        string GetNewJobName(string jobName);
        string GetNewGroupName(string groupName);
        Dictionary<string, Type> Jobs { get; }
        IScheduler Scheduler { get; }
        TaskExecutionListener Listener { get; }
        void SaveTasks();
        void LoadTasks();
    }

    public class QuartzSchedulerConfig : IConfig
    {
        public static QuartzSchedulerConfig Instance => ConfigService.Instance.GetRequiredService<QuartzSchedulerConfig>();
        public ObservableCollection<SchedulerInfo> TaskInfos { get; set; } = new ObservableCollection<SchedulerInfo>();

        private static readonly string ConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scheduler_tasks.json");

        public void Save()
        {
            var json = JsonConvert.SerializeObject(TaskInfos, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            File.WriteAllText(ConfigFile, json);
        }

        public void Load()
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                var list = JsonConvert.DeserializeObject<ObservableCollection<SchedulerInfo>>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                if (list != null)
                {
                    TaskInfos.Clear();
                    foreach (var item in list)
                        TaskInfos.Add(item);
                }
            }
        }
    }

    public class QuartzSchedulerManager : ISchedulerService
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
            QuartzSchedulerConfig.Instance.Load();
            Task.Run(() => Start());
        }

        public string GetNewJobName(string jobName)
        {
            if (!TaskInfos.Any(x => x.JobName == jobName))
                return jobName;
            for (int i = 1; i < 999; i++)
            {
                var newName = $"{jobName}{i}";
                if (!TaskInfos.Any(x => x.JobName == newName))
                    return newName;
            }
            return jobName + Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        public string GetNewGroupName(string groupName)
        {
            if (!TaskInfos.Any(x => x.GroupName == groupName))
                return groupName;
            for (int i = 1; i < 999; i++)
            {
                var newName = $"{groupName}{i}";
                if (!TaskInfos.Any(x => x.GroupName == newName))
                    return newName;
            }
            return groupName + Guid.NewGuid().ToString("N").Substring(0, 6);
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
            var info = TaskInfos.FirstOrDefault(x => x.JobName == jobName && x.GroupName == groupName);
            if (info != null)
            {
                TaskInfos.Remove(info);
                SaveTasks();
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
            // 参数校验
            if (!ValidateSchedulerInfo(schedulerInfo, out string errorMsg))
            {
                MessageBox.Show(errorMsg, "参数错误");
                return;
            }
            var selectedJobType = schedulerInfo.JobType;
            var scheduler = Scheduler;
            if (scheduler == null)
                return;
            var job = JobBuilder.Create(selectedJobType)
                .WithIdentity(schedulerInfo.JobName, schedulerInfo.GroupName)
                .Build();
            ITrigger trigger = BuildTrigger(schedulerInfo);
            if (trigger != null)
            {
                await scheduler.ScheduleJob(job, trigger);
                schedulerInfo.NextFireTime = trigger.GetNextFireTimeUtc()?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A";
                if (!TaskInfos.Contains(schedulerInfo))
                {
                    TaskInfos.Add(schedulerInfo);
                    SaveTasks();
                }
            }
        }

        public async Task UpdateJob(SchedulerInfo schedulerInfo)
        {
            // 先删除原任务，再创建新任务
            await RemoveJob(schedulerInfo.JobName, schedulerInfo.GroupName);
            await CreateJob(schedulerInfo);
        }

        private ITrigger BuildTrigger(SchedulerInfo schedulerInfo)
        {
            var triggerBuilder = TriggerBuilder.Create().WithIdentity($"{schedulerInfo.JobName}-trigger", schedulerInfo.GroupName);
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
                    triggerBuilder.WithSimpleSchedule(x =>
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
                    triggerBuilder.WithCalendarIntervalSchedule(x => x.WithIntervalInDays(1));
                    break;
                case JobExecutionMode.Interval:
                    triggerBuilder.WithDailyTimeIntervalSchedule(x =>
                    {
                        x.WithInterval((int)schedulerInfo.Interval.TotalSeconds, IntervalUnit.Second);
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
            return triggerBuilder.Build();
        }

        private bool ValidateSchedulerInfo(SchedulerInfo info, out string errorMsg)
        {
            if (info.JobType == null)
            {
                errorMsg = "任务类型不能为空";
                return false;
            }
            if (string.IsNullOrWhiteSpace(info.JobName) || string.IsNullOrWhiteSpace(info.GroupName))
            {
                errorMsg = "任务名和分组名不能为空";
                return false;
            }
            if (info.Mode == JobExecutionMode.Cron)
            {
                if (string.IsNullOrWhiteSpace(info.CronExpression))
                {
                    errorMsg = "Cron表达式不能为空";
                    return false;
                }
                if (!Quartz.CronExpression.IsValidExpression(info.CronExpression))
                {
                    errorMsg = "Cron表达式不合法";
                    return false;
                }
            }
            if ((info.Mode == JobExecutionMode.Simple || info.Mode == JobExecutionMode.Interval) && info.Interval.TotalSeconds <= 0)
            {
                errorMsg = "间隔时间必须大于0";
                return false;
            }
            if (info.RepeatMode == JobRepeatMode.Multiple && info.RepeatCount <= 0)
            {
                errorMsg = "重复次数必须大于0";
                return false;
            }
            errorMsg = string.Empty;
            return true;
        }

        public void SaveTasks()
        {
            QuartzSchedulerConfig.Instance.Save();
        }

        public void LoadTasks()
        {
            QuartzSchedulerConfig.Instance.Load();
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

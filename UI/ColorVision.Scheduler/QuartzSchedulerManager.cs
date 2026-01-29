using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace ColorVision.Scheduler
{
    public interface ISchedulerService
    {
        ObservableCollection<SchedulerInfo> TaskInfos { get; }
        Task PauseAll();
        Task ResumeAll();
        Task Start();
        Task Shutdown();
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

    public class QuartzSchedulerManager : ISchedulerService
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(QuartzSchedulerManager));
        private static QuartzSchedulerManager _instance;
        private static readonly object _locker = new();
        public static QuartzSchedulerManager GetInstance() { lock (_locker) { return _instance ??= new QuartzSchedulerManager(); } }
        private static readonly string ConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ColorVision", "scheduler_tasks.json");
       
        public ObservableCollection<SchedulerInfo> TaskInfos { get; set; } = new ObservableCollection<SchedulerInfo>();

        public IScheduler Scheduler { get; set; }
        public TaskExecutionListener Listener { get; set; }
        public Dictionary<string, Type> Jobs { get; set; }
        public RelayCommand PauseAllCommand { get; set; }
        public RelayCommand ResumeAllCommand { get; set; }
        public RelayCommand StartCommand { get; set; }
        public RelayCommand ShutdownCommand { get; set; }

        public QuartzSchedulerManager()
        {
            Load();
            Task.Run(() => Start());
        }

        public void Save()
        {
            try
            {
                _logger.Debug($"Saving {TaskInfos.Count} tasks to {ConfigFile}");
                var json = JsonConvert.SerializeObject(TaskInfos, Formatting.Indented, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                File.WriteAllText(ConfigFile, json);
                _logger.Info("Tasks saved successfully");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to save tasks", ex);
                MessageBox.Show($"保存任务配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    _logger.Info($"Loading tasks from {ConfigFile}");
                    var json = File.ReadAllText(ConfigFile);
                    var list = JsonConvert.DeserializeObject<ObservableCollection<SchedulerInfo>>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                    if (list != null)
                    {
                        TaskInfos.Clear();
                        foreach (var item in list)
                            TaskInfos.Add(item);
                        _logger.Info($"Loaded {TaskInfos.Count} tasks successfully");
                    }
                    else
                    {
                        _logger.Warn("Deserialized task list is null");
                    }
                }
                else
                {
                    _logger.Info($"Config file not found: {ConfigFile}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load tasks", ex);
                MessageBox.Show($"加载任务配置失败: {ex.Message}\n将使用空配置启动。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
            return jobName + Guid.NewGuid().ToString("N")[..6];
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
            return groupName + Guid.NewGuid().ToString("N")[..6];
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
            try
            {
                _logger.Info("Starting Quartz Scheduler");
                Scheduler = await StdSchedulerFactory.GetDefaultScheduler();
                PauseAllCommand = new RelayCommand(async a => await PauseAll(), a => Scheduler.IsStarted);
                ResumeAllCommand = new RelayCommand(async a => await ResumeAll(), a => Scheduler.IsStarted);
                StartCommand = new RelayCommand(async a => await Scheduler.Start(), a => true);
                ShutdownCommand = new RelayCommand(async a => await Scheduler.Shutdown(), a => Scheduler.IsStarted);

                // 创建调度器
                await Scheduler.Start();
                _logger.Info("Scheduler started successfully");

                Listener = new TaskExecutionListener(this);
                Scheduler.ListenerManager.AddJobListener(Listener);
                Jobs = new Dictionary<string, Type>();

                _logger.Debug("Discovering job types from assemblies");
                foreach (var assembly in AssemblyService.Instance.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (typeof(IJob).IsAssignableFrom(type) && !type.IsInterface)
                        {
                            string name = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name;
                            Jobs[name] = type;
                        }
                    }
                }
                _logger.Info($"Discovered {Jobs.Count} job types");

                //5s 后恢复任务
                _logger.Debug("Waiting 5 seconds before recovering tasks");
                await Task.Delay(5000);
                
                var failedJobs = new List<string>();
                _logger.Info($"Recovering {TaskInfos.Count} tasks");
                foreach (var item in TaskInfos)
                {
                    try
                    {
                        if (item.JobType != null)
                        {
                            await CreateJob(item);
                            _logger.Debug($"Recovered task: {item.JobName}({item.GroupName})");
                        }
                        else
                        {
                            var errorMsg = $"{item.JobName}({item.GroupName}) 类型丢失";
                            failedJobs.Add(errorMsg);
                            _logger.Warn(errorMsg);
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"{item.JobName}({item.GroupName}): {ex.Message}";
                        failedJobs.Add(errorMsg);
                        _logger.Error($"Failed to recover task: {item.JobName}({item.GroupName})", ex);
                    }
                }
                if (failedJobs.Count > 0)
                {
                    _logger.Warn($"{failedJobs.Count} tasks failed to recover");
                    MessageBox.Show("以下任务未能恢复：\n" + string.Join("\n", failedJobs), "任务恢复警告");
                }
                else
                {
                    _logger.Info("All tasks recovered successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to start scheduler", ex);
                MessageBox.Show($"调度器启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }
        public async Task StopJob(string jobName, string groupName)
        {
            try
            {
                _logger.Info($"Stopping job: {jobName}({groupName})");
                JobKey jobKey = new JobKey(jobName, groupName);
                if (await Scheduler.CheckExists(jobKey))
                {
                    await Scheduler.PauseJob(jobKey);
                    _logger.Info($"Job stopped: {jobName}({groupName})");
                }
                else
                {
                    _logger.Warn($"Job not found: {jobName}({groupName})");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to stop job: {jobName}({groupName})", ex);
                throw;
            }
        }

        public async Task RemoveJob(string jobName, string groupName)
        {
            try
            {
                _logger.Info($"Removing job: {jobName}({groupName})");
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
                    _logger.Info($"Job removed: {jobName}({groupName})");
                }
                else
                {
                    _logger.Warn($"Job not found in TaskInfos: {jobName}({groupName})");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to remove job: {jobName}({groupName})", ex);
                throw;
            }
        }
        
        public async Task ResumeJob(string jobName, string groupName)
        {
            try
            {
                _logger.Info($"Resuming job: {jobName}({groupName})");
                JobKey jobKey = new JobKey(jobName, groupName);
                if (await Scheduler.CheckExists(jobKey))
                {
                    await Scheduler.ResumeJob(jobKey);
                    _logger.Info($"Job resumed: {jobName}({groupName})");
                }
                else
                {
                    _logger.Warn($"Job not found: {jobName}({groupName})");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to resume job: {jobName}({groupName})", ex);
                throw;
            }
        }

        public async Task CreateJob(SchedulerInfo schedulerInfo)
        {
            try
            {
                _logger.Info($"Creating job: {schedulerInfo.JobName}({schedulerInfo.GroupName})");
                
                // 参数校验
                if (!ValidateSchedulerInfo(schedulerInfo, out string errorMsg))
                {
                    _logger.Warn($"Job validation failed: {errorMsg}");
                    MessageBox.Show(errorMsg, "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var selectedJobType = schedulerInfo.JobType;
                var scheduler = Scheduler;
                if (scheduler == null)
                {
                    _logger.Error("Scheduler is null");
                    MessageBox.Show("调度器未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var job = JobBuilder.Create(selectedJobType)
                    .WithIdentity(schedulerInfo.JobName, schedulerInfo.GroupName)
                    .Build();
                job.JobDataMap["SchedulerInfo"] = schedulerInfo;
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
                    _logger.Info($"Job created successfully: {schedulerInfo.JobName}({schedulerInfo.GroupName})");
                }
                else
                {
                    _logger.Error($"Failed to build trigger for job: {schedulerInfo.JobName}");
                    MessageBox.Show("创建触发器失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to create job: {schedulerInfo.JobName}({schedulerInfo.GroupName})", ex);
                MessageBox.Show($"创建任务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        public async Task UpdateJob(SchedulerInfo schedulerInfo)
        {
            try
            {
                _logger.Info($"Updating job: {schedulerInfo.JobName}({schedulerInfo.GroupName})");
                // 先删除原任务，再创建新任务
                await RemoveJob(schedulerInfo.JobName, schedulerInfo.GroupName);
                await CreateJob(schedulerInfo);
                _logger.Info($"Job updated successfully: {schedulerInfo.JobName}({schedulerInfo.GroupName})");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to update job: {schedulerInfo.JobName}({schedulerInfo.GroupName})", ex);
                MessageBox.Show($"更新任务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private static ITrigger BuildTrigger(SchedulerInfo schedulerInfo)
        {
            var triggerBuilder = TriggerBuilder.Create()
                .WithIdentity($"{schedulerInfo.JobName}-trigger", schedulerInfo.GroupName)
                .WithPriority(schedulerInfo.Priority); // 设置优先级
            
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

        private static bool ValidateSchedulerInfo(SchedulerInfo info, out string errorMsg)
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
            Save();
        }

        public void LoadTasks()
        {
            Load();
        }

        public async Task Shutdown()
        {
            if (Scheduler != null)
            {
                await Scheduler.Shutdown();
            }
        }
    }
}

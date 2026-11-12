#pragma warning disable CA1863
#pragma warning disable CA1001 // Process-lifetime singleton owns the mutation gate.
using ColorVision.Common.MVVM;
using ColorVision.Scheduler.Data;
using ColorVision.UI;
using log4net;
using Quartz;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
        Task ExecuteJob(string jobName, string groupName);
        Task<SchedulerOperationResult> CreateJob(SchedulerInfo schedulerInfo);
        Task<SchedulerOperationResult> UpdateJob(SchedulerInfo schedulerInfo);
        Task<SchedulerOperationResult> UpdateJob(SchedulerInfo schedulerInfo, string originalJobName, string originalGroupName);
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
        private const int MaxCopilotTaskSnapshots = 30;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(QuartzSchedulerManager));
        private CopilotDynamicContextSession? _copilotContextSession;
        private static QuartzSchedulerManager _instance;
        private static readonly object _locker = new();
        private readonly SemaphoreSlim _mutationGate = new(1, 1);
        private StandaloneSchedulerFactory? _schedulerFactory;
        public static QuartzSchedulerManager GetInstance() { lock (_locker) { return _instance ??= new QuartzSchedulerManager(); } }
        private readonly string ConfigFile;
        private string? _configurationLoadError;
        private string? _configurationRevision;
       
        public ObservableCollection<SchedulerInfo> TaskInfos { get; set; } = new ObservableCollection<SchedulerInfo>();

        public IScheduler Scheduler { get; set; }
        public TaskExecutionListener Listener { get; set; }
        public Dictionary<string, Type> Jobs { get; set; }
        public RelayCommand PauseAllCommand { get; set; }
        public RelayCommand ResumeAllCommand { get; set; }
        public RelayCommand StartCommand { get; set; }
        public RelayCommand ShutdownCommand { get; set; }
        public Task InitializationTask { get; }

        internal event Action? ActiveExecutionsChanged;

        internal void NotifyActiveExecutionsChanged()
        {
            try
            {
                ActiveExecutionsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.Error("An active execution state subscriber failed.", ex);
            }
        }

        public QuartzSchedulerManager()
        {
            ConfigFile = Path.Combine(Environments.DirStateScheduler, "scheduler_tasks.json");
            Load();
            RestoreStatsFromDb();
            EnsureCopilotContextRegistered();
            InitializationTask = Start();
        }

        internal QuartzSchedulerManager(IScheduler scheduler, string configFile)
        {
            ConfigFile = configFile;
            Scheduler = scheduler;
            Listener = new TaskExecutionListener(this);
            Jobs = new Dictionary<string, Type>();
            InitializationTask = Task.CompletedTask;
        }

        internal CopilotSchedulerContextSnapshot CaptureCopilotSchedulerSnapshot(
            string surface = "Scheduler overview",
            SchedulerInfo? selectedTask = null,
            int selectedTaskCount = 0,
            IReadOnlyList<JobExecutionRecord>? historyRecords = null,
            int historyPageIndex = 0,
            string historyFilter = "",
            string historyScope = "",
            string historyTaskName = "",
            string historyGroupName = "",
            JobExecutionRecord? selectedHistoryRecord = null)
        {
            var tasks = TaskInfos.ToArray();
            historyRecords ??= Array.Empty<JobExecutionRecord>();
            var taskSnapshots = tasks
                .OrderBy(task => task.Status == SchedulerStatus.Running ? 0 : task.FailureCount > 0 ? 1 : task.Status == SchedulerStatus.Paused ? 2 : 3)
                .ThenByDescending(task => task.FailureCount)
                .ThenBy(task => task.JobName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(task => task.GroupName, StringComparer.CurrentCultureIgnoreCase)
                .Take(MaxCopilotTaskSnapshots)
                .Select(task => new CopilotSchedulerTaskContextSnapshot
                {
                    TaskName = task.JobName,
                    GroupName = task.GroupName,
                    Status = task.Status.ToString(),
                    JobType = task.JobType?.Name ?? string.Empty,
                    ExecutionMode = task.Mode.ToString(),
                    Priority = task.Priority,
                    RunCount = task.RunCount,
                    SuccessCount = task.SuccessCount,
                    FailureCount = task.FailureCount,
                    LastExecutionTimeMilliseconds = task.LastExecutionTimeMs,
                    LastExecutionResult = task.LastExecutionResult,
                    HasLastExecutionMessage = !string.IsNullOrWhiteSpace(task.LastExecutionMessage),
                    NextFireTime = task.NextFireTime,
                })
                .ToArray();
            var schedulerState = Scheduler?.Status switch
            {
                null => "Initializing",
                Quartz.SchedulerStatus.Created => "Ready",
                Quartz.SchedulerStatus.Running => "Started",
                Quartz.SchedulerStatus.Standby => "Standby",
                Quartz.SchedulerStatus.ShuttingDown => "ShuttingDown",
                Quartz.SchedulerStatus.Shutdown => "Shutdown",
                _ => "Unknown"
            };

            return new CopilotSchedulerContextSnapshot
            {
                SourceId = CopilotSchedulerAgentExtension.SourceId,
                Surface = surface,
                SchedulerState = schedulerState,
                TotalTaskCount = tasks.Length,
                ReadyTaskCount = tasks.Count(task => task.Status == SchedulerStatus.Ready),
                RunningTaskCount = tasks.Count(task => task.Status == SchedulerStatus.Running),
                PausedTaskCount = tasks.Count(task => task.Status == SchedulerStatus.Paused),
                TotalRunCount = tasks.Sum(task => task.RunCount),
                TotalSuccessCount = tasks.Sum(task => task.SuccessCount),
                TotalFailureCount = tasks.Sum(task => task.FailureCount),
                IsTaskListTruncated = tasks.Length > taskSnapshots.Length,
                Tasks = taskSnapshots,
                SelectedTaskCount = selectedTaskCount,
                HasSelectedTask = selectedTask != null,
                SelectedTaskName = selectedTask?.JobName ?? string.Empty,
                SelectedGroupName = selectedTask?.GroupName ?? string.Empty,
                SelectedTaskStatus = selectedTask?.Status.ToString() ?? string.Empty,
                SelectedJobType = selectedTask?.JobType?.Name ?? string.Empty,
                SelectedExecutionMode = selectedTask?.Mode.ToString() ?? string.Empty,
                SelectedRepeatMode = selectedTask?.RepeatMode.ToString() ?? string.Empty,
                SelectedPriority = selectedTask?.Priority ?? 0,
                SelectedTimeoutSeconds = selectedTask?.TimeoutSeconds ?? 0,
                SelectedRunCount = selectedTask?.RunCount ?? 0,
                SelectedSuccessCount = selectedTask?.SuccessCount ?? 0,
                SelectedFailureCount = selectedTask?.FailureCount ?? 0,
                SelectedLastExecutionTimeMilliseconds = selectedTask?.LastExecutionTimeMs ?? 0,
                SelectedAverageExecutionTimeMilliseconds = selectedTask?.AverageExecutionTimeMs ?? 0,
                SelectedLastExecutionResult = selectedTask?.LastExecutionResult ?? string.Empty,
                SelectedHasLastExecutionMessage = !string.IsNullOrWhiteSpace(selectedTask?.LastExecutionMessage),
                SelectedNextFireTime = selectedTask?.NextFireTime ?? string.Empty,
                SelectedPreviousFireTime = selectedTask?.PreviousFireTime ?? string.Empty,
                SelectedCreatedAt = selectedTask?.CreateTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                SelectedHasConfiguration = selectedTask?.Config != null,
                SelectedHasCronExpression = selectedTask?.Mode == JobExecutionMode.Cron && !string.IsNullOrWhiteSpace(selectedTask.CronExpression),
                HasLoadedHistory = historyPageIndex > 0,
                HistoryScope = historyScope,
                HistoryTaskName = historyTaskName,
                HistoryGroupName = historyGroupName,
                HistoryPageIndex = historyPageIndex,
                HistoryFilter = historyFilter,
                LoadedHistoryCount = historyRecords.Count,
                LoadedHistorySuccessCount = historyRecords.Count(record => record.Success),
                LoadedHistoryFailureCount = historyRecords.Count(record => !record.Success),
                LoadedHistoryAverageExecutionTimeMilliseconds = historyRecords.Count > 0
                    ? (long)historyRecords.Average(record => record.ExecutionTimeMs)
                    : 0,
                HasSelectedHistoryRecord = selectedHistoryRecord != null,
                SelectedHistoryRecordId = selectedHistoryRecord?.Id,
                SelectedHistoryTaskName = selectedHistoryRecord?.JobName ?? string.Empty,
                SelectedHistoryGroupName = selectedHistoryRecord?.GroupName ?? string.Empty,
                SelectedHistoryStartTime = selectedHistoryRecord?.StartTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                SelectedHistoryEndTime = selectedHistoryRecord?.EndTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                SelectedHistoryExecutionTimeMilliseconds = selectedHistoryRecord?.ExecutionTimeMs ?? 0,
                SelectedHistorySucceeded = selectedHistoryRecord?.Success == true,
                SelectedHistoryResult = selectedHistoryRecord?.Result ?? string.Empty,
                SelectedHistoryHasMessage = !string.IsNullOrWhiteSpace(selectedHistoryRecord?.Message),
            };
        }

        private async Task<CopilotSchedulerContextSnapshot?> CaptureCopilotSchedulerSnapshotAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return await dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CaptureCopilotSchedulerSnapshot();
                });
            }

            return CaptureCopilotSchedulerSnapshot();
        }

        private void EnsureCopilotContextRegistered()
        {
            if (_copilotContextSession != null)
                return;

            try
            {
                _copilotContextSession = CopilotSchedulerContextHub.Shared.Register(
                    CaptureCopilotSchedulerSnapshotAsync,
                    GetType().Assembly.GetName().Version?.ToString());
            }
            catch (Exception ex)
            {
                _logger.Warn("Could not register the Task Scheduler Copilot Agent extension.", ex);
            }
        }

        public void Save()
        {
            if (!TryPersistTasks(out string errorMessage))
            {
                MessageBox.Show(errorMessage, Properties.Resources.Sched_Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryPersistTasks(out string errorMessage)
        {
            if (_configurationLoadError is { } loadError)
            {
                errorMessage = loadError;
                return false;
            }
            try
            {
                _logger.Debug($"Saving {TaskInfos.Count} tasks to {ConfigFile}");
                _configurationRevision = SchedulerTaskSerializer.SaveLoadedFile(ConfigFile, TaskInfos, _configurationRevision);
                _logger.Info("Tasks saved successfully");
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to save tasks", ex);
                errorMessage = string.Format(Properties.Resources.Sched_SaveFailed, ex.Message);
                return false;
            }
        }

        public void Load()
        {
            string? notification = LoadTaskDefinitions();
            if (notification != null)
            {
                string title = _configurationLoadError != null ? Properties.Resources.Sched_Error : Properties.Resources.Sched_RestoreWarningTitle;
                MessageBox.Show(notification, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        internal string? LoadTaskDefinitions()
        {
            try
            {
                var (tasks, revision, restoredBackup) = SchedulerTaskSerializer.LoadForStartup(ConfigFile);
                _configurationLoadError = null;
                // A missing-file reload leaves the current tasks in memory. Keep their
                // revision so they cannot recreate a file that was removed externally.
                if (tasks == null) return null;
                _configurationRevision = revision;

                var notifications = new List<string>();
                if (restoredBackup)
                {
                    string message = string.Format(Properties.Resources.Sched_TaskBackupRestored, ConfigFile + ".bak");
                    _logger.Warn(message);
                    notifications.Add(message);
                }

                bool definitionVersionUpgraded = false;
                List<string> pausedLegacyIntervalTasks = [];
                foreach (var item in tasks)
                {
                    bool isLegacyDefinition = item.ScheduleDefinitionVersion < SchedulerInfo.CurrentScheduleDefinitionVersion;
                    // Older Interval/Forever tasks could run daily. Require explicit resume before using their configured interval.
                    if (isLegacyDefinition && item.Mode == JobExecutionMode.Interval && item.RepeatMode == JobRepeatMode.Forever)
                        pausedLegacyIntervalTasks.Add($"{item.JobName} ({item.GroupName})");

                    SchedulerScheduleState.Restore(item);
                    if (isLegacyDefinition)
                    {
                        item.ScheduleDefinitionVersion = SchedulerInfo.CurrentScheduleDefinitionVersion;
                        definitionVersionUpgraded = true;
                    }
                }

                TaskInfos.Clear();
                foreach (var item in tasks) TaskInfos.Add(item);

                // Preserve the recovered backup until an ordinary task save takes place.
                if (definitionVersionUpgraded && !restoredBackup && !TryPersistTasks(out string persistenceError))
                    notifications.Add(persistenceError);
                if (pausedLegacyIntervalTasks.Count > 0)
                {
                    string taskNames = string.Join(Environment.NewLine, pausedLegacyIntervalTasks);
                    _logger.Warn($"Paused legacy Interval/Forever tasks after schedule migration:{Environment.NewLine}{taskNames}");
                    notifications.Add(string.Format(Properties.Resources.Sched_LegacyIntervalForeverPaused, taskNames));
                }
                return notifications.Count == 0 ? null : string.Join(Environment.NewLine + Environment.NewLine, notifications);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load tasks", ex);
                _configurationLoadError = string.Format(Properties.Resources.Sched_LoadFailed, ex.Message);
                return _configurationLoadError;
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
            using IDisposable mutation = await EnterMutationAsync();
            var errors = new List<string>();
            bool save = false;
            foreach (var item in TaskInfos.ToArray())
            {
                try
                {
                    if (await PauseJobCore(item.JobName, item.GroupName, item)) save = true;
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to pause job: {item.JobName}({item.GroupName})", ex);
                    errors.Add($"{item.JobName} ({item.GroupName}) {ex.Message}");
                }
            }

            if (save && !TryPersistTasks(out string persistenceError))
                errors.Add(string.Format(Properties.Resources.Sched_PauseSaveFailed, persistenceError));
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        public async Task ResumeAll()
        {
            // Each task uses the same checked resume path and persistence
            // compensation as the individual button. Never rearm running jobs.
            var errors = new List<string>();
            foreach (var item in TaskInfos.ToArray())
            {
                if (item.Status != SchedulerStatus.Paused || item.ScheduleCompleted)
                    continue;

                try
                {
                    await ResumeJob(item.JobName, item.GroupName);
                }
                catch (Exception ex)
                {
                    errors.Add($"{item.JobName} ({item.GroupName}) {ex.Message}");
                }
            }

            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        public async Task Start()
        {
            try
            {
                _schedulerFactory = QuartzSchedulerBuilder.Create(builder =>
                {
                    builder.ConfigureScheduler(options => options.InstanceName = "DefaultQuartzScheduler");
                    builder.UseDefaultThreadPool(options => options.MaxConcurrency = 10);
                    builder.UseInMemoryStore(options => options.MisfireThreshold = TimeSpan.FromSeconds(60));
                }).Build();
                Scheduler = await _schedulerFactory.GetScheduler();
                PauseAllCommand = new RelayCommand(async _ =>
                {
                    try
                    {
                        await PauseAll();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Failed to pause all scheduler jobs.", ex);
                        MessageBox.Show(ex.Message, Properties.Resources.Sched_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }, _ => Scheduler.Status is Quartz.SchedulerStatus.Running);
                ResumeAllCommand = new RelayCommand(async _ =>
                {
                    try
                    {
                        await ResumeAll();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Failed to resume all scheduler jobs.", ex);
                        MessageBox.Show(ex.Message, Properties.Resources.Sched_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }, _ => Scheduler.Status is Quartz.SchedulerStatus.Running);
                StartCommand = new RelayCommand(
                    async _ => await StartScheduler(),
                    _ => Scheduler?.Status is Quartz.SchedulerStatus.Created);
                ShutdownCommand = new RelayCommand(
                    async _ => await Shutdown(),
                    _ => Scheduler?.Status is Quartz.SchedulerStatus.Running or Quartz.SchedulerStatus.Standby);

                Listener = new TaskExecutionListener(this);
                Listener.AttachTo(Scheduler.ListenerManager);
                Jobs = new Dictionary<string, Type>();

                _logger.Debug("Discovering job types from assemblies");
                foreach (var type in AssemblyService.Instance.GetImplementationTypes(typeof(IJob)))
                {
                    string name = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name;
                    Jobs[name] = type;
                }
                var failedJobs = new List<string>();
                foreach (var item in TaskInfos)
                {
                    try
                    {
                        if (item.JobType != null)
                        {
                            await RestoreJob(item);
                            _logger.Debug($"Recovered paused task: {item.JobName}({item.GroupName})");
                        }
                        else
                        {
                            var errorMsg = $"{item.JobName}({item.GroupName}): job type is missing";
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
                    MessageBox.Show(string.Format(Properties.Resources.Sched_RestoreWarning, string.Join("\n", failedJobs)), Properties.Resources.Sched_RestoreWarningTitle);
                }
                // Startup registers durable jobs only. Resume explicitly arms
                // their remaining schedules; completed schedules stay unarmed.
                await Scheduler.Start();
                _logger.Info($"Scheduler initialization completed. Tasks={TaskInfos.Count}, JobTypes={Jobs.Count}, RecoveryFailures={failedJobs.Count}.");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to start scheduler", ex);
                MessageBox.Show(string.Format(Properties.Resources.Sched_StartFailed, ex.Message), Properties.Resources.Sched_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }
        public async Task StopJob(string jobName, string groupName)
        {
            using IDisposable mutation = await EnterMutationAsync();
            try
            {
                SchedulerInfo? info = TaskInfos.FirstOrDefault(x => x.JobName == jobName && x.GroupName == groupName);
                if (await PauseJobCore(jobName, groupName, info) && !TryPersistTasks(out string persistenceError))
                    throw new InvalidOperationException(string.Format(Properties.Resources.Sched_PauseSaveFailed, persistenceError));
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to stop job: {jobName}({groupName})", ex);
                throw;
            }
        }

        private async Task<bool> PauseJobCore(string jobName, string groupName, SchedulerInfo? info)
        {
            if (info?.Status == SchedulerStatus.Completed) return false;

            var jobKey = new JobKey(jobName, groupName);
            if (!await Scheduler.Exists(jobKey))
            {
                _logger.Warn($"Job not found: {jobName}({groupName})");
                return false;
            }

            await Scheduler.PauseJob(jobKey);
            if (info != null) info.Status = SchedulerStatus.Paused;
            _logger.Info($"Job paused: {jobName}({groupName})");
            return info != null;
        }

        public async Task RemoveJob(string jobName, string groupName)
        {
            using IDisposable mutation = await EnterMutationAsync();
            try
            {
                await using SchedulerStandbyLease standbyScope =
                    await SchedulerStandbyLease.AcquireAsync(Scheduler);
                _logger.Info($"Removing job: {jobName}({groupName})");
                JobKey jobKey = new JobKey(jobName, groupName);
                SchedulerInfo? info = TaskInfos.FirstOrDefault(x => x.JobName == jobName && x.GroupName == groupName);
                int originalIndex = info == null ? -1 : TaskInfos.IndexOf(info);
                IJobDetail? originalJob = await Scheduler.GetJobDetail(jobKey);
                IReadOnlyCollection<ITrigger> originalTriggers = originalJob == null
                    ? Array.Empty<ITrigger>()
                    : await Scheduler.GetTriggersOfJob(jobKey);

                if (await Scheduler.Exists(jobKey))
                {
                    await Scheduler.DeleteJob(jobKey);
                }
                if (info != null)
                {
                    TaskInfos.Remove(info);
                    if (!TryPersistTasks(out string persistenceError))
                    {
                        TaskInfos.Insert(Math.Clamp(originalIndex, 0, TaskInfos.Count), info);
                        bool rollbackSucceeded = await TryRestorePreviousJob(
                            Scheduler,
                            jobKey,
                            originalJob,
                            originalTriggers,
                            info.Status == SchedulerStatus.Paused);
                        if (!rollbackSucceeded)
                        {
                            persistenceError += " The previous Quartz schedule could not be fully restored; see the log for details.";
                        }

                        throw new InvalidOperationException(persistenceError);
                    }
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
            using IDisposable mutation = await EnterMutationAsync();
            await ResumeJobCore(jobName, groupName);
        }

        public async Task ExecuteJob(string jobName, string groupName)
        {
            using IDisposable mutation = await EnterMutationAsync();
            SchedulerInfo info = TaskInfos.FirstOrDefault(task => task.JobName == jobName && task.GroupName == groupName)
                ?? throw new InvalidOperationException(Properties.Resources.Sched_TaskNotFound);
            if (info.Status is SchedulerStatus.Ready or SchedulerStatus.Running)
                throw new InvalidOperationException(Properties.Resources.Sched_AlreadyActive);

            PagedResult<FireInstance> runningJobs = await Scheduler.QueryFireInstances(
                new FireInstanceQuery { Job = new JobKey(jobName, groupName) });
            if (runningJobs.Items.Count > 0)
                throw new InvalidOperationException(Properties.Resources.Sched_AlreadyActive);

            if (!info.ScheduleCompleted)
            {
                await ResumeJobCore(jobName, groupName);
                return;
            }

            // Starting another round uses the same validated replacement and
            // rollback path as editing, without modifying the saved settings.
            SchedulerInfo nextRound = SchedulerTaskSerializer.Deserialize(SchedulerTaskSerializer.Serialize([info])).Single();
            SchedulerOperationResult result = await UpdateJobCore(nextRound, jobName, groupName);
            if (!result.Success)
                throw new InvalidOperationException(result.Message);
        }

        private async Task ResumeJobCore(string jobName, string groupName)
        {
            try
            {
                _logger.Info($"Resuming job: {jobName}({groupName})");
                JobKey jobKey = new JobKey(jobName, groupName);
                SchedulerInfo info = TaskInfos.FirstOrDefault(x => x.JobName == jobName && x.GroupName == groupName)
                    ?? throw new InvalidOperationException(Properties.Resources.Sched_TaskNotFound);
                if (info.ScheduleCompleted)
                    throw new InvalidOperationException(Properties.Resources.Sched_ScheduleFinished);
                if (info.Status != SchedulerStatus.Paused)
                    return;

                SchedulerTriggerBuildResult triggerResult = SchedulerScheduleState.BuildRemaining(info);
                if (!triggerResult.Success)
                    throw new InvalidOperationException(triggerResult.ErrorMessage);
                SchedulerStatus previousStatus = info.Status;
                await using SchedulerStandbyLease standbyScope =
                    await SchedulerStandbyLease.AcquireAsync(Scheduler);
                // Quartz still lists vetoed firings while their cleanup runs.
                if (Listener?.HasActiveExecution(jobKey) == true)
                    throw new InvalidOperationException(Properties.Resources.Sched_AlreadyActive);
                IJobDetail? originalJob = await Scheduler.GetJobDetail(jobKey);
                IReadOnlyCollection<ITrigger> originalTriggers = originalJob == null
                    ? Array.Empty<ITrigger>()
                    : await Scheduler.GetTriggersOfJob(jobKey);
                bool previousCheckpoint = info.HasScheduleCheckpoint;
                DateTimeOffset? previousNext = info.NextScheduledFireTimeUtc;
                string previousNextDisplay = info.NextFireTime;
                try
                {
                    ITrigger trigger = triggerResult.Trigger!;
                    await Scheduler.ScheduleJob(BuildJobDetail(info), [trigger], ScheduleJobOptions.Replacing);
                    await Scheduler.ResumeJob(jobKey);
                    info.HasScheduleCheckpoint = true;
                    info.NextScheduledFireTimeUtc = trigger.NextFireTimeUtc;
                    UpdateNextFireTime(info, trigger);
                    info.Status = SchedulerStatus.Ready;
                    if (!TryPersistTasks(out string persistenceError))
                        throw new InvalidOperationException(persistenceError);
                    info.ScheduleAdmissionError = null;
                }
                catch
                {
                    info.Status = previousStatus;
                    info.HasScheduleCheckpoint = previousCheckpoint;
                    info.NextScheduledFireTimeUtc = previousNext;
                    info.NextFireTime = previousNextDisplay;
                    await TryRestorePreviousJob(Scheduler, jobKey, originalJob, originalTriggers, true);
                    throw;
                }
                _logger.Info($"Job resumed: {jobName}({groupName})");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to resume job: {jobName}({groupName})", ex);
                throw;
            }
        }

        public async Task<SchedulerOperationResult> CreateJob(SchedulerInfo schedulerInfo)
        {
            ArgumentNullException.ThrowIfNull(schedulerInfo);
            using IDisposable mutation = await EnterMutationAsync();
            _logger.Info($"Creating job: {schedulerInfo.JobName}({schedulerInfo.GroupName})");

            SchedulerTriggerBuildResult triggerResult = SchedulerTriggerFactory.Build(schedulerInfo);
            if (!triggerResult.Success)
            {
                _logger.Warn($"Job validation failed: {triggerResult.ErrorMessage}");
                return SchedulerOperationResult.Failed(SchedulerOperationError.Validation, triggerResult.ErrorMessage);
            }

            var scheduler = Scheduler;
            if (scheduler == null)
            {
                _logger.Error("Scheduler is null");
                return SchedulerOperationResult.Failed(SchedulerOperationError.SchedulerUnavailable, Properties.Resources.Sched_NotInit);
            }

            SchedulerInfo? conflictingInfo = TaskInfos.FirstOrDefault(info =>
                info.JobName == schedulerInfo.JobName
                && info.GroupName == schedulerInfo.GroupName
                && !ReferenceEquals(info, schedulerInfo));
            if (conflictingInfo != null)
            {
                string message = $"Task '{schedulerInfo.JobName}({schedulerInfo.GroupName})' already exists.";
                _logger.Warn(message);
                return SchedulerOperationResult.Failed(SchedulerOperationError.Conflict, message);
            }

            SchedulerStandbyLease standbyLease;
            try
            {
                standbyLease = await SchedulerStandbyLease.AcquireAsync(scheduler);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to place the scheduler in standby before creating a task.", ex);
                return SchedulerOperationResult.Failed(
                    SchedulerOperationError.QuartzFailure,
                    string.Format(Properties.Resources.Sched_CreateTaskFailed, ex.Message));
            }
            await using SchedulerStandbyLease standbyScope = standbyLease;

            bool scheduled = false;
            try
            {
                IJobDetail job = BuildJobDetail(schedulerInfo);
                ITrigger trigger = triggerResult.Trigger!;
                DateTimeOffset firstFireTimeUtc = await scheduler.ScheduleJob(job, trigger);
                scheduled = true;
                if (schedulerInfo.Status == SchedulerStatus.Paused)
                    await scheduler.PauseJob(job.Key);
                UpdateNextFireTime(schedulerInfo, trigger);
                SchedulerScheduleState.Reset(schedulerInfo, trigger);
                if (!TaskInfos.Contains(schedulerInfo))
                {
                    TaskInfos.Add(schedulerInfo);
                    if (!TryPersistTasks(out string persistenceError))
                    {
                        TaskInfos.Remove(schedulerInfo);
                        await TryRollbackNewJob(scheduler, job.Key);
                        return SchedulerOperationResult.Failed(
                            SchedulerOperationError.PersistenceFailure,
                            persistenceError);
                    }
                }
                _logger.Info($"Job created successfully: {schedulerInfo.JobName}({schedulerInfo.GroupName})");
                return SchedulerOperationResult.Completed(firstFireTimeUtc);
            }
            catch (ObjectAlreadyExistsException ex)
            {
                _logger.Warn($"Job already exists: {schedulerInfo.JobName}({schedulerInfo.GroupName})", ex);
                return SchedulerOperationResult.Failed(SchedulerOperationError.Conflict, ex.Message);
            }
            catch (Exception ex)
            {
                if (scheduled)
                    await TryRollbackNewJob(scheduler, new JobKey(schedulerInfo.JobName, schedulerInfo.GroupName));
                _logger.Error($"Failed to create job: {schedulerInfo.JobName}({schedulerInfo.GroupName})", ex);
                return SchedulerOperationResult.Failed(
                    SchedulerOperationError.QuartzFailure,
                    string.Format(Properties.Resources.Sched_CreateTaskFailed, ex.Message));
            }
        }

        public Task<SchedulerOperationResult> UpdateJob(SchedulerInfo schedulerInfo)
        {
            ArgumentNullException.ThrowIfNull(schedulerInfo);
            return UpdateJob(schedulerInfo, schedulerInfo.JobName, schedulerInfo.GroupName);
        }

        public async Task<SchedulerOperationResult> UpdateJob(
            SchedulerInfo schedulerInfo,
            string originalJobName,
            string originalGroupName)
        {
            ArgumentNullException.ThrowIfNull(schedulerInfo);
            using IDisposable mutation = await EnterMutationAsync();
            return await UpdateJobCore(schedulerInfo, originalJobName, originalGroupName);
        }

        private async Task<SchedulerOperationResult> UpdateJobCore(
            SchedulerInfo schedulerInfo, string originalJobName, string originalGroupName)
        {
            if (string.IsNullOrWhiteSpace(originalJobName) || string.IsNullOrWhiteSpace(originalGroupName))
                return SchedulerOperationResult.Failed(SchedulerOperationError.Validation, Properties.Resources.Sched_NameEmpty);

            _logger.Info(
                $"Updating job: {originalJobName}({originalGroupName}) -> " +
                $"{schedulerInfo.JobName}({schedulerInfo.GroupName})");

            SchedulerTriggerBuildResult triggerResult = SchedulerTriggerFactory.Build(schedulerInfo);
            if (!triggerResult.Success)
            {
                _logger.Warn($"Job validation failed before update: {triggerResult.ErrorMessage}");
                return SchedulerOperationResult.Failed(SchedulerOperationError.Validation, triggerResult.ErrorMessage);
            }

            var scheduler = Scheduler;
            if (scheduler == null)
                return SchedulerOperationResult.Failed(SchedulerOperationError.SchedulerUnavailable, Properties.Resources.Sched_NotInit);

            SchedulerInfo? originalInfo = TaskInfos.FirstOrDefault(info =>
                info.JobName == originalJobName && info.GroupName == originalGroupName);
            if (originalInfo == null)
            {
                string message = $"Task '{originalJobName}({originalGroupName})' was not found.";
                _logger.Warn(message);
                return SchedulerOperationResult.Failed(SchedulerOperationError.NotFound, message);
            }

            SchedulerStandbyLease standbyLease;
            try
            {
                standbyLease = await SchedulerStandbyLease.AcquireAsync(scheduler);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to place the scheduler in standby before updating a task.", ex);
                return SchedulerOperationResult.Failed(
                    SchedulerOperationError.QuartzFailure,
                    string.Format(Properties.Resources.Sched_UpdateTaskFailed, ex.Message));
            }
            await using SchedulerStandbyLease standbyScope = standbyLease;

            var originalJobKey = new JobKey(originalJobName, originalGroupName);
            var updatedJobKey = new JobKey(schedulerInfo.JobName, schedulerInfo.GroupName);
            bool identityChanged = originalJobKey != updatedJobKey;
            try
            {
                PagedResult<FireInstance> runningJobs = await scheduler.QueryFireInstances(
                    new FireInstanceQuery { Job = originalJobKey });
                if (runningJobs.Items.Count > 0)
                {
                    return SchedulerOperationResult.Failed(
                        SchedulerOperationError.Conflict,
                        Properties.Resources.Sched_UpdateRunning);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to inspect running jobs before updating: {originalJobKey}", ex);
                return SchedulerOperationResult.Failed(
                    SchedulerOperationError.QuartzFailure,
                    string.Format(Properties.Resources.Sched_UpdateTaskFailed, ex.Message));
            }

            if (identityChanged)
            {
                bool definitionConflict = TaskInfos.Any(info =>
                    !ReferenceEquals(info, originalInfo)
                    && info.JobName == schedulerInfo.JobName
                    && info.GroupName == schedulerInfo.GroupName);
                bool schedulerConflict;
                try
                {
                    schedulerConflict = await scheduler.Exists(updatedJobKey);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to check updated job identity: {updatedJobKey}", ex);
                    return SchedulerOperationResult.Failed(
                        SchedulerOperationError.QuartzFailure,
                        string.Format(Properties.Resources.Sched_UpdateTaskFailed, ex.Message));
                }

                if (definitionConflict || schedulerConflict)
                {
                    string message = $"Task '{schedulerInfo.JobName}({schedulerInfo.GroupName})' already exists.";
                    _logger.Warn(message);
                    return SchedulerOperationResult.Failed(SchedulerOperationError.Conflict, message);
                }
            }

            MergeRuntimeStateForUpdate(schedulerInfo, originalInfo, identityChanged);

            IJobDetail? originalJob;
            IReadOnlyCollection<ITrigger> originalTriggers;
            try
            {
                originalJob = await scheduler.GetJobDetail(originalJobKey);
                originalTriggers = originalJob == null
                    ? Array.Empty<ITrigger>()
                    : await scheduler.GetTriggersOfJob(originalJobKey);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to capture the existing schedule before updating: {originalJobKey}", ex);
                return SchedulerOperationResult.Failed(
                    SchedulerOperationError.QuartzFailure,
                    string.Format(Properties.Resources.Sched_UpdateTaskFailed, ex.Message));
            }

            IJobDetail job;
            ITrigger trigger = triggerResult.Trigger!;
            try
            {
                job = BuildJobDetail(schedulerInfo);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to build updated job: {schedulerInfo.JobName}({schedulerInfo.GroupName})", ex);
                return SchedulerOperationResult.Failed(SchedulerOperationError.Validation, ex.Message);
            }

            bool scheduleMutated = false;
            try
            {
                if (identityChanged)
                {
                    await scheduler.ScheduleJob(job, trigger);
                    scheduleMutated = true;
                    try
                    {
                        await scheduler.DeleteJob(originalJobKey);
                    }
                    catch
                    {
                        await TryRollbackNewJob(scheduler, updatedJobKey);
                        throw;
                    }
                }
                else
                {
                    await scheduler.ScheduleJob(job, [trigger], ScheduleJobOptions.Replacing);
                    scheduleMutated = true;
                }

                if (schedulerInfo.Status == SchedulerStatus.Paused)
                    await scheduler.PauseJob(updatedJobKey);

                int originalIndex = TaskInfos.IndexOf(originalInfo);
                if (originalIndex >= 0)
                    TaskInfos[originalIndex] = schedulerInfo;
                UpdateNextFireTime(schedulerInfo, trigger);
                SchedulerScheduleState.Reset(schedulerInfo, trigger);
                if (!TryPersistTasks(out string persistenceError))
                {
                    if (originalIndex >= 0)
                        TaskInfos[originalIndex] = originalInfo;

                    bool rollbackSucceeded = await TryRestorePreviousJob(
                        scheduler,
                        updatedJobKey,
                        originalJob,
                        originalTriggers,
                        originalInfo.Status == SchedulerStatus.Paused);
                    if (!rollbackSucceeded)
                    {
                        persistenceError += " The previous Quartz schedule could not be fully restored; see the log for details.";
                    }

                    return SchedulerOperationResult.Failed(
                        SchedulerOperationError.PersistenceFailure,
                        persistenceError);
                }
                _logger.Info($"Job updated successfully: {schedulerInfo.JobName}({schedulerInfo.GroupName})");
                return SchedulerOperationResult.Completed(trigger.NextFireTimeUtc);
            }
            catch (ObjectAlreadyExistsException ex)
            {
                _logger.Warn($"Updated job conflicts with an existing task: {schedulerInfo.JobName}({schedulerInfo.GroupName})", ex);
                return SchedulerOperationResult.Failed(SchedulerOperationError.Conflict, ex.Message);
            }
            catch (Exception ex)
            {
                if (scheduleMutated)
                {
                    await TryRestorePreviousJob(
                        scheduler,
                        updatedJobKey,
                        originalJob,
                        originalTriggers,
                        originalInfo.Status == SchedulerStatus.Paused);
                }
                _logger.Error($"Failed to update job: {schedulerInfo.JobName}({schedulerInfo.GroupName})", ex);
                return SchedulerOperationResult.Failed(
                    SchedulerOperationError.QuartzFailure,
                    string.Format(Properties.Resources.Sched_UpdateTaskFailed, ex.Message));
            }
        }

        private static IJobDetail BuildJobDetail(SchedulerInfo schedulerInfo)
        {
            IJobDetail job = JobBuilder.Create()
                .OfType(schedulerInfo.JobType!)
                .WithIdentity(schedulerInfo.JobName, schedulerInfo.GroupName)
                .StoreDurably()
                .Build();
            job.JobDataMap["SchedulerInfo"] = schedulerInfo;
            return job;
        }

        internal async Task RestoreJob(SchedulerInfo info)
        {
            using IDisposable mutation = await EnterMutationAsync();
            string? error = SchedulerTriggerFactory.Validate(info);
            if (error != null)
                throw new InvalidOperationException(error);
            SchedulerScheduleState.Restore(info);
            await Scheduler.AddJob(BuildJobDetail(info), AddJobOptions.Replacing);
        }

        internal async Task RecordExecutionAsync(IJobExecutionContext context, Action<SchedulerInfo> update)
        {
            using IDisposable mutation = await EnterMutationAsync();
            void Apply()
            {
                SchedulerInfo? info = GetCurrentJobDefinition(context);
                if (info == null)
                    return;
                update(info);
                if (!TryPersistTasks(out string error))
                    throw new InvalidOperationException(error);
            }
            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
                await dispatcher.InvokeAsync(Apply);
            else
                Apply();
        }

        internal async Task<bool> TryCheckpointScheduledFireAsync(IJobExecutionContext context)
        {
            using IDisposable mutation = await EnterMutationAsync();
            bool pauseSchedule = false;
            bool Checkpoint()
            {
                SchedulerInfo? info = GetCurrentJobDefinition(context);
                if (info == null)
                    return false;
                // Quartz reenters admission for an already checkpointed firing.
                // Pausing applies to subsequent firings.
                if (context.RefireCount > 0)
                    return true;
                if (info.Status == SchedulerStatus.Paused)
                    return false;

                var previous = (info.HasScheduleCheckpoint, info.ScheduledFireCount, info.ScheduleCompleted,
                    info.NextScheduledFireTimeUtc, info.NextFireTime, info.PreviousFireTime);
                SchedulerScheduleState.RecordFire(info, context);
                info.Status = SchedulerStatus.Running;
                if (TryPersistTasks(out string error))
                    return true;

                // Quartz has advanced the trigger, but no business ran. Retain the
                // saved progress so Resume can rebuild the unconsumed schedule.
                (info.HasScheduleCheckpoint, info.ScheduledFireCount, info.ScheduleCompleted,
                    info.NextScheduledFireTimeUtc, info.NextFireTime, info.PreviousFireTime) = previous;
                info.ScheduleAdmissionError = error;
                info.Status = SchedulerStatus.Paused;
                pauseSchedule = true;
                return false;
            }

            bool admitted = Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess()
                ? await dispatcher.InvokeAsync(Checkpoint)
                : Checkpoint();
            if (pauseSchedule)
                await Scheduler.PauseJob(context.JobDetail.Key);
            return admitted;
        }

        private SchedulerInfo? GetCurrentJobDefinition(IJobExecutionContext context)
        {
            if (!context.JobDetail.JobDataMap.TryGetValue("SchedulerInfo", out object? definition))
                return null;

            JobKey key = context.JobDetail.Key;
            // A removed/replaced definition must not inherit an older fire's checkpoint.
            return TaskInfos.FirstOrDefault(info => ReferenceEquals(info, definition)
                && info.JobName == key.Name && info.GroupName == key.Group);
        }

        private static void UpdateNextFireTime(SchedulerInfo schedulerInfo, ITrigger trigger)
        {
            schedulerInfo.NextFireTime =
                trigger.NextFireTimeUtc?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A";
        }

        private static void MergeRuntimeStateForUpdate(
            SchedulerInfo updatedInfo,
            SchedulerInfo originalInfo,
            bool identityChanged)
        {
            updatedInfo.Status = originalInfo.Status == SchedulerStatus.Paused
                ? SchedulerStatus.Paused
                : SchedulerStatus.Ready;

            if (identityChanged)
            {
                // Execution history is keyed by task/group identity. Treat a
                // rename as a new runtime identity so the list aggregates do
                // not claim to represent history stored under the old key.
                updatedInfo.RunCount = 0;
                updatedInfo.SuccessCount = 0;
                updatedInfo.FailureCount = 0;
                updatedInfo.LastExecutionTimeMs = 0;
                updatedInfo.AverageExecutionTimeMs = 0;
                updatedInfo.MinExecutionTimeMs = 0;
                updatedInfo.MaxExecutionTimeMs = 0;
                updatedInfo.LastExecutionResult = string.Empty;
                updatedInfo.LastExecutionMessage = string.Empty;
                updatedInfo.PreviousFireTime = string.Empty;
                updatedInfo.NextFireTime = string.Empty;
                updatedInfo.CreateTime = DateTime.Now;
                return;
            }

            // The edit dialog works on a clone. Merge the latest live fields
            // just before replacement so executions completed while the dialog
            // was open are not overwritten by the stale clone.
            updatedInfo.RunCount = originalInfo.RunCount;
            updatedInfo.SuccessCount = originalInfo.SuccessCount;
            updatedInfo.FailureCount = originalInfo.FailureCount;
            updatedInfo.LastExecutionTimeMs = originalInfo.LastExecutionTimeMs;
            updatedInfo.AverageExecutionTimeMs = originalInfo.AverageExecutionTimeMs;
            updatedInfo.MinExecutionTimeMs = originalInfo.MinExecutionTimeMs;
            updatedInfo.MaxExecutionTimeMs = originalInfo.MaxExecutionTimeMs;
            updatedInfo.LastExecutionResult = originalInfo.LastExecutionResult;
            updatedInfo.LastExecutionMessage = originalInfo.LastExecutionMessage;
            updatedInfo.PreviousFireTime = originalInfo.PreviousFireTime;
            updatedInfo.CreateTime = originalInfo.CreateTime;
        }

        private static async Task TryRollbackNewJob(IScheduler scheduler, JobKey updatedJobKey)
        {
            try
            {
                await scheduler.DeleteJob(updatedJobKey);
            }
            catch (Exception rollbackException)
            {
                _logger.Error($"Failed to roll back newly scheduled job: {updatedJobKey}", rollbackException);
            }
        }

        private static async Task<bool> TryRestorePreviousJob(
            IScheduler scheduler,
            JobKey updatedJobKey,
            IJobDetail? originalJob,
            IReadOnlyCollection<ITrigger> originalTriggers,
            bool wasPaused)
        {
            try
            {
                if (originalJob == null)
                {
                    await scheduler.DeleteJob(updatedJobKey);
                    return true;
                }

                if (updatedJobKey != originalJob.Key)
                    await scheduler.DeleteJob(updatedJobKey);

                ITrigger[] pending = originalTriggers.Where(trigger => trigger.NextFireTimeUtc.HasValue).ToArray();
                var previousKeys = pending.Select(trigger => trigger.Key).ToHashSet();
                foreach (ITrigger trigger in await scheduler.GetTriggersOfJob(originalJob.Key))
                    if (!previousKeys.Contains(trigger.Key))
                        await scheduler.UnscheduleJob(trigger.Key);

                await scheduler.AddJob(originalJob, AddJobOptions.ReplacingAndStoringNonDurable);
                foreach (ITrigger trigger in pending)
                    await RestoreTrigger(scheduler, originalJob.Key, trigger, wasPaused);

                if (wasPaused)
                    await scheduler.PauseJob(originalJob.Key);

                return true;
            }
            catch (Exception rollbackException)
            {
                _logger.Error($"Failed to restore previous Quartz schedule: {originalJob?.Key}", rollbackException);
                return false;
            }
        }

        private static async Task RestoreTrigger(IScheduler scheduler, JobKey jobKey, ITrigger trigger, bool wasPaused)
        {
            bool missing = !await scheduler.Exists(trigger.Key);
            if (missing)
            {
                // RescheduleJob preserves the supplied fire times, whereas
                // registering a trigger recomputes its first fire.
                ITrigger placeholder = TriggerBuilder.Create()
                    .WithIdentity(trigger.Key)
                    .ForJob(jobKey)
                    .StartAt(trigger.NextFireTimeUtc!.Value)
                    .Build();
                await scheduler.ScheduleJob(placeholder, new ScheduleJobOptions { PauseReason = "Restoring a saved trigger" });
            }

            try
            {
                if (await scheduler.RescheduleJob(trigger.Key, trigger) == null)
                    throw new InvalidOperationException($"Could not restore Quartz trigger '{trigger.Key}'.");
                if (missing && !wasPaused)
                    await scheduler.ResumeTrigger(trigger.Key);
            }
            catch
            {
                if (missing)
                    await scheduler.UnscheduleJob(trigger.Key);
                throw;
            }
        }

        public void SaveTasks()
        {
            Save();
        }

        public void LoadTasks()
        {
            Load();
        }

        /// <summary>
        /// 从 SQLite 数据库恢复每个任务的聚合统计数据
        /// </summary>
        private void RestoreStatsFromDb()
        {
            try
            {
                var dbManager = SchedulerDbManager.GetInstance();
                foreach (var task in TaskInfos)
                {
                    var stats = dbManager.GetTaskStats(task.JobName, task.GroupName);
                    if (stats.RunCount > 0)
                    {
                        task.RunCount = stats.RunCount;
                        task.SuccessCount = stats.SuccessCount;
                        task.FailureCount = stats.FailureCount;
                        task.AverageExecutionTimeMs = stats.AvgMs;
                        task.MinExecutionTimeMs = stats.MinMs;
                        task.MaxExecutionTimeMs = stats.MaxMs;
                        task.LastExecutionResult = stats.LastResult ?? string.Empty;
                        task.LastExecutionMessage = stats.LastMessage ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to restore stats from database", ex);
            }
        }

        public async Task Shutdown()
        {
            using IDisposable mutation = await EnterMutationAsync();
            if (Scheduler != null && Scheduler.Status is not Quartz.SchedulerStatus.ShuttingDown and not Quartz.SchedulerStatus.Shutdown)
            {
                await Scheduler.Shutdown();
            }
            if (_schedulerFactory != null)
            {
                await _schedulerFactory.DisposeAsync();
                _schedulerFactory = null;
            }
        }

        private async Task StartScheduler()
        {
            using IDisposable mutation = await EnterMutationAsync();
            if (Scheduler != null && Scheduler.Status is not Quartz.SchedulerStatus.ShuttingDown and not Quartz.SchedulerStatus.Shutdown)
            {
                await Scheduler.Start();
            }
        }

        private async Task<IDisposable> EnterMutationAsync()
        {
            await _mutationGate.WaitAsync();
            return new MutationGateLease(_mutationGate);
        }

        private sealed class MutationGateLease : IDisposable
        {
            private SemaphoreSlim? _gate;

            public MutationGateLease(SemaphoreSlim gate)
            {
                _gate = gate;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _gate, null)?.Release();
            }
        }

        private sealed class SchedulerStandbyLease : IAsyncDisposable
        {
            private readonly IScheduler _scheduler;
            private readonly bool _restartOnDispose;

            private SchedulerStandbyLease(IScheduler scheduler, bool restartOnDispose)
            {
                _scheduler = scheduler;
                _restartOnDispose = restartOnDispose;
            }

            public static async Task<SchedulerStandbyLease> AcquireAsync(IScheduler scheduler)
            {
                bool restartOnDispose = scheduler.Status is Quartz.SchedulerStatus.Running;
                if (restartOnDispose)
                    await scheduler.Standby();

                return new SchedulerStandbyLease(scheduler, restartOnDispose);
            }

            public async ValueTask DisposeAsync()
            {
                if (_restartOnDispose && _scheduler.Status is not Quartz.SchedulerStatus.ShuttingDown and not Quartz.SchedulerStatus.Shutdown)
                    await _scheduler.Start();
            }
        }
    }
}

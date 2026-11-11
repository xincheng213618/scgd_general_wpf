using ColorVision.Scheduler;
using ColorVision.Scheduler.Data;
using ColorVision.Scheduler.Properties;
using Quartz;
using System.Globalization;
using System.IO;
using System.Threading.Channels;
using System.Windows.Controls;
using System.Windows.Threading;
using SchedulerStatus = ColorVision.Scheduler.SchedulerStatus;

namespace ColorVision.UI.Tests;

// These tests protect schedule exhaustion/restart boundaries using real Quartz
// and the production listener, with no host startup, device jobs or real database.
public sealed class SchedulerLifecycleTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), nameof(SchedulerLifecycleTests), Guid.NewGuid().ToString("N"));
    private readonly Channel<JobExecutionRecord> _executions = Channel.CreateUnbounded<JobExecutionRecord>();
    private StandaloneSchedulerFactory _factory = null!;
    private IScheduler _scheduler = null!;
    private QuartzSchedulerManager _manager = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        _factory = QuartzSchedulerBuilder.Create(builder =>
        {
            builder.ConfigureScheduler(options => options.InstanceName = $"lifecycle-{Guid.NewGuid():N}");
            builder.UseDefaultThreadPool(1);
            builder.UseInMemoryStore(_ => { });
        }).Build();
        _scheduler = await _factory.GetScheduler();
        _manager = new QuartzSchedulerManager(_scheduler, Path.Combine(_directory, "tasks.json"));
        _manager.Listener = new TaskExecutionListener(_manager, record => _executions.Writer.TryWrite(record));
        _manager.Listener.AttachTo(_scheduler.ListenerManager);
    }

    [Fact]
    public async Task ExhaustedTask_CanRunManuallyAgainWithoutRearmingItsSchedule()
    {
        SchedulerInfo info = CreateInfo();
        Assert.True((await _manager.CreateJob(info)).Success);
        await _scheduler.Start();
        await NextExecution();

        var key = new JobKey(info.JobName, info.GroupName);
        // Force trigger cleanup deterministically, even if Quartz has not yet
        // finished its post-listener cleanup when the completion signal arrives.
        await _scheduler.UnscheduleJob(new TriggerKey($"{info.JobName}-trigger", info.GroupName));
        Assert.True(await _scheduler.Exists(key));
        Assert.True(info.ScheduleCompleted);
        Assert.Equal(SchedulerStatus.Completed, info.Status);
        Assert.Equal(1, info.ScheduledFireCount);

        await _scheduler.TriggerJob(key);
        await NextExecution();
        await _scheduler.TriggerJob(key);
        await NextExecution();

        Assert.Equal(3, info.RunCount);
        Assert.Equal(1, info.ScheduledFireCount);
        Assert.True(info.ScheduleCompleted);
        Assert.Equal(SchedulerStatus.Completed, info.Status);
        SchedulerInfo saved = Assert.Single(SchedulerTaskSerializer.LoadFromFile(Path.Combine(_directory, "tasks.json")));
        Assert.Equal(3, saved.RunCount);
        Assert.True(saved.ScheduleCompleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restore_RegistersNoAutomaticTriggerAndKeepsManualRunAvailable(bool completed)
    {
        SchedulerInfo info = CreateInfo();
        info.HasScheduleCheckpoint = true;
        info.ScheduleCompleted = completed;
        info.ScheduledFireCount = completed ? 1 : 0;
        info.Status = SchedulerStatus.Running;
        info.PreviousFireTime = "2026/10/10 08:00:00";
        _manager.TaskInfos.Add(info);
        await _manager.RestoreJob(info);
        await _scheduler.Start();

        var key = new JobKey(info.JobName, info.GroupName);
        Assert.Empty(await _scheduler.GetTriggersOfJob(key));
        Assert.Equal(completed ? SchedulerStatus.Completed : SchedulerStatus.Paused, info.Status);
        Assert.Equal("2026/10/10 08:00:00", info.PreviousFireTime);
        await _scheduler.TriggerJob(key);
        await NextExecution();
        Assert.Equal(completed ? 1 : 0, info.ScheduledFireCount);
        Assert.Equal(completed, info.ScheduleCompleted);
        Assert.Empty(await _scheduler.GetTriggersOfJob(key));
    }

    [Fact]
    public async Task MissingPrimary_RecoversTheBackupWithoutArmingIt()
    {
        string path = Path.Combine(_directory, "tasks.json");
        SchedulerInfo saved = CreateInfo();
        saved.HasScheduleCheckpoint = true;
        saved.Status = SchedulerStatus.Running;
        SchedulerTaskSerializer.SaveToFile(path + ".bak", [saved]);
        byte[] backup = File.ReadAllBytes(path + ".bak");

        Assert.NotNull(_manager.LoadTaskDefinitions());

        SchedulerInfo recovered = Assert.Single(_manager.TaskInfos);
        Assert.Equal(saved.JobName, recovered.JobName);
        Assert.Equal(SchedulerStatus.Paused, recovered.Status);
        Assert.Equal(backup, File.ReadAllBytes(path + ".bak"));
        Assert.Equal(saved.JobName, Assert.Single(SchedulerTaskSerializer.LoadFromFile(path)).JobName);
        await _manager.RestoreJob(recovered);
        Assert.Empty(await _scheduler.GetTriggersOfJob(new JobKey(recovered.JobName, recovered.GroupName)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleManager_CannotOverwriteAnotherManagersSavedDefinitions(bool existingFile)
    {
        string path = Path.Combine(_directory, "tasks.json");
        if (existingFile)
            SchedulerTaskSerializer.SaveToFile(path, [CreateInfo()]);
        var staleManager = new QuartzSchedulerManager(_scheduler, path);
        Assert.Null(_manager.LoadTaskDefinitions());
        Assert.Null(staleManager.LoadTaskDefinitions());

        SchedulerInfo savedByFirst = CreateInfo();
        savedByFirst.JobName = "saved-by-first";
        Assert.True((await _manager.CreateJob(savedByFirst)).Success);
        byte[] primary = File.ReadAllBytes(path);
        byte[]? backup = existingFile ? File.ReadAllBytes(path + ".bak") : null;

        SchedulerInfo staleAddition = CreateInfo();
        staleAddition.JobName = "stale-addition";
        SchedulerOperationResult result = await staleManager.CreateJob(staleAddition);

        Assert.False(result.Success);
        Assert.Equal(SchedulerOperationError.PersistenceFailure, result.Error);
        Assert.DoesNotContain(staleAddition, staleManager.TaskInfos);
        Assert.False(await _scheduler.Exists(new JobKey(staleAddition.JobName, staleAddition.GroupName)));
        Assert.Equal(primary, File.ReadAllBytes(path));
        if (backup == null)
        {
            Assert.False(File.Exists(path + ".bak"));
            return;
        }

        Assert.Equal(backup, File.ReadAllBytes(path + ".bak"));
        File.Delete(path);
        File.Delete(path + ".bak");
        Assert.Null(staleManager.LoadTaskDefinitions());
        Assert.Single(staleManager.TaskInfos);
        Assert.False((await staleManager.CreateJob(staleAddition)).Success);
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("{\"$values\":[")]
    [InlineData("null")]
    [InlineData("{\"$type\":\"Missing.TaskList, Missing.SchedulerJobs\",\"$values\":[]}")]
    public async Task UnreadablePrimary_PreservesItAndItsBackupUntilValidReload(string invalidJson)
    {
        string path = Path.Combine(_directory, "tasks.json");
        SchedulerInfo previous = CreateInfo();
        SchedulerTaskSerializer.SaveToFile(path + ".bak", [previous]);
        File.WriteAllText(path, invalidJson);
        byte[] backup = File.ReadAllBytes(path + ".bak");

        Assert.NotNull(_manager.LoadTaskDefinitions());
        Assert.Empty(_manager.TaskInfos);
        var replacement = CreateInfo();
        replacement.JobName = "replacement";
        SchedulerOperationResult rejected = await _manager.CreateJob(replacement);
        Assert.False(rejected.Success);
        Assert.Empty(_manager.TaskInfos);
        Assert.False(await _scheduler.Exists(new JobKey(replacement.JobName, replacement.GroupName)));
        Assert.Equal(invalidJson, File.ReadAllText(path));
        Assert.Equal(backup, File.ReadAllBytes(path + ".bak"));

        File.Copy(path + ".bak", path, overwrite: true);
        Assert.Null(_manager.LoadTaskDefinitions());
        Assert.Equal(previous.JobName, Assert.Single(_manager.TaskInfos).JobName);
        Assert.True((await _manager.CreateJob(replacement)).Success);
    }

    [Fact]
    public async Task PausedManualRun_DoesNotConsumeOrResumeRemainingSchedule()
    {
        SchedulerInfo info = CreateInfo();
        info.RepeatMode = JobRepeatMode.Multiple;
        info.RepeatCount = 4;
        info.Interval = TimeSpan.FromHours(1);
        Assert.True((await _manager.CreateJob(info)).Success);
        await _scheduler.Start();
        await NextExecution();
        await _manager.StopJob(info.JobName, info.GroupName);
        DateTimeOffset? savedNext = info.NextScheduledFireTimeUtc;

        await _scheduler.TriggerJob(new JobKey(info.JobName, info.GroupName));
        await NextExecution();
        Assert.Equal(SchedulerStatus.Paused, info.Status);
        Assert.Equal(1, info.ScheduledFireCount);
        Assert.Equal(savedNext, info.NextScheduledFireTimeUtc);

        await _manager.ExecuteJob(info.JobName, info.GroupName);
        ISimpleTrigger resumed = Assert.IsAssignableFrom<ISimpleTrigger>(Assert.Single(
            await _scheduler.GetTriggersOfJob(new JobKey(info.JobName, info.GroupName))));
        Assert.Equal(3, resumed.RepeatCount); // Four executions remain.
        Assert.Equal(savedNext, resumed.NextFireTimeUtc);
        Assert.Equal(2, info.RunCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPauseSave_LeavesRequestedSchedulesPaused(bool pauseAll)
    {
        SchedulerInfo first = CreateInfo();
        first.IsDelayed = true;
        first.Delay = TimeSpan.FromHours(1);
        SchedulerInfo second = CreateInfo();
        second.JobName = "second-job";
        second.IsDelayed = true;
        second.Delay = TimeSpan.FromHours(1);
        Assert.True((await _manager.CreateJob(first)).Success);
        Assert.True((await _manager.CreateJob(second)).Success);
        await _scheduler.Start();
        var firstTrigger = new TriggerKey($"{first.JobName}-trigger", first.GroupName);
        var secondTrigger = new TriggerKey($"{second.JobName}-trigger", second.GroupName);
        Assert.Equal(TriggerState.Normal, await _scheduler.GetTriggerState(firstTrigger));
        Assert.Equal(TriggerState.Normal, await _scheduler.GetTriggerState(secondTrigger));
        string path = Path.Combine(_directory, "tasks.json");
        Task Pause() => pauseAll ? _manager.PauseAll() : _manager.StopJob(first.JobName, first.GroupName);

        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(Pause);
            Assert.Equal(TriggerState.Paused, await _scheduler.GetTriggerState(firstTrigger));
            Assert.Equal(pauseAll ? TriggerState.Paused : TriggerState.Normal, await _scheduler.GetTriggerState(secondTrigger));
            Assert.Equal(SchedulerStatus.Paused, first.Status);
            Assert.Equal(pauseAll ? SchedulerStatus.Paused : SchedulerStatus.Ready, second.Status);
        }

        Assert.All(SchedulerTaskSerializer.LoadFromFile(path), task => Assert.Equal(SchedulerStatus.Ready, task.Status));
        await Pause();
        var saved = SchedulerTaskSerializer.LoadFromFile(path);
        Assert.Equal(SchedulerStatus.Paused, saved.Single(task => task.JobName == first.JobName).Status);
        Assert.Equal(pauseAll ? SchedulerStatus.Paused : SchedulerStatus.Ready, saved.Single(task => task.JobName == second.JobName).Status);
    }

    [Fact]
    public async Task FailedResumeSave_RestoresPausedJobWithoutLeavingATrigger()
    {
        // A directory cannot be replaced by the task JSON file.
        var manager = new QuartzSchedulerManager(_scheduler, _directory);
        SchedulerInfo info = CreateInfo();
        manager.TaskInfos.Add(info);
        await manager.RestoreJob(info);
        await _scheduler.Start();

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ResumeJob(info.JobName, info.GroupName));
        Assert.Equal(SchedulerStatus.Paused, info.Status);
        Assert.False(info.HasScheduleCheckpoint);
        Assert.Empty(await _scheduler.GetTriggersOfJob(new JobKey(info.JobName, info.GroupName)));
    }

    [Theory]
    [InlineData(JobRepeatMode.Once)]
    [InlineData(JobRepeatMode.Multiple)]
    public async Task ScheduledFireWithUnsavedCheckpoint_IsVetoedAndCanResume(JobRepeatMode repeatMode)
    {
        SchedulerInfo info = CreateInfo();
        info.RepeatMode = repeatMode;
        info.Interval = TimeSpan.FromHours(1);
        var observation = new ScheduledFireObservation();
        _scheduler.ListenerManager.AddJobListener(observation);
        Assert.True((await _manager.CreateJob(info)).Success);

        var jobKey = new JobKey(info.JobName, info.GroupName);
        DateTimeOffset? savedNext = info.NextScheduledFireTimeUtc;
        string? previousFire = info.PreviousFireTime;
        string path = Path.Combine(_directory, "tasks.json");
        byte[] saved = File.ReadAllBytes(path);
        try
        {
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                await _scheduler.Start();
                Assert.True(await observation.Vetoed.Task.WaitAsync(TimeSpan.FromSeconds(15)));
                Assert.Equal(SchedulerStatus.Paused, info.Status);
                Assert.False(info.ScheduleCompleted);
                Assert.Equal(0, info.ScheduledFireCount);
                Assert.Equal(0, info.RunCount);
                Assert.Equal(savedNext, info.NextScheduledFireTimeUtc);
                Assert.Equal(previousFire, info.PreviousFireTime);
                Assert.False(string.IsNullOrWhiteSpace(info.ScheduleAdmissionError));
                Assert.False(_executions.Reader.TryRead(out _));
            }
            Assert.Equal(saved, File.ReadAllBytes(path));

            // Resume before Quartz finishes handling the veto. Its cleanup
            // of the old fire must leave the replacement trigger alone.
            await _manager.ResumeJob(info.JobName, info.GroupName);
            Assert.Null(info.ScheduleAdmissionError);
            ISimpleTrigger resumed = Assert.IsAssignableFrom<ISimpleTrigger>(
                Assert.Single(await _scheduler.GetTriggersOfJob(jobKey)));
            Assert.Equal(repeatMode == JobRepeatMode.Once ? 0 : info.RepeatCount, resumed.RepeatCount);
        }
        finally
        {
            observation.ReleaseVeto.TrySetResult();
        }

        if (repeatMode == JobRepeatMode.Once)
        {
            await NextExecution();
            Assert.Equal(1, info.ScheduledFireCount);
            Assert.True(info.ScheduleCompleted);
            Assert.Equal(SchedulerStatus.Completed, info.Status);
        }
        else
        {
            // The manual run uses the same single Quartz worker, so its
            // completion also proves the old fire's veto cleanup is done.
            await _scheduler.TriggerJob(jobKey);
            await NextExecution();
            var triggerKey = new TriggerKey($"{info.JobName}-trigger", info.GroupName);
            ISimpleTrigger resumed = Assert.IsAssignableFrom<ISimpleTrigger>(await _scheduler.GetTrigger(triggerKey));
            Assert.Equal(info.RepeatCount, resumed.RepeatCount);
            Assert.Equal(TriggerState.Normal, await _scheduler.GetTriggerState(triggerKey));
            Assert.Equal(0, info.ScheduledFireCount);
        }
    }

    private async Task NextExecution()
    {
        // A timeout fails a hung test; correctness assertions use completed
        // callbacks and trigger state rather than sleeps or timing tolerances.
        JobExecutionRecord record = await _executions.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(record.Success, record.Message);
    }

    [Fact]
    public async Task StatusBar_TracksInFlightRoundAfterPauseAndNextTimeAfterResume()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _scheduler.Context[nameof(StatusProbeJob)] = (started, finish);
        SchedulerInfo info = CreateInfo();
        info.JobType = typeof(StatusProbeJob);
        info.RepeatMode = JobRepeatMode.Multiple;
        info.RepeatCount = 1;
        info.Interval = TimeSpan.FromDays(2);
        Assert.True((await _manager.CreateJob(info)).Success);

        // Exercise the real status bar binding with no task window open.
        TextBlock status = WpfTestHost.Invoke(() =>
        {
            var provider = new SchedulerStatusBarProvider(_manager);
            var statusBar = new StatusBarControl();
            statusBar.LoadItems(provider.GetStatusBarIconMetadata());
            var right = Assert.IsType<StackPanel>(statusBar.FindName("RightItemsPanel"));
            var item = Assert.IsType<Border>(Assert.Single(right.Children));
            var host = Assert.IsType<ContentControl>(item.Child);
            var content = Assert.IsType<StackPanel>(host.Content);
            return Assert.IsType<TextBlock>(content.Children[0]);
        });
        Task<string> ReadStatusAsync() => status.Dispatcher.InvokeAsync(() => status.Text, DispatcherPriority.Background).Task;

        try
        {
            await _scheduler.Start();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(Resources.Sched_StatusBarRunning, await ReadStatusAsync());

            await WpfTestHost.Invoke(() => _manager.StopJob(info.JobName, info.GroupName));
            Assert.Equal(Resources.Sched_StatusBarPausedRunning, await ReadStatusAsync());
        }
        finally
        {
            finish.TrySetResult();
        }

        await NextExecution();
        Assert.Empty(await ReadStatusAsync());

        await WpfTestHost.Invoke(() => _manager.ResumeJob(info.JobName, info.GroupName));
        DateTimeOffset next = Assert.IsType<DateTimeOffset>(info.NextScheduledFireTimeUtc);
        string time = next.LocalDateTime.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.CurrentCulture);
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, Resources.Sched_StatusBarNextRun, time), await ReadStatusAsync());
    }

    [Fact]
    public async Task PauseAndResumeAll_KeepCompletedSchedulesUnarmed()
    {
        SchedulerInfo pending = CreateInfo();
        pending.IsDelayed = true;
        pending.Delay = TimeSpan.FromHours(1);
        Assert.True((await _manager.CreateJob(pending)).Success);
        SchedulerInfo completed = CreateInfo();
        completed.JobName = "completed";
        completed.HasScheduleCheckpoint = completed.ScheduleCompleted = true;
        _manager.TaskInfos.Add(completed);
        await _manager.RestoreJob(completed);
        await _scheduler.Start();

        await _manager.PauseAll();
        Assert.Equal(SchedulerStatus.Paused, pending.Status);
        Assert.Equal(SchedulerStatus.Completed, completed.Status);
        await _manager.ResumeAll();
        Assert.Equal(SchedulerStatus.Ready, pending.Status);
        Assert.Equal(TriggerState.Normal, await _scheduler.GetTriggerState(new TriggerKey($"{pending.JobName}-trigger", pending.GroupName)));
        Assert.Empty(await _scheduler.GetTriggersOfJob(new JobKey(completed.JobName, completed.GroupName)));
    }

    [Fact]
    public async Task EditCompletedTask_StartsANewScheduleAndKeepsHistoricalStatistics()
    {
        SchedulerInfo original = CreateInfo();
        original.HasScheduleCheckpoint = original.ScheduleCompleted = true;
        original.ScheduledFireCount = 1;
        original.RunCount = 24657;
        _manager.TaskInfos.Add(original);
        await _manager.RestoreJob(original);
        SchedulerInfo edited = Assert.Single(SchedulerTaskSerializer.Deserialize(SchedulerTaskSerializer.Serialize(_manager.TaskInfos)));
        edited.IsDelayed = true;
        edited.Delay = TimeSpan.FromMinutes(5);

        Assert.True((await _manager.UpdateJob(edited)).Success);
        Assert.False(edited.ScheduleCompleted);
        Assert.Equal(0, edited.ScheduledFireCount);
        Assert.Equal(24657, edited.RunCount);
        Assert.Single(await _scheduler.GetTriggersOfJob(new JobKey(edited.JobName, edited.GroupName)));
    }

    [Fact]
    public async Task ExecuteCompletedLoop_RunsAllConfiguredRepetitionsAndRejectsDuplicateStart()
    {
        SchedulerInfo original = CreateInfo();
        original.RepeatMode = JobRepeatMode.Multiple;
        original.RepeatCount = 4;
        original.Interval = TimeSpan.FromMilliseconds(10);
        original.HasScheduleCheckpoint = original.ScheduleCompleted = true;
        original.ScheduledFireCount = 5;
        original.RunCount = 24657;
        _manager.TaskInfos.Add(original);
        await _manager.RestoreJob(original);

        await _manager.ExecuteJob(original.JobName, original.GroupName);
        SchedulerInfo restarted = Assert.Single(_manager.TaskInfos);
        Assert.False(restarted.ScheduleCompleted);
        Assert.Equal(0, restarted.ScheduledFireCount);
        Assert.Equal(24657, restarted.RunCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _manager.ExecuteJob(original.JobName, original.GroupName));
        ISimpleTrigger trigger = Assert.IsAssignableFrom<ISimpleTrigger>(Assert.Single(
            await _scheduler.GetTriggersOfJob(new JobKey(original.JobName, original.GroupName))));
        Assert.Equal(4, trigger.RepeatCount);

        await _scheduler.Start();
        for (int execution = 0; execution <= original.RepeatCount; execution++)
            await NextExecution();

        Assert.Equal(5, restarted.ScheduledFireCount);
        Assert.Equal(24662, restarted.RunCount);
        Assert.True(restarted.ScheduleCompleted);
        Assert.Equal(SchedulerStatus.Completed, restarted.Status);
    }

    [Fact]
    public async Task ExecuteCron_ArmsTheConfiguredTimeInsteadOfAddingAnImmediateRun()
    {
        SchedulerInfo info = CreateInfo();
        info.Mode = JobExecutionMode.Cron;
        info.CronExpression = "0 0 0 1 1 ? 2099";
        _manager.TaskInfos.Add(info);
        await _manager.RestoreJob(info);

        await _manager.ExecuteJob(info.JobName, info.GroupName);

        ICronTrigger trigger = Assert.IsAssignableFrom<ICronTrigger>(Assert.Single(
            await _scheduler.GetTriggersOfJob(new JobKey(info.JobName, info.GroupName))));
        Assert.Equal(info.CronExpression, trigger.CronExpressionString);
        Assert.Equal(2099, trigger.NextFireTimeUtc!.Value.ToLocalTime().Year);
        Assert.Equal(SchedulerStatus.Ready, info.Status);
        Assert.Equal(0, info.ScheduledFireCount);
    }

    [Fact]
    public async Task FailedReexecutionSave_KeepsCompletedProgressAndDoesNotArmAnotherRound()
    {
        var manager = new QuartzSchedulerManager(_scheduler, _directory);
        SchedulerInfo original = CreateInfo();
        original.HasScheduleCheckpoint = original.ScheduleCompleted = true;
        original.ScheduledFireCount = 1;
        manager.TaskInfos.Add(original);
        await manager.RestoreJob(original);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ExecuteJob(original.JobName, original.GroupName));

        Assert.Same(original, Assert.Single(manager.TaskInfos));
        Assert.True(original.ScheduleCompleted);
        Assert.Equal(1, original.ScheduledFireCount);
        Assert.Empty(await _scheduler.GetTriggersOfJob(new JobKey(original.JobName, original.GroupName)));
    }

    private static SchedulerInfo CreateInfo() => new()
    {
        JobName = "test-job",
        GroupName = "test-group",
        JobType = typeof(NoOpJob),
        Mode = JobExecutionMode.Simple,
        RepeatMode = JobRepeatMode.Once,
    };

    private sealed class ScheduledFireObservation : IJobListener
    {
        public string Name => nameof(ScheduledFireObservation);
        internal TaskCompletionSource<bool> Vetoed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseVeto { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            Vetoed.TrySetResult(true);
            await ReleaseVeto.Task.WaitAsync(cancellationToken);
        }

        public ValueTask JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
        {
            Vetoed.TrySetResult(false);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class NoOpJob : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    public sealed class StatusProbeJob : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            var (started, finish) = ((TaskCompletionSource, TaskCompletionSource))context.Scheduler.Context[nameof(StatusProbeJob)]!;
            started.TrySetResult();
            await finish.Task.WaitAsync(cancellationToken);
        }
    }

    public async Task DisposeAsync()
    {
        await _scheduler.Shutdown(waitForJobsToComplete: true);
        await _factory.DisposeAsync();
        // The fixture creates this unique absolute directory under Temp only.
        Directory.Delete(_directory, recursive: true);
    }
}

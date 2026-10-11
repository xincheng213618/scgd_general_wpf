using ColorVision.Scheduler;
using ColorVision.Scheduler.Data;
using Quartz;
using System.IO;
using System.Threading.Channels;
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
        _scheduler.ListenerManager.AddJobListener(_manager.Listener);
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

        await _manager.ResumeJob(info.JobName, info.GroupName);
        ISimpleTrigger resumed = Assert.IsAssignableFrom<ISimpleTrigger>(Assert.Single(
            await _scheduler.GetTriggersOfJob(new JobKey(info.JobName, info.GroupName))));
        Assert.Equal(3, resumed.RepeatCount); // Four executions remain.
        Assert.Equal(savedNext, resumed.NextFireTimeUtc);
        Assert.Equal(2, info.RunCount);
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

    private async Task NextExecution()
    {
        // A timeout fails a hung test; correctness assertions use completed
        // callbacks and trigger state rather than sleeps or timing tolerances.
        JobExecutionRecord record = await _executions.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(record.Success, record.Message);
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

    private static SchedulerInfo CreateInfo() => new()
    {
        JobName = "test-job",
        GroupName = "test-group",
        JobType = typeof(NoOpJob),
        Mode = JobExecutionMode.Simple,
        RepeatMode = JobRepeatMode.Once,
    };

    public sealed class NoOpJob : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _scheduler.Shutdown(waitForJobsToComplete: true);
        await _factory.DisposeAsync();
        // The fixture creates this unique absolute directory under Temp only.
        Directory.Delete(_directory, recursive: true);
    }
}

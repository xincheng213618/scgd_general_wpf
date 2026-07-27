using ColorVision.Scheduler;
using Quartz;
using Quartz.Impl;
using System.Collections.Specialized;

namespace ColorVision.UI.Tests
{
    public sealed class SchedulerTriggerFactoryTests
    {
        private static readonly DateTimeOffset ScheduleBaseUtc =
            new(2026, 7, 26, 8, 0, 0, TimeSpan.Zero);

        [Fact]
        public void SimpleMultiple_PreservesLegacyAdditionalRepeatCount()
        {
            SchedulerInfo info = CreateInfo();
            info.Mode = JobExecutionMode.Simple;
            info.RepeatMode = JobRepeatMode.Multiple;
            info.RepeatCount = 3;
            info.Interval = TimeSpan.FromMinutes(2);

            SchedulerTriggerBuildResult result = SchedulerTriggerFactory.Build(info, ScheduleBaseUtc);

            Assert.True(result.Success, result.ErrorMessage);
            ISimpleTrigger trigger = Assert.IsAssignableFrom<ISimpleTrigger>(result.Trigger);
            Assert.Equal(3, trigger.RepeatCount);
            Assert.Equal(TimeSpan.FromMinutes(2), trigger.RepeatInterval);
        }

        [Fact]
        public void DailyIntervalMultiple_PreservesLegacyAdditionalRepeatCount()
        {
            SchedulerInfo info = CreateInfo();
            info.Mode = JobExecutionMode.Interval;
            info.RepeatMode = JobRepeatMode.Multiple;
            info.RepeatCount = 4;
            info.Interval = TimeSpan.FromMinutes(5);

            SchedulerTriggerBuildResult result = SchedulerTriggerFactory.Build(info, ScheduleBaseUtc);

            Assert.True(result.Success, result.ErrorMessage);
            IDailyTimeIntervalTrigger trigger = Assert.IsAssignableFrom<IDailyTimeIntervalTrigger>(result.Trigger);
            Assert.Equal(4, trigger.RepeatCount);
            Assert.Equal(300, trigger.RepeatInterval);
            Assert.Equal(IntervalUnit.Second, trigger.RepeatIntervalUnit);
        }

        [Fact]
        public void DailyIntervalForever_DoesNotCollapseToOncePerDay()
        {
            SchedulerInfo info = CreateInfo();
            info.Mode = JobExecutionMode.Interval;
            info.RepeatMode = JobRepeatMode.Forever;
            info.Interval = TimeSpan.FromSeconds(30);

            SchedulerTriggerBuildResult result = SchedulerTriggerFactory.Build(info, ScheduleBaseUtc);

            Assert.True(result.Success, result.ErrorMessage);
            IDailyTimeIntervalTrigger trigger = Assert.IsAssignableFrom<IDailyTimeIntervalTrigger>(result.Trigger);
            Assert.Equal(-1, trigger.RepeatCount);
        }

        [Fact]
        public void CalendarMode_IsExplicitlyOneDayCalendarInterval()
        {
            SchedulerInfo info = CreateInfo();
            info.Mode = JobExecutionMode.Calendar;

            SchedulerTriggerBuildResult result = SchedulerTriggerFactory.Build(info, ScheduleBaseUtc);

            Assert.True(result.Success, result.ErrorMessage);
            ICalendarIntervalTrigger trigger = Assert.IsAssignableFrom<ICalendarIntervalTrigger>(result.Trigger);
            Assert.Equal(1, trigger.RepeatInterval);
            Assert.Equal(IntervalUnit.Day, trigger.RepeatIntervalUnit);
        }

        [Fact]
        public void CronMode_PreservesValidatedExpression()
        {
            SchedulerInfo info = CreateInfo();
            info.Mode = JobExecutionMode.Cron;
            info.CronExpression = "0 0/5 * * * ?";

            SchedulerTriggerBuildResult result = SchedulerTriggerFactory.Build(info, ScheduleBaseUtc);

            Assert.True(result.Success, result.ErrorMessage);
            ICronTrigger trigger = Assert.IsAssignableFrom<ICronTrigger>(result.Trigger);
            Assert.Equal(info.CronExpression, trigger.CronExpressionString);
        }

        [Fact]
        public void DelayedStart_PreservesSubSecondDelay()
        {
            SchedulerInfo info = CreateInfo();
            info.JobStartMode = JobStartMode.Delayed;
            info.Delay = TimeSpan.FromMilliseconds(1500);

            SchedulerTriggerBuildResult result = SchedulerTriggerFactory.Build(info, ScheduleBaseUtc);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(ScheduleBaseUtc.AddMilliseconds(1500), result.Trigger!.StartTimeUtc);
        }

        [Fact]
        public void RepeatingSchedule_RejectsNonPositiveIntervalBeforeBuilding()
        {
            SchedulerInfo info = CreateInfo();
            info.Mode = JobExecutionMode.Simple;
            info.RepeatMode = JobRepeatMode.Forever;
            info.Interval = TimeSpan.Zero;

            SchedulerTriggerBuildResult result = SchedulerTriggerFactory.Build(info, ScheduleBaseUtc);

            Assert.False(result.Success);
            Assert.Null(result.Trigger);
            Assert.NotEmpty(result.ErrorMessage);
        }

        [Fact]
        public void DailyInterval_RejectsFractionalSecondsInsteadOfTruncating()
        {
            SchedulerInfo info = CreateInfo();
            info.Mode = JobExecutionMode.Interval;
            info.RepeatMode = JobRepeatMode.Forever;
            info.Interval = TimeSpan.FromMilliseconds(1500);

            SchedulerTriggerBuildResult result = SchedulerTriggerFactory.Build(info, ScheduleBaseUtc);

            Assert.False(result.Success);
            Assert.Null(result.Trigger);
            Assert.NotEmpty(result.ErrorMessage);
        }

        [Fact]
        public void InvalidOrOverflowingValues_ReturnStructuredFailure()
        {
            SchedulerInfo invalidModeInfo = CreateInfo();
            invalidModeInfo.Mode = (JobExecutionMode)999;
            SchedulerTriggerBuildResult invalidModeResult =
                SchedulerTriggerFactory.Build(invalidModeInfo, ScheduleBaseUtc);

            SchedulerInfo overflowingDelayInfo = CreateInfo();
            overflowingDelayInfo.JobStartMode = JobStartMode.Delayed;
            overflowingDelayInfo.Delay = TimeSpan.MaxValue;
            SchedulerTriggerBuildResult overflowingDelayResult =
                SchedulerTriggerFactory.Build(overflowingDelayInfo, ScheduleBaseUtc);

            Assert.False(invalidModeResult.Success);
            Assert.False(overflowingDelayResult.Success);
            Assert.NotEmpty(invalidModeResult.ErrorMessage);
            Assert.NotEmpty(overflowingDelayResult.ErrorMessage);
        }

        [Fact]
        public async Task QuartzBulkReplace_ReplacesSameIdentityWithoutDuplicatingTrigger()
        {
            IScheduler scheduler = await CreateRamScheduler();
            var jobKey = new JobKey("replace-job", "replace-group");
            var triggerKey = new TriggerKey("replace-job-trigger", "replace-group");
            try
            {
                IJobDetail originalJob = JobBuilder.Create<TestJob>()
                    .WithIdentity(jobKey)
                    .UsingJobData("revision", "old")
                    .Build();
                ITrigger originalTrigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .ForJob(jobKey)
                    .StartAt(DateTimeOffset.UtcNow.AddHours(1))
                    .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(10).RepeatForever())
                    .Build();
                await scheduler.ScheduleJob(originalJob, originalTrigger);

                IJobDetail replacementJob = JobBuilder.Create<TestJob>()
                    .WithIdentity(jobKey)
                    .UsingJobData("revision", "new")
                    .Build();
                ITrigger replacementTrigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .ForJob(jobKey)
                    .StartAt(DateTimeOffset.UtcNow.AddHours(2))
                    .WithPriority(9)
                    .WithSimpleSchedule(schedule => schedule.WithIntervalInMinutes(30).RepeatForever())
                    .Build();

                await scheduler.ScheduleJob(replacementJob, [replacementTrigger], replace: true);

                IJobDetail? storedJob = await scheduler.GetJobDetail(jobKey);
                IReadOnlyCollection<ITrigger> storedTriggers = await scheduler.GetTriggersOfJob(jobKey);
                Assert.NotNull(storedJob);
                Assert.Equal("new", storedJob.JobDataMap.GetString("revision"));
                ITrigger storedTrigger = Assert.Single(storedTriggers);
                Assert.Equal(triggerKey, storedTrigger.Key);
                Assert.Equal(9, storedTrigger.Priority);
                Assert.Equal(TimeSpan.FromMinutes(30), Assert.IsAssignableFrom<ISimpleTrigger>(storedTrigger).RepeatInterval);
            }
            finally
            {
                await scheduler.Shutdown(waitForJobsToComplete: false);
            }
        }

        [Fact]
        public async Task QuartzBulkReplace_WhenReplacementIsInvalid_KeepsExistingSchedule()
        {
            IScheduler scheduler = await CreateRamScheduler();
            var jobKey = new JobKey("rollback-job", "rollback-group");
            var triggerKey = new TriggerKey("rollback-job-trigger", "rollback-group");
            try
            {
                IJobDetail originalJob = JobBuilder.Create<TestJob>()
                    .WithIdentity(jobKey)
                    .UsingJobData("revision", "old")
                    .Build();
                ITrigger originalTrigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .ForJob(jobKey)
                    .StartAt(DateTimeOffset.UtcNow.AddHours(1))
                    .WithPriority(4)
                    .Build();
                await scheduler.ScheduleJob(originalJob, originalTrigger);

                IJobDetail replacementJob = JobBuilder.Create<TestJob>()
                    .WithIdentity(jobKey)
                    .UsingJobData("revision", "new")
                    .Build();
                ITrigger replacementTrigger = TriggerBuilder.Create()
                    .WithIdentity(triggerKey)
                    .ForJob(jobKey)
                    .StartAt(DateTimeOffset.UtcNow.AddHours(2))
                    .ModifiedByCalendar("missing-calendar")
                    .Build();

                await Assert.ThrowsAnyAsync<Exception>(
                    () => scheduler.ScheduleJob(replacementJob, [replacementTrigger], replace: true));

                IJobDetail? storedJob = await scheduler.GetJobDetail(jobKey);
                IReadOnlyCollection<ITrigger> storedTriggers = await scheduler.GetTriggersOfJob(jobKey);
                Assert.NotNull(storedJob);
                Assert.Equal("old", storedJob.JobDataMap.GetString("revision"));
                ITrigger storedTrigger = Assert.Single(storedTriggers);
                Assert.Equal(triggerKey, storedTrigger.Key);
                Assert.Equal(4, storedTrigger.Priority);
            }
            finally
            {
                await scheduler.Shutdown(waitForJobsToComplete: false);
            }
        }

        private static async Task<IScheduler> CreateRamScheduler()
        {
            var properties = new NameValueCollection
            {
                ["quartz.scheduler.instanceName"] = $"scheduler-tests-{Guid.NewGuid():N}",
                ["quartz.scheduler.instanceId"] = "AUTO",
                ["quartz.threadPool.type"] = "Quartz.Simpl.DefaultThreadPool, Quartz",
                ["quartz.threadPool.threadCount"] = "1",
                ["quartz.jobStore.type"] = "Quartz.Simpl.RAMJobStore, Quartz",
            };
            return await new StdSchedulerFactory(properties).GetScheduler();
        }

        private static SchedulerInfo CreateInfo()
        {
            return new SchedulerInfo
            {
                JobName = "test-job",
                GroupName = "test-group",
                JobType = typeof(TestJob),
                Mode = JobExecutionMode.Simple,
                RepeatMode = JobRepeatMode.Once,
                Priority = 5,
            };
        }

        private sealed class TestJob : IJob
        {
            public Task Execute(IJobExecutionContext context)
            {
                return Task.CompletedTask;
            }
        }
    }
}

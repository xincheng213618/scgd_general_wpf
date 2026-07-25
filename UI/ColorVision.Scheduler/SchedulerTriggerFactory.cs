using Quartz;

namespace ColorVision.Scheduler
{
    public sealed record SchedulerTriggerBuildResult(ITrigger? Trigger, string ErrorMessage)
    {
        public bool Success => Trigger != null;

        public static SchedulerTriggerBuildResult Failed(string errorMessage)
        {
            return new SchedulerTriggerBuildResult(null, errorMessage);
        }
    }

    public static class SchedulerTriggerFactory
    {
        public static SchedulerTriggerBuildResult Build(SchedulerInfo info, DateTimeOffset? nowUtc = null)
        {
            ArgumentNullException.ThrowIfNull(info);

            string? validationError = Validate(info);
            if (validationError != null)
                return SchedulerTriggerBuildResult.Failed(validationError);

            try
            {
                DateTimeOffset scheduleBaseUtc = (nowUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
                var triggerBuilder = TriggerBuilder.Create()
                    .WithIdentity($"{info.JobName}-trigger", info.GroupName)
                    .WithPriority(info.Priority);

                if (info.JobStartMode == JobStartMode.Delayed)
                    triggerBuilder.StartAt(scheduleBaseUtc.Add(info.Delay));
                else
                    triggerBuilder.StartAt(scheduleBaseUtc);

                switch (info.Mode)
                {
                    case JobExecutionMode.Simple:
                        triggerBuilder.WithSimpleSchedule(schedule =>
                        {
                            switch (info.RepeatMode)
                            {
                                case JobRepeatMode.Multiple:
                                    // Preserve the persisted contract: RepeatCount means
                                    // additional executions after the initial firing.
                                    schedule.WithInterval(info.Interval).WithRepeatCount(info.RepeatCount);
                                    break;
                                case JobRepeatMode.Forever:
                                    schedule.WithInterval(info.Interval).RepeatForever();
                                    break;
                                case JobRepeatMode.Once:
                                    schedule.WithRepeatCount(0);
                                    break;
                            }
                        });
                        break;
                    case JobExecutionMode.Calendar:
                        triggerBuilder.WithCalendarIntervalSchedule(schedule => schedule.WithIntervalInDays(1));
                        break;
                    case JobExecutionMode.Cron:
                        triggerBuilder.WithCronSchedule(info.CronExpression);
                        break;
                    case JobExecutionMode.Interval:
                        triggerBuilder.WithDailyTimeIntervalSchedule(schedule =>
                        {
                            schedule.WithInterval((int)info.Interval.TotalSeconds, IntervalUnit.Second);
                            switch (info.RepeatMode)
                            {
                                case JobRepeatMode.Multiple:
                                    schedule.WithRepeatCount(info.RepeatCount);
                                    break;
                                case JobRepeatMode.Once:
                                    schedule.WithRepeatCount(0);
                                    break;
                                case JobRepeatMode.Forever:
                                    // DailyTimeIntervalScheduleBuilder defaults to repeating for
                                    // every valid day. Setting repeat count to zero would mean
                                    // only one fire per day, which is not "forever".
                                    break;
                            }
                        });
                        break;
                }

                return new SchedulerTriggerBuildResult(triggerBuilder.Build(), string.Empty);
            }
            catch (Exception ex)
            {
                return SchedulerTriggerBuildResult.Failed(ex.Message);
            }
        }

        public static string? Validate(SchedulerInfo info)
        {
            ArgumentNullException.ThrowIfNull(info);

            if (!Enum.IsDefined(info.JobStartMode)
                || !Enum.IsDefined(info.Mode)
                || !Enum.IsDefined(info.RepeatMode))
            {
                return Properties.Resources.Sched_ParamError;
            }

            if (info.JobType == null
                || !typeof(IJob).IsAssignableFrom(info.JobType)
                || !info.JobType.IsClass
                || info.JobType.IsAbstract)
            {
                return Properties.Resources.Sched_TypeEmpty;
            }

            if (string.IsNullOrWhiteSpace(info.JobName) || string.IsNullOrWhiteSpace(info.GroupName))
                return Properties.Resources.Sched_NameEmpty;

            if (info.Priority is < 1 or > 10)
                return $"{Properties.Resources.Sched_PriorityLabel}: 1-10";

            if (info.TimeoutSeconds < 0)
                return $"{Properties.Resources.Sched_Timeout}: >= 0";

            if (info.JobStartMode == JobStartMode.Delayed && info.Delay <= TimeSpan.Zero)
                return $"{Properties.Resources.Sched_TriggerDelay}: > 00:00:00";

            bool needsInterval = info.Mode == JobExecutionMode.Interval
                || info.Mode == JobExecutionMode.Simple && info.RepeatMode != JobRepeatMode.Once;
            if (needsInterval && info.Interval <= TimeSpan.Zero)
                return $"{Properties.Resources.Sched_Interval}: > 00:00:00";

            if (info.Mode == JobExecutionMode.Interval
                && (info.Interval.TotalSeconds < 1
                    || info.Interval.TotalSeconds > int.MaxValue
                    || info.Interval.Ticks % TimeSpan.TicksPerSecond != 0))
            {
                return $"{Properties.Resources.Sched_Interval}: 00:00:01 - {TimeSpan.FromSeconds(int.MaxValue)} (whole seconds)";
            }

            if ((info.Mode == JobExecutionMode.Simple || info.Mode == JobExecutionMode.Interval)
                && info.RepeatMode == JobRepeatMode.Multiple
                && info.RepeatCount <= 0)
            {
                return Properties.Resources.Sched_RepeatInvalid;
            }

            if (info.Mode == JobExecutionMode.Cron)
            {
                if (string.IsNullOrWhiteSpace(info.CronExpression))
                    return Properties.Resources.Sched_CronEmpty;
                if (!CronExpression.IsValidExpression(info.CronExpression))
                    return Properties.Resources.Sched_CronInvalid;
            }

            return null;
        }
    }
}

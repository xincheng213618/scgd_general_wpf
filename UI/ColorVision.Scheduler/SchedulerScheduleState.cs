using Quartz;

namespace ColorVision.Scheduler;

internal static class SchedulerScheduleState
{
    internal static void Restore(SchedulerInfo info)
    {
        // Legacy display timestamps can prove exhaustion, but lifetime counts
        // include earlier schedules and manual runs and cannot prove progress.
        if (!info.HasScheduleCheckpoint)
            info.ScheduleCompleted = info.Mode is JobExecutionMode.Simple or JobExecutionMode.Interval
            && info.RepeatMode != JobRepeatMode.Forever
            && info.NextFireTime == "N/A"
            && !string.IsNullOrWhiteSpace(info.PreviousFireTime)
            && info.PreviousFireTime != "N/A";
        info.Status = info.ScheduleCompleted ? SchedulerStatus.Completed : SchedulerStatus.Paused;
    }

    internal static void Reset(SchedulerInfo info, ITrigger trigger)
    {
        info.HasScheduleCheckpoint = true;
        info.ScheduledFireCount = 0;
        info.ScheduleCompleted = false;
        info.NextScheduledFireTimeUtc = trigger.NextFireTimeUtc;
    }

    internal static bool RecordFire(SchedulerInfo info, IJobExecutionContext context)
    {
        info.PreviousFireTime = context.FireTimeUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
        if (context.Trigger.Key != new TriggerKey($"{info.JobName}-trigger", info.GroupName))
            return false;

        info.HasScheduleCheckpoint = true;
        info.ScheduledFireCount++;
        info.NextScheduledFireTimeUtc = context.NextFireTimeUtc;
        info.ScheduleCompleted = context.NextFireTimeUtc == null;
        info.NextFireTime = context.NextFireTimeUtc?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "N/A";
        return true;
    }

    internal static SchedulerTriggerBuildResult BuildRemaining(SchedulerInfo info, DateTimeOffset? nowUtc = null)
    {
        DateTimeOffset now = nowUtc ?? DateTimeOffset.UtcNow;
        SchedulerTriggerBuildResult result = SchedulerTriggerFactory.Build(info, now);
        if (!result.Success)
            return result;

        long remaining = info.RepeatMode == JobRepeatMode.Once ? 1 : (long)info.RepeatCount + 1;
        remaining -= info.ScheduledFireCount;
        bool finite = info.Mode is JobExecutionMode.Simple or JobExecutionMode.Interval
            && info.RepeatMode != JobRepeatMode.Forever;
        if (info.ScheduleCompleted || finite && remaining <= 0)
            return SchedulerTriggerBuildResult.Failed(Properties.Resources.Sched_ScheduleFinished);

        var builder = result.Trigger!.GetTriggerBuilder();
        if (info.HasScheduleCheckpoint)
        {
            // Keep a future due time; skip overdue firings instead of replaying
            // a backlog on resume. A delayed start is not restarted each time.
            DateTimeOffset next = info.NextScheduledFireTimeUtc is { } saved && saved > now
                ? saved
                : info.Mode == JobExecutionMode.Simple && info.RepeatMode != JobRepeatMode.Once
                    ? now.Add(info.Interval)
                    : now;
            builder.StartAt(next);
        }

        if (finite && info.Mode == JobExecutionMode.Simple)
            builder.WithSimpleSchedule(schedule =>
            {
                schedule.WithRepeatCount((int)(remaining - 1));
                if (info.RepeatMode != JobRepeatMode.Once)
                    schedule.WithInterval(info.Interval);
            });
        else if (finite && info.Mode == JobExecutionMode.Interval)
            builder.WithDailyTimeIntervalSchedule(schedule => schedule
                .WithInterval((int)info.Interval.TotalSeconds, IntervalUnit.Second)
                .WithRepeatCount((int)(remaining - 1)));

        return new SchedulerTriggerBuildResult(builder.Build(), string.Empty);
    }
}

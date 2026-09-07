using ColorVision.Common.Utilities;
using ColorVision.Scheduler.Properties;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;

namespace ColorVision.Scheduler;

// Display-only projections stay out of the persisted SchedulerInfo contract.
internal static class SchedulerPresentation
{
    internal static string ScheduleSummary(SchedulerInfo task)
    {
        string plan = task.Mode switch
        {
            JobExecutionMode.Cron => $"Cron · {task.CronExpression}",
            JobExecutionMode.Calendar => Resources.Sched_EveryCalendarDay,
            _ when task.RepeatMode == JobRepeatMode.Once => Resources.Sched_RunOnce,
            _ when task.RepeatMode == JobRepeatMode.Multiple => string.Format(CultureInfo.CurrentCulture, Resources.Sched_RepeatSummary, task.Interval, (long)task.RepeatCount + 1),
            _ => string.Format(CultureInfo.CurrentCulture, Resources.Sched_ForeverSummary, task.Interval)
        };
        if (task.Mode == JobExecutionMode.Interval)
            plan = $"{task.Mode.ToDescription()} · {plan}";
        return task.IsDelayed
            ? $"{plan} · {string.Format(CultureInfo.CurrentCulture, Resources.Sched_DelaySummary, task.Delay)}"
            : plan;
    }

    internal static string StatusText(SchedulerStatus status) => status switch
    {
        SchedulerStatus.Running => Resources.Sched_Running,
        SchedulerStatus.Paused => Resources.Sched_Paused,
        _ => Resources.Sched_Ready
    };

    internal static string TaskTypeName(Type? type) => type?.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type?.Name ?? Resources.Sched_SelectTaskType;

    internal static string Timestamp(string? value) => string.IsNullOrWhiteSpace(value) || value == "N/A" ? "—" : value;
}

internal sealed class SchedulerScheduleSummaryConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.FirstOrDefault() is SchedulerInfo task ? SchedulerPresentation.ScheduleSummary(task) : string.Empty;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

internal sealed class SchedulerDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => parameter switch
    {
        "Status" when value is SchedulerStatus status => SchedulerPresentation.StatusText(status),
        "Type" => SchedulerPresentation.TaskTypeName(value as Type),
        "Time" => SchedulerPresentation.Timestamp(value as string),
        _ => value ?? DependencyProperty.UnsetValue
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

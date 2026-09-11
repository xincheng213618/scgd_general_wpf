using Newtonsoft.Json;
using System.Text;

namespace ColorVision.Scheduler;

// Formats snapshots only. File selection and notifications belong to the window.
internal static class SchedulerTaskExporter
{
    internal static string Csv(IReadOnlyCollection<SchedulerInfo> tasks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("任务名称,分组名称,优先级,运行次数,成功次数,失败次数,状态,最后执行时间(ms),平均执行时间(ms),最大执行时间(ms),最小执行时间(ms),最后执行结果,结果详情,下次执行时间,上次执行时间,创建时间");
        foreach (var task in tasks)
            sb.AppendLine($"{Quote(task.JobName)},{Quote(task.GroupName)},{task.Priority},{task.RunCount},{task.SuccessCount},{task.FailureCount},{Quote(task.Status.ToString())},{task.LastExecutionTimeMs},{task.AverageExecutionTimeMs},{task.MaxExecutionTimeMs},{task.MinExecutionTimeMs},{Quote(task.LastExecutionResult)},{Quote(task.LastExecutionMessage)},{Quote(task.NextFireTime)},{Quote(task.PreviousFireTime)},{Quote(task.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"))}");
        return sb.ToString();
    }

    private static string Quote(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

    internal static string Json(IReadOnlyCollection<SchedulerInfo> tasks) => JsonConvert.SerializeObject(tasks, new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore
    });

    internal static string Report(IReadOnlyCollection<SchedulerInfo> tasks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║        ColorVision.Scheduler 任务执行统计报告                  ║");
        sb.AppendLine("╚════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"任务总数: {tasks.Count}");
        sb.AppendLine();

        // 总体统计
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("总体统计");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        var totalRuns = tasks.Sum(t => t.RunCount);
        var totalSuccess = tasks.Sum(t => t.SuccessCount);
        var totalFailure = tasks.Sum(t => t.FailureCount);
        var avgExecutionTime = tasks.Where(t => t.AverageExecutionTimeMs > 0).Average(t => (double?)t.AverageExecutionTimeMs) ?? 0;

        sb.AppendLine($"总执行次数: {totalRuns}");
        sb.AppendLine($"成功次数: {totalSuccess} ({(totalRuns > 0 ? (totalSuccess * 100.0 / totalRuns).ToString("F2") : "0.00")}%)");
        sb.AppendLine($"失败次数: {totalFailure} ({(totalRuns > 0 ? (totalFailure * 100.0 / totalRuns).ToString("F2") : "0.00")}%)");
        sb.AppendLine($"平均执行时间: {avgExecutionTime:F2} ms");
        sb.AppendLine();

        // 按状态分组统计
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("任务状态分布");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        var statusGroups = tasks.GroupBy(t => t.Status);
        foreach (var group in statusGroups)
        {
            sb.AppendLine($"{group.Key}: {group.Count()} 个任务");
        }
        sb.AppendLine();

        // 详细任务列表
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("任务详细信息");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        foreach (var task in tasks.OrderByDescending(t => t.RunCount))
        {
            sb.AppendLine();
            sb.AppendLine($"【{task.JobName}】({task.GroupName})");
            sb.AppendLine($"  优先级: {task.Priority}");
            sb.AppendLine($"  状态: {task.Status}");
            sb.AppendLine($"  执行统计: 总计 {task.RunCount} 次 (成功 {task.SuccessCount}, 失败 {task.FailureCount})");
            if (task.RunCount > 0)
            {
                sb.AppendLine($"  执行时间: 最后 {task.LastExecutionTimeMs}ms, 平均 {task.AverageExecutionTimeMs}ms, 最大 {task.MaxExecutionTimeMs}ms, 最小 {task.MinExecutionTimeMs}ms");
            }
            if (!string.IsNullOrEmpty(task.LastExecutionResult))
            {
                sb.AppendLine($"  最后执行结果: {task.LastExecutionResult}");
            }
            if (!string.IsNullOrEmpty(task.LastExecutionMessage))
            {
                sb.AppendLine($"  结果详情: {task.LastExecutionMessage}");
            }
            if (!string.IsNullOrEmpty(task.NextFireTime) && task.NextFireTime != "N/A")
            {
                sb.AppendLine($"  下次执行: {task.NextFireTime}");
            }
            if (!string.IsNullOrEmpty(task.PreviousFireTime))
            {
                sb.AppendLine($"  上次执行: {task.PreviousFireTime}");
            }
            sb.AppendLine($"  创建时间: {task.CreateTime:yyyy-MM-dd HH:mm:ss}");
        }

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("报告结束");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");


        return sb.ToString();
    }
}

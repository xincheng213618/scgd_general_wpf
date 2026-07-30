using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed record CopilotTaskRunDiagnosticSnapshot(
        string ConversationId,
        string Title,
        CopilotAgentMode Mode,
        CopilotHostedRunState State,
        DateTimeOffset EnqueuedAtUtc,
        DateTimeOffset? StartedAtUtc,
        bool IsCheckpointReady,
        int QueuePosition);

    internal sealed record CopilotTaskAttentionDiagnosticSnapshot(
        string ConversationId,
        string Title,
        string Status,
        int RemainingCount,
        bool CanResume);

    internal sealed record CopilotTaskDiagnosticSnapshot(
        DateTimeOffset CapturedAtUtc,
        bool HostShutdown,
        int MaximumQueuedRuns,
        IReadOnlyList<CopilotTaskRunDiagnosticSnapshot> Runs,
        int TotalAttentionTasks,
        IReadOnlyList<CopilotTaskAttentionDiagnosticSnapshot> AttentionTasks);

    internal static class CopilotTaskDiagnostics
    {
        internal const int MaximumAttentionTasks = 20;

        public static CopilotTaskDiagnosticSnapshot Capture(
            CopilotAgentTaskHost host,
            IEnumerable<CopilotConversationRecord>? conversations,
            DateTimeOffset capturedAtUtc)
        {
            ArgumentNullException.ThrowIfNull(host);

            var conversationList = (conversations ?? Array.Empty<CopilotConversationRecord>())
                .Where(conversation => conversation != null)
                .ToArray();
            var titles = conversationList
                .Where(conversation => !string.IsNullOrWhiteSpace(conversation.Id))
                .GroupBy(conversation => conversation.Id, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => NormalizeLabel(group.First().Title, "未命名会话", 120),
                    StringComparer.Ordinal);

            var queuePosition = 0;
            var runs = new List<CopilotTaskRunDiagnosticSnapshot>();
            foreach (var run in host.ScheduledRuns)
            {
                var state = run.State;
                if (state == CopilotHostedRunState.Completed)
                    continue;

                var currentQueuePosition = state == CopilotHostedRunState.Queued
                    ? ++queuePosition
                    : 0;
                runs.Add(new CopilotTaskRunDiagnosticSnapshot(
                    run.ConversationId,
                    titles.TryGetValue(run.ConversationId, out var title) ? title : "未命名会话",
                    run.Mode,
                    state,
                    run.EnqueuedAtUtc,
                    run.StartedAtUtc,
                    run.IsCheckpointReady,
                    currentQueuePosition));
            }

            var scheduledConversationIds = runs
                .Select(run => run.ConversationId)
                .ToHashSet(StringComparer.Ordinal);
            var attentionTasks = CopilotAgentTaskIndex.Build(conversationList)
                .Where(task => !scheduledConversationIds.Contains(task.ConversationId))
                .Select(task => new CopilotTaskAttentionDiagnosticSnapshot(
                    task.ConversationId,
                    NormalizeLabel(task.Title, "未命名会话", 120),
                    NormalizeLabel(task.StatusLabel, "等待处理", 40),
                    Math.Max(0, task.RemainingCount),
                    task.CanResume))
                .ToArray();

            return new CopilotTaskDiagnosticSnapshot(
                capturedAtUtc,
                host.IsShutdown,
                host.MaxQueuedRuns,
                runs,
                attentionTasks.Length,
                attentionTasks.Take(MaximumAttentionTasks).ToArray());
        }

        public static string Format(CopilotTaskDiagnosticSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var runs = snapshot.Runs ?? Array.Empty<CopilotTaskRunDiagnosticSnapshot>();
            var attentionTasks = snapshot.AttentionTasks ?? Array.Empty<CopilotTaskAttentionDiagnosticSnapshot>();
            var queuedCount = runs.Count(run => run.State == CopilotHostedRunState.Queued);
            var hostState = snapshot.HostShutdown
                ? "已关闭"
                : runs.Any(run => run.State != CopilotHostedRunState.Queued)
                    ? "运行中"
                    : "空闲";
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision Copilot 任务");
            builder.AppendLine("本地快照：不会调用模型或工具，也不会修改任务、队列或会话。");
            builder.Append("宿主：").Append(hostState)
                .Append(" · 排队 ").Append(FormatCount(queuedCount))
                .Append('/').Append(FormatCount(snapshot.MaximumQueuedRuns))
                .Append(" · 待处理 ").AppendLine(FormatCount(snapshot.TotalAttentionTasks));
            builder.AppendLine();
            builder.AppendLine("活动与队列");
            if (runs.Count == 0)
            {
                builder.AppendLine("无正在运行或排队的任务。");
            }
            else
            {
                for (var index = 0; index < runs.Count; index++)
                    AppendRun(builder, runs[index], index + 1, snapshot.CapturedAtUtc);
            }

            builder.AppendLine();
            builder.AppendLine("需要处理");
            if (attentionTasks.Count == 0)
            {
                builder.AppendLine("无暂停、受阻或等待恢复的任务。");
            }
            else
            {
                for (var index = 0; index < attentionTasks.Count; index++)
                    AppendAttentionTask(builder, attentionTasks[index], index + 1);
                var omittedCount = Math.Max(0, snapshot.TotalAttentionTasks - attentionTasks.Count);
                if (omittedCount > 0)
                    builder.Append("另有 ").Append(FormatCount(omittedCount)).AppendLine(" 条未显示，请使用 Agent 任务列表查看。");
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendRun(
            StringBuilder builder,
            CopilotTaskRunDiagnosticSnapshot run,
            int index,
            DateTimeOffset capturedAtUtc)
        {
            builder.Append(FormatCount(index)).Append(". [").Append(FormatRunState(run)).Append("] ")
                .Append(NormalizeLabel(run.Title, "未命名会话", 120))
                .Append(" · ").Append(FormatMode(run.Mode))
                .Append(" · ").Append(FormatRunDuration(run, capturedAtUtc));
            if (run.IsCheckpointReady)
                builder.Append(" · 恢复点已就绪");
            builder.AppendLine();
        }

        private static void AppendAttentionTask(
            StringBuilder builder,
            CopilotTaskAttentionDiagnosticSnapshot task,
            int index)
        {
            builder.Append(FormatCount(index)).Append(". [")
                .Append(NormalizeLabel(task.Status, "等待处理", 40)).Append("] ")
                .Append(NormalizeLabel(task.Title, "未命名会话", 120));

            var remainingText = task.RemainingCount > 0 ? $"剩余 {FormatCount(task.RemainingCount)} 项" : string.Empty;
            if (remainingText.Length > 0)
                builder.Append(" · ").Append(remainingText);
            if (task.CanResume)
                builder.Append(" · 可继续");
            builder.AppendLine();
        }

        private static string FormatRunState(CopilotTaskRunDiagnosticSnapshot run)
        {
            return run.State switch
            {
                CopilotHostedRunState.Queued => $"排队 {FormatCount(Math.Max(1, run.QueuePosition))}",
                CopilotHostedRunState.Running => "运行中",
                CopilotHostedRunState.PauseRequested => "正在暂停",
                CopilotHostedRunState.CancelRequested => "正在取消",
                CopilotHostedRunState.Completed => "已完成",
                _ => run.State.ToString(),
            };
        }

        private static string FormatMode(CopilotAgentMode mode)
        {
            return mode switch
            {
                CopilotAgentMode.Chat => "聊天",
                CopilotAgentMode.Auto => "自动",
                CopilotAgentMode.Explain => "解释",
                CopilotAgentMode.Web => "网页",
                CopilotAgentMode.Code => "代码",
                CopilotAgentMode.Review => "审查",
                CopilotAgentMode.Diagnose => "诊断",
                CopilotAgentMode.Plan => "计划",
                _ => mode.ToString(),
            };
        }

        private static string FormatRunDuration(
            CopilotTaskRunDiagnosticSnapshot run,
            DateTimeOffset capturedAtUtc)
        {
            var startedAt = run.State == CopilotHostedRunState.Queued
                ? run.EnqueuedAtUtc
                : run.StartedAtUtc ?? run.EnqueuedAtUtc;
            var elapsed = capturedAtUtc - startedAt;
            var prefix = run.State == CopilotHostedRunState.Queued ? "已等待 " : "已运行 ";
            return prefix + FormatDuration(elapsed);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            var totalSeconds = Math.Max(0, (long)duration.TotalSeconds);
            if (totalSeconds < 60)
                return $"{FormatCount(totalSeconds)} 秒";

            var totalMinutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            if (totalMinutes < 60)
                return $"{FormatCount(totalMinutes)} 分 {seconds:00} 秒";

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            return $"{FormatCount(hours)} 小时 {minutes:00} 分";
        }

        private static string NormalizeLabel(string? value, string fallback, int maximumLength)
        {
            var normalized = string.Join(
                " ",
                (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (normalized.Length == 0)
                return fallback;
            if (normalized.Length <= maximumLength)
                return normalized;

            var retainedLength = maximumLength;
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength].TrimEnd() + "...";
        }

        private static string FormatCount(long value)
        {
            return Math.Max(0, value).ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotTaskCommandAction
    {
        List,
        Stop,
        Resume,
        Invalid,
    }

    internal readonly record struct CopilotTaskCommandRequest(
        CopilotTaskCommandAction Action,
        int Position);

    internal enum CopilotTaskStopRequestOutcome
    {
        NotFound,
        PauseRequested,
        CancelRequested,
    }

    internal sealed record CopilotTaskRunDiagnosticSnapshot(
        string RunId,
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
        internal const string Usage = "用法：/tasks [stop N|resume N]"
            + "\n输入 /tasks 查看实时位置；stop N 对应“活动与队列”，resume N 对应“需要处理”。";

        public static CopilotTaskCommandRequest ParseCommand(string? arguments)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
                return new CopilotTaskCommandRequest(CopilotTaskCommandAction.List, 0);

            var parts = normalized.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var position)
                && position > 0)
            {
                if (string.Equals(parts[0], "stop", StringComparison.OrdinalIgnoreCase))
                    return new CopilotTaskCommandRequest(CopilotTaskCommandAction.Stop, position);
                if (string.Equals(parts[0], "resume", StringComparison.OrdinalIgnoreCase))
                    return new CopilotTaskCommandRequest(CopilotTaskCommandAction.Resume, position);
            }

            return new CopilotTaskCommandRequest(CopilotTaskCommandAction.Invalid, 0);
        }

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
                    run.Id,
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

        public static CopilotTaskRunDiagnosticSnapshot? FindRun(
            CopilotTaskDiagnosticSnapshot snapshot,
            int position)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (position <= 0 || position > snapshot.Runs.Count)
                return null;

            return snapshot.Runs[position - 1];
        }

        public static CopilotTaskAttentionDiagnosticSnapshot? FindAttentionTask(
            CopilotTaskDiagnosticSnapshot snapshot,
            int position)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (position <= 0 || position > snapshot.AttentionTasks.Count)
                return null;

            return snapshot.AttentionTasks[position - 1];
        }

        public static string FormatStopConfirmation(
            CopilotTaskRunDiagnosticSnapshot run,
            int position)
        {
            ArgumentNullException.ThrowIfNull(run);

            var action = run.State switch
            {
                CopilotHostedRunState.Queued => "将取消尚未开始的排队任务，并移除其持久化恢复项。",
                CopilotHostedRunState.PauseRequested => "该任务已在等待安全暂停；继续会升级为取消。",
                CopilotHostedRunState.CancelRequested => "该任务已在等待取消完成，不会重复发出停止请求。",
                _ when run.Mode != CopilotAgentMode.Chat && run.IsCheckpointReady =>
                    "将优先请求安全暂停并保留可恢复 checkpoint；若状态刚刚变化，则取消当前轮次。",
                _ => "将请求取消当前轮次；已完成的消息与审计证据仍会保留。",
            };

            return new StringBuilder()
                .Append("停止任务 #")
                .Append(FormatCount(position))
                .Append('？')
                .AppendLine()
                .Append("会话：")
                .AppendLine(NormalizeLabel(run.Title, "未命名会话", 120))
                .Append("状态：")
                .Append(FormatRunState(run))
                .Append(" · ")
                .AppendLine(FormatMode(run.Mode))
                .AppendLine(action)
                .Append("其他任务不会改变；确认窗口不会显示提示词、附件、内部运行 ID 或授权内容。")
                .ToString();
        }

        public static CopilotTaskStopRequestOutcome RequestStop(
            CopilotAgentTaskHost host,
            string? runId)
        {
            ArgumentNullException.ThrowIfNull(host);
            if (string.IsNullOrWhiteSpace(runId))
                return CopilotTaskStopRequestOutcome.NotFound;

            var run = host.ScheduledRuns.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, runId, StringComparison.Ordinal));
            if (run == null || run.State is CopilotHostedRunState.CancelRequested or CopilotHostedRunState.Completed)
                return CopilotTaskStopRequestOutcome.NotFound;

            if (run.State == CopilotHostedRunState.PauseRequested)
            {
                return host.RequestCancel(run.Id)
                    ? CopilotTaskStopRequestOutcome.CancelRequested
                    : CopilotTaskStopRequestOutcome.NotFound;
            }

            if (run.IsAgent && host.RequestPause(run.Id))
                return CopilotTaskStopRequestOutcome.PauseRequested;

            return host.RequestCancel(run.Id)
                ? CopilotTaskStopRequestOutcome.CancelRequested
                : CopilotTaskStopRequestOutcome.NotFound;
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
                builder.AppendLine("控制：/tasks stop N（仅对应上方“活动与队列”的编号；执行前会原生确认）");
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
                builder.AppendLine("恢复：/tasks resume N（仅对应上方“需要处理”的编号；恢复前会重新检查 checkpoint 与运行环境）");
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

using ColorVision.Copilot.Mcp;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotAgentTaskEventDiagnosticAction
    {
        Recent,
        Errors,
        Invalid,
    }

    internal readonly record struct CopilotAgentTaskEventDiagnosticRequest(
        CopilotAgentTaskEventDiagnosticAction Action,
        int Limit);

    internal static class CopilotAgentTaskEventDiagnostics
    {
        internal const int MaximumDisplayedEvents = 20;
        internal const string Usage = "用法：/task-log [N|errors]"
            + "\nN 可取 1–100；errors 只显示错误、阻塞、审批拒绝、异常停止及带失败码的事件。";

        public static CopilotAgentTaskEventDiagnosticRequest ParseCommand(string? arguments)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return new CopilotAgentTaskEventDiagnosticRequest(
                    CopilotAgentTaskEventDiagnosticAction.Recent,
                    MaximumDisplayedEvents);
            }
            if (string.Equals(normalized, "errors", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotAgentTaskEventDiagnosticRequest(
                    CopilotAgentTaskEventDiagnosticAction.Errors,
                    MaximumDisplayedEvents);
            }
            if (int.TryParse(
                    normalized,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var limit)
                && limit is >= 1 and <= CopilotAgentTaskEventJournal.MaxQueryLimit)
            {
                return new CopilotAgentTaskEventDiagnosticRequest(
                    CopilotAgentTaskEventDiagnosticAction.Recent,
                    limit);
            }

            return new CopilotAgentTaskEventDiagnosticRequest(
                CopilotAgentTaskEventDiagnosticAction.Invalid,
                0);
        }

        public static string Format(CopilotConversationRecord? conversation) =>
            Format(conversation, string.Empty);

        public static string Format(
            CopilotConversationRecord? conversation,
            string? arguments)
        {
            var request = ParseCommand(arguments);
            if (request.Action == CopilotAgentTaskEventDiagnosticAction.Invalid)
                return Usage;
            if (conversation == null)
                return "Agent 任务日志" + Environment.NewLine + "当前没有可检查的 Copilot 会话。";

            var title = string.IsNullOrWhiteSpace(conversation.Title)
                ? CopilotUiText.NewConversationTitle
                : conversation.Title.Trim();
            var journal = conversation.LatestAgentTaskEventJournal
                ?? conversation.AgentSessionCheckpoint?.TaskEventJournal;
            if (journal?.Events?.Count is not > 0 || !journal.IsStructurallyValid())
            {
                return $"Agent 任务日志 · {title}"
                    + Environment.NewLine
                    + "当前会话还没有已保存的 Agent 任务事件。运行一次 Agent 任务后再查看。";
            }

            var filteredJournal = request.Action == CopilotAgentTaskEventDiagnosticAction.Errors
                ? new CopilotAgentTaskEventJournalSnapshot
                {
                    Events = journal.Events.Where(IsFailureEvent).ToArray(),
                }
                : journal;
            if (filteredJournal.Events.Count == 0)
            {
                return $"Agent 任务日志 · {title}"
                    + Environment.NewLine
                    + "当前会话没有已保存的失败事件。"
                    + Environment.NewLine
                    + "范围：仅检查持久化的限长脱敏摘要；不包含工具参数、模型隐藏推理或授权凭据。";
            }

            var result = CopilotAgentTaskEventJournal.Query(
                filteredJournal,
                new CopilotAgentTaskEventQuery { Limit = request.Limit });
            var category = request.Action == CopilotAgentTaskEventDiagnosticAction.Errors
                ? "失败"
                : "最近";
            var builder = new StringBuilder()
                .Append("Agent 任务日志 · ")
                .AppendLine(title)
                .Append(category)
                .Append(' ')
                .Append(result.Events.Count.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" / ")
                .Append(filteredJournal.Events.Count.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine(" 条（新到旧）");

            foreach (var item in result.Events)
            {
                builder
                    .Append('#')
                    .Append(item.Sequence.ToString(CultureInfo.InvariantCulture))
                    .Append(" · ")
                    .Append(item.OccurredAtUtc.ToLocalTime().ToString("MM-dd HH:mm:ss", CultureInfo.CurrentCulture))
                    .Append(" · ")
                    .Append(item.Type);
                if (!string.IsNullOrWhiteSpace(item.ToolName))
                    builder.Append(" · ").Append(item.ToolName);
                if (!string.IsNullOrWhiteSpace(item.State))
                    builder.Append(" · ").Append(item.State);
                if (!string.IsNullOrWhiteSpace(item.FailureCode))
                    builder.Append(" · ").Append(item.FailureCode);
                builder.AppendLine();

                var summary = CopilotMcpAuditLogger.RedactText(item.Summary);
                if (!string.IsNullOrWhiteSpace(summary))
                    builder.Append("  ").AppendLine(summary);
            }

            if (result.HasMore)
            {
                builder
                    .Append("另有 ")
                    .Append((filteredJournal.Events.Count - result.Events.Count).ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 条较早")
                    .Append(request.Action == CopilotAgentTaskEventDiagnosticAction.Errors ? "失败" : string.Empty)
                    .AppendLine("事件未显示。");
            }

            builder.Append("范围：仅显示持久化的限长脱敏摘要；不包含工具参数、模型隐藏推理或授权凭据。");
            return builder.ToString();
        }

        private static bool IsFailureEvent(CopilotAgentTaskEvent item)
        {
            if (item.Type is CopilotAgentTaskEventType.RuntimeError
                    or CopilotAgentTaskEventType.BlockerDetected
                    or CopilotAgentTaskEventType.ApprovalDenied
                || !string.IsNullOrWhiteSpace(item.FailureCode))
            {
                return true;
            }

            return item.Type == CopilotAgentTaskEventType.RunStopped
                && Enum.TryParse<CopilotAgentStopReason>(item.State, out var stopReason)
                && stopReason is CopilotAgentStopReason.ApprovalDenied
                    or CopilotAgentStopReason.Blocked
                    or CopilotAgentStopReason.BudgetExhausted
                    or CopilotAgentStopReason.TaskPassLimit
                    or CopilotAgentStopReason.IncompleteOutput
                    or CopilotAgentStopReason.ProviderFailure
                    or CopilotAgentStopReason.Interrupted;
        }
    }
}

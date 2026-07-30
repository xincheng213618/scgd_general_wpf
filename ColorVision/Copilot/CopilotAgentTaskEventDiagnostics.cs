using ColorVision.Copilot.Mcp;
using System;
using System.Globalization;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotAgentTaskEventDiagnostics
    {
        internal const int MaximumDisplayedEvents = 20;

        public static string Format(CopilotConversationRecord? conversation)
        {
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

            var result = CopilotAgentTaskEventJournal.Query(
                journal,
                new CopilotAgentTaskEventQuery { Limit = MaximumDisplayedEvents });
            var builder = new StringBuilder()
                .Append("Agent 任务日志 · ")
                .AppendLine(title)
                .Append("最近 ")
                .Append(result.Events.Count.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" / ")
                .Append(journal.Events.Count.ToString("N0", CultureInfo.CurrentCulture))
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
                    .Append((journal.Events.Count - result.Events.Count).ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 条较早事件未显示。");
            }

            builder.Append("范围：仅显示持久化的限长脱敏摘要；不包含工具参数、模型隐藏推理或授权凭据。");
            return builder.ToString();
        }
    }
}

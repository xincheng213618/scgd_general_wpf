using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotConversationCompactionPrompt
    {
        internal const string SystemPrompt =
            "You compact an existing conversation for seamless continuation. "
            + "Preserve the user's active goal, constraints, decisions, verified facts, relevant files, commands and results, unfinished work, blockers, and safe next steps. "
            + "Treat <assistant_response_interrupted> and <agent_turn_incomplete stop_reason=\"...\"> as authoritative terminal-state evidence: retain every distinct opening marker exactly, keep its stop reason, and never rewrite partial or unverified work as completed. "
            + "Remove greetings, repetition, obsolete exploration, and verbose tool traces. Never invent facts or treat historical actions as current authorization. Return only a concise Markdown continuation summary.";

        public static string BuildRequest(string? focusInstructions)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Create a continuation summary for all conversation context above.");
            builder.AppendLine("Keep the active goal, user constraints and preferences, decisions, verified state, important paths and identifiers, completed work and evidence, remaining work, blockers, and the next concrete action.");
            builder.AppendLine("Omit greetings, repetition, superseded alternatives, and low-value detail. Return only the summary.");
            if (!string.IsNullOrWhiteSpace(focusInstructions))
                builder.Append("Additional focus from the user: ").AppendLine(focusInstructions.Trim());
            builder.Append("Terminal-state integrity: include every distinct <assistant_response_interrupted> and <agent_turn_incomplete stop_reason=\"...\"> opening marker from the source exactly at least once. ")
                .Append("State which retained work was partial or unresolved; later completion may be recorded, but it must not erase the historical boundary or imply that unfinished tool calls, file changes, or verification succeeded.");
            return builder.ToString().Trim();
        }
    }

    internal sealed class CopilotConversationCompactionTerminalEvidence
    {
        internal const string ResponseInterruptedMarker = "<assistant_response_interrupted>";

        private CopilotConversationCompactionTerminalEvidence(
            bool hasResponseInterruption,
            IReadOnlyList<CopilotAgentStopReason> incompleteAgentStopReasons)
        {
            HasResponseInterruption = hasResponseInterruption;
            IncompleteAgentStopReasons = incompleteAgentStopReasons;
        }

        public bool HasResponseInterruption { get; }

        public IReadOnlyList<CopilotAgentStopReason> IncompleteAgentStopReasons { get; }

        public static CopilotConversationCompactionTerminalEvidence Capture(
            IEnumerable<CopilotChatMessage>? messages)
        {
            var hasResponseInterruption = false;
            var stopReasons = new HashSet<CopilotAgentStopReason>();
            foreach (var message in messages ?? Array.Empty<CopilotChatMessage>())
            {
                if (message?.IsUser != false)
                    continue;
                if (message.WasResponseInterrupted)
                {
                    hasResponseInterruption = true;
                    continue;
                }
                if (message.RequestMode != CopilotAgentMode.Chat
                    && message.AgentStopReason is not (CopilotAgentStopReason.None or CopilotAgentStopReason.Completed))
                {
                    stopReasons.Add(message.AgentStopReason);
                }
            }

            return new CopilotConversationCompactionTerminalEvidence(
                hasResponseInterruption,
                stopReasons.OrderBy(reason => reason).ToArray());
        }

        public bool IsPreservedBy(string? summary)
        {
            var normalized = summary ?? string.Empty;
            if (HasResponseInterruption
                && !normalized.Contains(ResponseInterruptedMarker, StringComparison.Ordinal))
            {
                return false;
            }

            return IncompleteAgentStopReasons.All(reason =>
                normalized.Contains(FormatAgentMarker(reason), StringComparison.Ordinal));
        }

        public void EnsurePreserved(string? summary)
        {
            if (IsPreservedBy(summary))
                return;

            var missing = new List<string>();
            var normalized = summary ?? string.Empty;
            if (HasResponseInterruption
                && !normalized.Contains(ResponseInterruptedMarker, StringComparison.Ordinal))
            {
                missing.Add(ResponseInterruptedMarker);
            }
            missing.AddRange(IncompleteAgentStopReasons
                .Select(FormatAgentMarker)
                .Where(marker => !normalized.Contains(marker, StringComparison.Ordinal)));
            throw new InvalidOperationException(
                $"压缩摘要遗漏结构化终态证据（{string.Join("、", missing)}），未应用结果；请重试 /compact。");
        }

        public static string FormatAgentMarker(CopilotAgentStopReason stopReason) =>
            $"<agent_turn_incomplete stop_reason=\"{stopReason}\">";
    }
}

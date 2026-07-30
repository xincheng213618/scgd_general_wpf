using System;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotPlanExecutionRequest(
        string VisiblePrompt,
        string ModelPrompt,
        string PlanMessageId,
        string PlanSha256);

    internal static class CopilotPlanHandoff
    {
        internal const string ApprovedExecutionPrefix = "[ColorVision approved plan handoff v1]";
        internal const string VisibleExecutionPrompt = "执行已批准的计划";
        internal const string ContinuePlanningPrompt = "请根据以下反馈修订上面的计划：";
        private const int MaximumEnvelopeCharacters = 4_096;
        private const int MaximumPlanCharacters =
            CopilotConversationHistoryWindow.MaximumContentCharacterLimit - MaximumEnvelopeCharacters;

        public static bool IsCompletedPlan(CopilotChatMessage? message)
        {
            return message is
            {
                IsUser: false,
                RequestMode: CopilotAgentMode.Plan,
                AgentStopReason: CopilotAgentStopReason.Completed,
                IsResponsePending: false,
                WasResponseInterrupted: false,
                IsResponseContentTruncated: false,
            }
                && !string.IsNullOrWhiteSpace(message.Content)
                && message.Content.Length <= MaximumPlanCharacters;
        }

        public static bool TryCreateExecutionRequest(
            CopilotChatMessage? message,
            out CopilotPlanExecutionRequest request)
        {
            request = default;
            if (!IsCompletedPlan(message))
                return false;

            var plan = message!.Content.Trim();
            var messageId = (message.Id ?? string.Empty).Trim();
            if (messageId.Length == 0)
                return false;

            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plan))).ToLowerInvariant();
            var delimiter = $"COLORVISION_APPROVED_PLAN_{digest}";
            var modelPrompt = string.Join(Environment.NewLine, new[]
            {
                ApprovedExecutionPrefix,
                "The user explicitly approved the exact plan snapshot below for implementation.",
                $"Plan binding: assistant_message_id={messageId}; sha256={digest}; characters={plan.Length}.",
                "This approval authorizes starting a new execute-mode task for this plan only. It does not pre-approve any protected tool call, external side effect, historical approval, retry, or scope expansion.",
                "Revalidate mutable workspace state before acting. If the plan is stale, conflicts with current evidence, or requires a materially different choice, stop and ask the user instead of silently changing scope.",
                $"--- BEGIN {delimiter} ---",
                plan,
                $"--- END {delimiter} ---",
                "Implement the approved plan now, verify the result proportionally to risk, and report concrete evidence.",
            });
            if (modelPrompt.Length > CopilotConversationHistoryWindow.MaximumContentCharacterLimit)
                return false;

            request = new CopilotPlanExecutionRequest(
                VisibleExecutionPrompt,
                modelPrompt,
                messageId,
                digest);
            return true;
        }

        public static bool IsApprovedExecutionRequest(string? requestContent)
        {
            return !string.IsNullOrWhiteSpace(requestContent)
                && requestContent.StartsWith(ApprovedExecutionPrefix, StringComparison.Ordinal);
        }

        public static string ResolveEffectiveUserText(string? visiblePrompt, string? requestContent)
        {
            return IsApprovedExecutionRequest(requestContent)
                ? requestContent!.Trim()
                : (visiblePrompt ?? string.Empty).Trim();
        }
    }
}

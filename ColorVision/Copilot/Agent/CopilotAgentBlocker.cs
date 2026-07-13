using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotAgentBlockerKind
    {
        UserDecision,
        Approval,
        ToolFailure,
    }

    public sealed class CopilotAgentBlockerSnapshot
    {
        public CopilotAgentBlockerKind Kind { get; init; }

        public string Code { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public string ToolName { get; init; } = string.Empty;

        public string SourceCallKey { get; init; } = string.Empty;

        public bool RetryEligible { get; init; }

        public bool RequiresUserInput { get; init; }

        public bool IsStructurallyValid()
        {
            return Enum.IsDefined(Kind)
                && IsBoundedIdentifier(Code, 80)
                && Summary.Length is > 0 and <= CopilotAgentTaskEventJournal.MaxSummaryLength
                && Summary.All(character => !char.IsControl(character))
                && ToolName.Length <= CopilotAgentTaskEventJournal.MaxToolNameLength
                && ToolName.All(character => !char.IsControl(character))
                && (string.IsNullOrWhiteSpace(SourceCallKey) || CopilotAgentTaskEventIds.IsKey(SourceCallKey, "call", 32));
        }

        private static bool IsBoundedIdentifier(string value, int maximumLength)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Length <= maximumLength
                && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
        }
    }

    public static class CopilotAgentBlockerDetector
    {
        public static IReadOnlyList<CopilotAgentBlockerSnapshot> Detect(
            CopilotAgentTaskLedgerSnapshot taskLedger,
            IReadOnlyList<CopilotAgentStepRecord> steps,
            CopilotAgentStopReason stopReason)
        {
            ArgumentNullException.ThrowIfNull(taskLedger);
            steps ??= Array.Empty<CopilotAgentStepRecord>();
            if (taskLedger.RemainingCount == 0)
                return Array.Empty<CopilotAgentBlockerSnapshot>();
            if (stopReason is CopilotAgentStopReason.Paused or CopilotAgentStopReason.Cancelled)
                return Array.Empty<CopilotAgentBlockerSnapshot>();

            if (stopReason == CopilotAgentStopReason.AwaitingUser)
            {
                return [new CopilotAgentBlockerSnapshot
                {
                    Kind = CopilotAgentBlockerKind.UserDecision,
                    Code = "user_decision_required",
                    Summary = "The Agent needs a user decision before continuing the remaining tasks.",
                    RequiresUserInput = true,
                }];
            }

            var denied = steps.LastOrDefault(step => step?.Execution.State == CopilotToolExecutionState.Denied);
            if (denied != null)
                return [CreateToolBlocker(denied, CopilotAgentBlockerKind.Approval, "approval_denied", "A protected action was denied or expired.", true)];

            var permanentFailure = steps.LastOrDefault(step => step != null
                && step.Execution.State is CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut
                && !step.Execution.RetryEligible);
            if (permanentFailure == null)
                return Array.Empty<CopilotAgentBlockerSnapshot>();

            var failureCode = permanentFailure.Execution.FailureKind == CopilotToolFailureKind.None
                ? "tool_failure"
                : "tool_" + permanentFailure.Execution.FailureKind.ToString().ToLowerInvariant();
            var summary = permanentFailure.Execution.FailureKind == CopilotToolFailureKind.Conflict
                ? "The Agent repeated an identical tool call without producing new progress."
                : "A required tool failed and the executor did not permit an automatic retry.";
            return [CreateToolBlocker(
                permanentFailure,
                CopilotAgentBlockerKind.ToolFailure,
                failureCode,
                summary,
                false)];
        }

        private static CopilotAgentBlockerSnapshot CreateToolBlocker(
            CopilotAgentStepRecord step,
            CopilotAgentBlockerKind kind,
            string code,
            string summary,
            bool requiresUserInput)
        {
            return new CopilotAgentBlockerSnapshot
            {
                Kind = kind,
                Code = code,
                Summary = summary,
                ToolName = step.Execution.ToolName,
                SourceCallKey = CopilotAgentTaskEventIds.ForCall(step.Execution.CallId),
                RetryEligible = step.Execution.RetryEligible,
                RequiresUserInput = requiresUserInput,
            };
        }
    }
}

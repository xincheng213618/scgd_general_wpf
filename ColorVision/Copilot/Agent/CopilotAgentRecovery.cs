using System;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotAgentRecoveryMode
    {
        Resume,
        Replan,
        RetryRead,
    }

    public sealed class CopilotAgentRecoveryRequest
    {
        public CopilotAgentRecoveryMode Mode { get; init; }

        public CopilotAgentStopReason PreviousStopReason { get; init; }

        public string ToolName { get; init; } = string.Empty;

        public string SourceCallKey { get; init; } = string.Empty;

        public bool IsStructurallyValid()
        {
            if (!Enum.IsDefined(Mode)
                || PreviousStopReason is not (CopilotAgentStopReason.BudgetExhausted or CopilotAgentStopReason.TaskPassLimit))
            {
                return false;
            }

            return Mode != CopilotAgentRecoveryMode.RetryRead
                || (!string.IsNullOrWhiteSpace(ToolName)
                    && ToolName.Length <= CopilotAgentTaskEventJournal.MaxToolNameLength
                    && CopilotAgentTaskEventIds.IsKey(SourceCallKey, "call", 32));
        }
    }

    public sealed class CopilotAgentRecoveryDecision
    {
        public static CopilotAgentRecoveryDecision Unavailable { get; } = new();

        public CopilotAgentRecoveryRequest? Request { get; init; }

        public string ActionLabel { get; init; } = string.Empty;

        public string UserMessage { get; init; } = string.Empty;

        public bool IsAvailable => Request?.IsStructurallyValid() == true;
    }

    public static class CopilotAgentRecoveryPolicy
    {
        public static CopilotAgentRecoveryDecision Evaluate(
            CopilotChatMessage? message,
            CopilotAgentSessionCheckpoint? checkpoint,
            CopilotProfileConfig? profile,
            CopilotCapabilityCatalogSnapshot capabilitySnapshot)
        {
            if (message == null
                || message.IsUser
                || !message.HasIncompleteAgentTasks
                || message.AgentStopReason is not (CopilotAgentStopReason.BudgetExhausted or CopilotAgentStopReason.TaskPassLimit)
                || checkpoint?.IsStructurallyValid() != true
                || profile?.IsConfigured != true)
            {
                return CopilotAgentRecoveryDecision.Unavailable;
            }

            var checkpointStop = checkpoint.TaskEventJournal.Events
                .LastOrDefault(item => item.Type == CopilotAgentTaskEventType.RunStopped);
            if (checkpointStop == null
                || !string.Equals(checkpointStop.State, message.AgentStopReason.ToString(), StringComparison.Ordinal))
            {
                return CopilotAgentRecoveryDecision.Unavailable;
            }

            var compatibility = checkpoint.EvaluateFor(profile, capabilitySnapshot);
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.Invalid)
                return CopilotAgentRecoveryDecision.Unavailable;

            if (compatibility.Kind != CopilotAgentCheckpointCompatibilityKind.Compatible)
            {
                return CreateDecision(
                    CopilotAgentRecoveryMode.Replan,
                    message.AgentStopReason,
                    "重新规划",
                    "运行环境已变化，请基于当前能力重新规划并继续未完成的 Agent 任务。");
            }

            var retryableRead = message.AgentTraceEntries?
                .LastOrDefault(entry => entry != null
                    && entry.IsFailure
                    && entry.RetryEligible
                    && entry.Access == CopilotToolAccess.ReadOnly
                    && entry.Idempotency == CopilotToolIdempotency.Idempotent
                    && !string.IsNullOrWhiteSpace(entry.CallId)
                    && !string.IsNullOrWhiteSpace(entry.ToolName));
            if (retryableRead != null)
            {
                return CreateDecision(
                    CopilotAgentRecoveryMode.RetryRead,
                    message.AgentStopReason,
                    "重试只读检查",
                    "重新核对并恢复未完成的 Agent 任务；仅在仍有必要时重试上次失败的只读检查。",
                    retryableRead.ToolName,
                    CopilotAgentTaskEventIds.ForCall(retryableRead.CallId));
            }

            return CreateDecision(
                CopilotAgentRecoveryMode.Resume,
                message.AgentStopReason,
                "继续任务",
                "继续未完成的 Agent 任务，并先重新核对当前状态。");
        }

        private static CopilotAgentRecoveryDecision CreateDecision(
            CopilotAgentRecoveryMode mode,
            CopilotAgentStopReason stopReason,
            string actionLabel,
            string userMessage,
            string toolName = "",
            string sourceCallKey = "")
        {
            return new CopilotAgentRecoveryDecision
            {
                Request = new CopilotAgentRecoveryRequest
                {
                    Mode = mode,
                    PreviousStopReason = stopReason,
                    ToolName = toolName,
                    SourceCallKey = sourceCallKey,
                },
                ActionLabel = actionLabel,
                UserMessage = userMessage,
            };
        }
    }
}

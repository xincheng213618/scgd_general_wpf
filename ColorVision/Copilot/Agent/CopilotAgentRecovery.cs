using System;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotAgentRecoveryMode
    {
        Resume,
        Replan,
        RetryRead,
        Finalize,
    }

    public sealed class CopilotAgentRecoveryRequest
    {
        public CopilotAgentRecoveryMode Mode { get; init; }

        public CopilotAgentStopReason PreviousStopReason { get; init; }

        public string ToolName { get; init; } = string.Empty;

        public string SourceCallKey { get; init; } = string.Empty;

        public bool IsStructurallyValid()
        {
            if (!Enum.IsDefined(Mode))
                return false;

            if (Mode == CopilotAgentRecoveryMode.Finalize)
                return PreviousStopReason is (CopilotAgentStopReason.IncompleteOutput
                    or CopilotAgentStopReason.BudgetExhausted
                    or CopilotAgentStopReason.ProviderFailure
                    or CopilotAgentStopReason.Interrupted)
                    && string.IsNullOrWhiteSpace(ToolName)
                    && string.IsNullOrWhiteSpace(SourceCallKey);

            if (PreviousStopReason is not (CopilotAgentStopReason.BudgetExhausted
                or CopilotAgentStopReason.TaskPassLimit
                or CopilotAgentStopReason.Paused
                or CopilotAgentStopReason.ProviderFailure
                or CopilotAgentStopReason.Interrupted))
                return false;

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
        internal const string FinalizeUserMessage = "仅使用本轮已保存的上下文和证据重新生成最终回答，不再调用任何工具。";
        internal const string ReplanUserMessage = "运行环境已变化，请基于当前能力重新规划并继续未完成的 Agent 任务。";
        internal const string RetryReadUserMessage = "重新核对并恢复未完成的 Agent 任务；仅在仍有必要时重试上次失败的只读检查。";
        internal const string ResumeUserMessage = "继续未完成的 Agent 任务，并先重新核对当前状态。";

        public static CopilotAgentRecoveryDecision Evaluate(
            CopilotChatMessage? message,
            CopilotAgentSessionCheckpoint? checkpoint,
            CopilotProfileConfig? profile,
            CopilotCapabilityCatalogSnapshot capabilitySnapshot)
        {
            if (message == null
                || message.IsUser
                || checkpoint?.IsStructurallyValid() != true
                || profile?.IsConfigured != true)
            {
                return CopilotAgentRecoveryDecision.Unavailable;
            }

            var isFinalAnswerRecovery = message.HasRecoverableFinalAnswer;
            var isTaskRecovery = (message.HasIncompleteAgentTasks
                    || message.AgentStopReason == CopilotAgentStopReason.Paused)
                && message.AgentStopReason is (CopilotAgentStopReason.BudgetExhausted
                    or CopilotAgentStopReason.TaskPassLimit
                    or CopilotAgentStopReason.Paused
                    or CopilotAgentStopReason.ProviderFailure
                    or CopilotAgentStopReason.Interrupted);
            if (!isFinalAnswerRecovery && !isTaskRecovery)
                return CopilotAgentRecoveryDecision.Unavailable;

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

            if (isFinalAnswerRecovery)
            {
                return CreateDecision(
                    CopilotAgentRecoveryMode.Finalize,
                    message.AgentStopReason,
                    "重试最终回答",
                    FinalizeUserMessage);
            }

            if (compatibility.Kind != CopilotAgentCheckpointCompatibilityKind.Compatible)
            {
                return CreateDecision(
                    CopilotAgentRecoveryMode.Replan,
                    message.AgentStopReason,
                    "重新规划",
                    ReplanUserMessage);
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
                    RetryReadUserMessage,
                    retryableRead.ToolName,
                    CopilotAgentTaskEventIds.ForCall(retryableRead.CallId));
            }

            return CreateDecision(
                CopilotAgentRecoveryMode.Resume,
                message.AgentStopReason,
                "继续任务",
                ResumeUserMessage);
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

    internal sealed class CopilotAgentRecoveryTaskContext
    {
        public string TaskIntentText { get; init; } = string.Empty;

        public string EffectiveUserText { get; init; } = string.Empty;

        public static CopilotAgentRecoveryTaskContext Resolve(
            string? currentUserText,
            CopilotAgentRecoveryRequest? recovery,
            CopilotAgentSessionCheckpoint? checkpoint)
        {
            var current = (currentUserText ?? string.Empty).Trim();
            if (recovery == null)
            {
                return new CopilotAgentRecoveryTaskContext
                {
                    TaskIntentText = current,
                    EffectiveUserText = current,
                };
            }

            var taskIntent = (checkpoint?.TaskIntentText ?? string.Empty).Trim();
            if (taskIntent.Length == 0)
            {
                taskIntent = (checkpoint?.ConversationMemory ?? Array.Empty<CopilotRequestMessage>())
                    .Where(message => string.Equals(message.Role, "user", StringComparison.Ordinal))
                    .Select(message => (message.Content ?? string.Empty).Trim())
                    .LastOrDefault(content => content.Length > 0 && !IsGeneratedRecoveryInstruction(content))
                    ?? string.Empty;
            }
            if (taskIntent.Length == 0)
                taskIntent = current;

            return new CopilotAgentRecoveryTaskContext
            {
                TaskIntentText = taskIntent,
                EffectiveUserText = recovery.Mode == CopilotAgentRecoveryMode.Finalize
                    || string.Equals(taskIntent, current, StringComparison.Ordinal)
                    ? current
                    : $"# Original task to continue{Environment.NewLine}{taskIntent}{Environment.NewLine}{Environment.NewLine}# Recovery instruction{Environment.NewLine}{current}",
            };
        }

        private static bool IsGeneratedRecoveryInstruction(string text)
        {
            return string.Equals(text, CopilotAgentRecoveryPolicy.FinalizeUserMessage, StringComparison.Ordinal)
                || string.Equals(text, CopilotAgentRecoveryPolicy.ReplanUserMessage, StringComparison.Ordinal)
                || string.Equals(text, CopilotAgentRecoveryPolicy.RetryReadUserMessage, StringComparison.Ordinal)
                || string.Equals(text, CopilotAgentRecoveryPolicy.ResumeUserMessage, StringComparison.Ordinal);
        }
    }
}

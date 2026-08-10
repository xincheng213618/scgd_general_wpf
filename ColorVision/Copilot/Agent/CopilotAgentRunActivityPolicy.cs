using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotAgentRunActivityPolicy
    {
        public static CopilotConversationActivityState ResolveCompletionState(
            CopilotHostedAgentRun run,
            CopilotChatMessage? assistantMessage = null)
        {
            ArgumentNullException.ThrowIfNull(run);
            if (!run.Completion.IsCompleted)
                throw new InvalidOperationException("A Copilot run can be classified only after it completes.");

            if (run.Completion.IsCanceled)
            {
                return run.RunControl?.Intent == CopilotAgentControlIntent.Pause
                    ? CopilotConversationActivityState.NeedsInput
                    : CopilotConversationActivityState.None;
            }
            if (run.Completion.IsFaulted)
                return CopilotConversationActivityState.Blocked;
            if (assistantMessage?.HasRecoverableFinalAnswer == true)
                return CopilotConversationActivityState.Blocked;

            var stopReason = assistantMessage?.AgentStopReason is { } messageStopReason
                    && messageStopReason != CopilotAgentStopReason.None
                ? messageStopReason
                : run.AgentStopReason;
            return stopReason switch
            {
                CopilotAgentStopReason.AwaitingUser or CopilotAgentStopReason.Paused
                    => CopilotConversationActivityState.NeedsInput,
                CopilotAgentStopReason.Cancelled
                    => CopilotConversationActivityState.None,
                CopilotAgentStopReason.ApprovalDenied
                    or CopilotAgentStopReason.BudgetExhausted
                    or CopilotAgentStopReason.TaskPassLimit
                    or CopilotAgentStopReason.Blocked
                    or CopilotAgentStopReason.IncompleteOutput
                    or CopilotAgentStopReason.ProviderFailure
                    or CopilotAgentStopReason.Interrupted
                    => CopilotConversationActivityState.Blocked,
                _ => CopilotConversationActivityState.Ready,
            };
        }

        public static CopilotConversationActivity? CreateCompletionActivity(
            CopilotHostedAgentRun run,
            CopilotConversationRecord? conversation,
            DateTimeOffset? updatedAtUtc = null)
        {
            ArgumentNullException.ThrowIfNull(run);
            if (!run.IsAgent
                || conversation == null
                || !string.Equals(run.ConversationId, conversation.Id, StringComparison.Ordinal))
            {
                return null;
            }

            var assistantMessage = conversation.Messages.LastOrDefault(message => message != null && !message.IsUser);
            if (assistantMessage == null)
                return null;

            var state = ResolveCompletionState(run, assistantMessage);
            return state == CopilotConversationActivityState.None
                ? null
                : CopilotConversationActivity.Create(
                    state,
                    assistantMessage.Id,
                    updatedAtUtc ?? DateTimeOffset.UtcNow);
        }
    }
}

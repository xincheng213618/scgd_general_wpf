using System;
using System.Linq;

namespace ColorVision.Copilot
{
    public static class CopilotAgentTaskContinuityPolicy
    {
        public static bool HasAvailableStructuredRecovery(
            CopilotConversationRecord? conversation,
            CopilotProfileConfig? profile,
            CopilotCapabilityCatalogSnapshot capabilitySnapshot,
            CopilotToolExecutionHookRegistrySnapshot? hookSurfaceSnapshot = null)
        {
            var latestAssistant = conversation?.Messages.LastOrDefault(message => message != null && !message.IsUser);
            return HasAvailableStructuredRecovery(
                conversation,
                latestAssistant,
                profile,
                capabilitySnapshot,
                hookSurfaceSnapshot);
        }

        public static bool HasAvailableStructuredRecovery(
            CopilotConversationRecord? conversation,
            CopilotChatMessage? message,
            CopilotProfileConfig? profile,
            CopilotCapabilityCatalogSnapshot capabilitySnapshot,
            CopilotToolExecutionHookRegistrySnapshot? hookSurfaceSnapshot = null)
        {
            ArgumentNullException.ThrowIfNull(capabilitySnapshot);
            if (conversation == null || message == null || message.IsUser)
                return false;

            var latestAssistant = conversation.Messages.LastOrDefault(candidate => candidate != null && !candidate.IsUser);
            if (!ReferenceEquals(latestAssistant, message))
                return false;

            return CopilotAgentRecoveryPolicy.Evaluate(
                message,
                conversation.AgentSessionCheckpoint,
                profile,
                capabilitySnapshot,
                hookSurfaceSnapshot ?? CopilotToolExecutor.GetSharedHookSurfaceSnapshot()).IsAvailable;
        }
    }
}

using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed class CopilotChatMessageNavigationRequestedEventArgs : EventArgs
    {
        public CopilotChatMessageNavigationRequestedEventArgs(CopilotChatMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            Message = message;
        }

        public CopilotChatMessage Message { get; }
    }

    internal static class CopilotConversationPlanNavigation
    {
        public static CopilotChatMessage? FindLatestCompletedPlan(
            CopilotConversationRecord? conversation)
        {
            return conversation?.Messages
                .LastOrDefault(message => message?.HasCompletedPlan == true);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotConversationHistoryWindow
    {
        public const int DefaultMaximumMessages = 8;

        public static IReadOnlyList<CopilotRequestMessage> Select(
            IEnumerable<CopilotRequestMessage>? history,
            int maximumMessages = DefaultMaximumMessages)
        {
            if (maximumMessages <= 0)
                return Array.Empty<CopilotRequestMessage>();

            var messages = (history ?? Array.Empty<CopilotRequestMessage>())
                .Where(message => message != null && !string.IsNullOrWhiteSpace(message.Content))
                .ToArray();
            if (messages.Length <= maximumMessages)
                return messages;

            var recent = messages.TakeLast(maximumMessages).ToArray();
            if (maximumMessages == 1)
                return recent;

            var initialUserGoal = messages.FirstOrDefault(message =>
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (initialUserGoal == null || recent.Contains(initialUserGoal))
                return recent;

            return new[] { initialUserGoal }
                .Concat(messages.TakeLast(maximumMessages - 1))
                .ToArray();
        }
    }
}

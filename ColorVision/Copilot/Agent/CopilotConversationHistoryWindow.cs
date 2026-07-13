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
                .Select(Normalize)
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .ToArray();
            if (messages.Length <= maximumMessages)
                return messages;

            if (maximumMessages == 1)
                return messages.TakeLast(1).ToArray();

            var initialUserGoalIndex = Array.FindIndex(messages, message =>
                string.Equals(message.Role, "user", StringComparison.Ordinal));
            var recent = SelectRecentTurnWindow(messages, initialUserGoalIndex >= 0 ? maximumMessages - 1 : maximumMessages);
            if (initialUserGoalIndex < 0 || recent.Contains(messages[initialUserGoalIndex]))
                return recent;

            return new[] { messages[initialUserGoalIndex] }
                .Concat(recent)
                .ToArray();
        }

        private static CopilotRequestMessage Normalize(CopilotRequestMessage message)
        {
            var role = message.Role?.Trim().ToLowerInvariant() switch
            {
                "user" => "user",
                "assistant" => "assistant",
                _ => string.Empty,
            };
            return role.Length == 0
                ? default
                : new CopilotRequestMessage(role, (message.Content ?? string.Empty).Trim());
        }

        private static CopilotRequestMessage[] SelectRecentTurnWindow(
            CopilotRequestMessage[] messages,
            int maximumMessages)
        {
            var startIndex = Math.Max(0, messages.Length - maximumMessages);
            if (startIndex > 0)
            {
                var firstUserIndex = -1;
                for (var index = startIndex; index < messages.Length; index++)
                {
                    if (!string.Equals(messages[index].Role, "user", StringComparison.Ordinal))
                        continue;
                    firstUserIndex = index;
                    break;
                }
                if (firstUserIndex >= 0)
                    startIndex = firstUserIndex;
            }
            return messages.Skip(startIndex).ToArray();
        }
    }
}

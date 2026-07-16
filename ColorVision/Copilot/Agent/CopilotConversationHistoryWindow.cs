using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public readonly record struct CopilotConversationHistorySelection(
        CopilotRequestMessage[] Messages,
        int SourceMessageCount,
        int SourceCharacters,
        int RetainedCharacters)
    {
        public bool WasReduced => Messages.Length < SourceMessageCount || RetainedCharacters < SourceCharacters;
    }

    public readonly record struct CopilotConversationHistoryLimits(
        int MaximumMessages,
        int MaximumCharacters,
        int MaximumContentCharacters);

    internal static class CopilotConversationHistoryWindow
    {
        public const int HistoryContextPercent = 50;
        public const int MaximumMessageLimit = 512;
        public const int MaximumContentCharacterLimit = 262_144;

        private const int EstimatedCharactersPerToken = 4;
        private const int MinimumMessages = 8;
        private const int TokensPerMessageSlot = 1_024;
        private const int MinimumContentCharacters = 8_000;

        private const string TruncationSuffix = "\n...<conversation history truncated>";

        public static CopilotConversationHistoryLimits ResolveLimits(int contextWindowTokens, int maxOutputTokens)
        {
            var boundedContextTokens = Math.Clamp(
                contextWindowTokens,
                CopilotAgentTokenBudget.MinimumContextWindowTokens,
                CopilotAgentTokenBudget.MaximumContextWindowTokens);
            var boundedOutputTokens = Math.Clamp(maxOutputTokens, 32, CopilotProfileConfig.DefaultMaxTokens);
            var inputTokens = Math.Max(1, boundedContextTokens - boundedOutputTokens);
            var historyTokens = Math.Max(1, (long)inputTokens * HistoryContextPercent / 100);
            var maximumCharacters = (int)Math.Clamp(
                historyTokens * EstimatedCharactersPerToken,
                MinimumContentCharacters,
                int.MaxValue);
            var maximumMessages = (int)Math.Clamp(
                historyTokens / TokensPerMessageSlot,
                MinimumMessages,
                MaximumMessageLimit);
            var maximumContentCharacters = Math.Clamp(
                maximumCharacters / 8,
                MinimumContentCharacters,
                MaximumContentCharacterLimit);
            return new CopilotConversationHistoryLimits(maximumMessages, maximumCharacters, maximumContentCharacters);
        }

        public static IReadOnlyList<CopilotRequestMessage> Select(
            IEnumerable<CopilotRequestMessage>? history,
            CopilotConversationHistoryLimits limits)
        {
            return SelectWithDiagnostics(history, limits).Messages;
        }

        public static IReadOnlyList<CopilotRequestMessage> Select(
            IEnumerable<CopilotRequestMessage>? history,
            int maximumMessages,
            int maximumCharacters,
            int maximumContentCharacters)
        {
            return SelectWithDiagnostics(history, maximumMessages, maximumCharacters, maximumContentCharacters).Messages;
        }

        public static CopilotConversationHistorySelection SelectWithDiagnostics(
            IEnumerable<CopilotRequestMessage>? history,
            CopilotConversationHistoryLimits limits)
        {
            return SelectWithDiagnostics(
                history,
                limits.MaximumMessages,
                limits.MaximumCharacters,
                limits.MaximumContentCharacters);
        }

        public static CopilotConversationHistorySelection SelectWithDiagnostics(
            IEnumerable<CopilotRequestMessage>? history,
            int maximumMessages,
            int maximumCharacters,
            int maximumContentCharacters)
        {
            if (maximumMessages <= 0 || maximumCharacters <= 0 || maximumContentCharacters <= 0)
                return new CopilotConversationHistorySelection([], 0, 0, 0);

            var source = (history ?? Array.Empty<CopilotRequestMessage>())
                .Select(Normalize)
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .ToArray();
            var sourceCharacters = SaturatingCharacterCount(source);
            if (source.Length == 0)
                return new CopilotConversationHistorySelection([], 0, sourceCharacters, 0);

            var perMessageLimit = Math.Min(maximumCharacters, maximumContentCharacters);
            var selected = SelectByMessageCount(source, maximumMessages)
                .Select(message => new CopilotRequestMessage(message.Role, TruncateContent(message.Content, perMessageLimit)))
                .ToList();
            ReduceToCharacterBudget(selected, maximumCharacters);

            var messages = selected.ToArray();
            return new CopilotConversationHistorySelection(
                messages,
                source.Length,
                sourceCharacters,
                SaturatingCharacterCount(messages));
        }

        private static CopilotRequestMessage[] SelectByMessageCount(
            CopilotRequestMessage[] messages,
            int maximumMessages)
        {
            var initialUserGoalIndex = Array.FindIndex(messages, message =>
                string.Equals(message.Role, "user", StringComparison.Ordinal));
            if (initialUserGoalIndex > 0)
            {
                messages = messages[initialUserGoalIndex..];
                initialUserGoalIndex = 0;
            }
            if (messages.Length <= maximumMessages)
                return messages;
            if (maximumMessages == 1)
                return messages.TakeLast(1).ToArray();

            var recent = SelectRecentTurnWindow(messages, initialUserGoalIndex >= 0 ? maximumMessages - 1 : maximumMessages);
            if (initialUserGoalIndex < 0 || recent.Contains(messages[initialUserGoalIndex]))
                return recent;

            return new[] { messages[initialUserGoalIndex] }
                .Concat(recent)
                .ToArray();
        }

        private static void ReduceToCharacterBudget(List<CopilotRequestMessage> messages, int maximumCharacters)
        {
            while (SaturatingCharacterCount(messages) > maximumCharacters && TryRemoveOldestUnprotectedMessage(messages))
            {
            }

            if (SaturatingCharacterCount(messages) <= maximumCharacters)
                return;

            var perMessageBudget = Math.Max(1, maximumCharacters / messages.Count);
            var remainder = Math.Max(0, maximumCharacters - perMessageBudget * messages.Count);
            for (var index = 0; index < messages.Count; index++)
            {
                var budget = perMessageBudget + (index >= messages.Count - remainder ? 1 : 0);
                messages[index] = new CopilotRequestMessage(
                    messages[index].Role,
                    TruncateContent(messages[index].Content, budget));
            }
        }

        private static bool TryRemoveOldestUnprotectedMessage(List<CopilotRequestMessage> messages)
        {
            if (messages.Count <= 1)
                return false;

            var lastUserIndex = messages.FindLastIndex(message =>
                string.Equals(message.Role, "user", StringComparison.Ordinal));
            if (lastUserIndex < 0)
            {
                messages.RemoveAt(0);
                return true;
            }

            if (lastUserIndex > 1)
            {
                var removeCount = lastUserIndex > 2
                    && string.Equals(messages[1].Role, "user", StringComparison.Ordinal)
                    && string.Equals(messages[2].Role, "assistant", StringComparison.Ordinal)
                        ? 2
                        : 1;
                messages.RemoveRange(1, removeCount);
                return true;
            }

            if (lastUserIndex == 0 && messages.Count > 2)
            {
                messages.RemoveAt(1);
                return true;
            }

            return false;
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

        private static string TruncateContent(string value, int maximumCharacters)
        {
            if (value.Length <= maximumCharacters)
                return value;
            if (maximumCharacters <= TruncationSuffix.Length)
                return SafePrefix(value, maximumCharacters);

            return SafePrefix(value, maximumCharacters - TruncationSuffix.Length).TrimEnd() + TruncationSuffix;
        }

        private static string SafePrefix(string value, int length)
        {
            var retainedLength = Math.Clamp(length, 0, value.Length);
            if (retainedLength > 0 && retainedLength < value.Length && char.IsHighSurrogate(value[retainedLength - 1]))
                retainedLength--;
            return value[..retainedLength];
        }

        private static int SaturatingCharacterCount(IEnumerable<CopilotRequestMessage> messages)
        {
            var count = messages.Sum(message => (long)message.Content.Length);
            return (int)Math.Min(int.MaxValue, count);
        }
    }
}

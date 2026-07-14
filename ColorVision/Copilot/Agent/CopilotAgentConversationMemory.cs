using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public static class CopilotAgentConversationMemory
    {
        public static IReadOnlyList<CopilotRequestMessage> Merge(
            IReadOnlyList<CopilotRequestMessage>? previousMemory,
            IEnumerable<CopilotRequestMessage>? visibleHistory,
            string currentUserText,
            string currentAssistantText)
        {
            var merged = Normalize(previousMemory).ToList();
            var seenHistory = merged
                .Select(CreateKey)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var message in Normalize(visibleHistory))
            {
                if (seenHistory.Add(CreateKey(message)))
                    merged.Add(message);
            }

            AppendCurrent(merged, "user", currentUserText);
            AppendCurrent(merged, "assistant", currentAssistantText);
            return SelectBounded(merged);
        }

        public static IReadOnlyList<CopilotRequestMessage> MergeIntoPreparedPrompt(
            IReadOnlyList<CopilotRequestMessage>? previousMemory,
            IReadOnlyList<CopilotRequestMessage> preparedMessages)
        {
            if (preparedMessages == null || preparedMessages.Count == 0)
                return Normalize(previousMemory);

            var history = Merge(
                previousMemory,
                preparedMessages.Take(preparedMessages.Count - 1),
                string.Empty,
                string.Empty);
            return history.Append(preparedMessages[^1]).ToArray();
        }

        public static IReadOnlyList<CopilotRequestMessage> SelectUnseenVisibleTail(
            IReadOnlyList<CopilotRequestMessage>? previousMemory,
            IEnumerable<CopilotRequestMessage>? visibleHistory)
        {
            var previous = Normalize(previousMemory);
            var visible = Normalize(visibleHistory);
            if (visible.Length == 0 || previous.Length == 0)
                return visible;

            var commonPrefixLength = 0;
            while (commonPrefixLength < previous.Length
                && commonPrefixLength < visible.Length
                && AreEqual(previous[commonPrefixLength], visible[commonPrefixLength]))
            {
                commonPrefixLength++;
            }

            var previousTail = previous.AsSpan(commonPrefixLength);
            var visibleTail = visible.AsSpan(commonPrefixLength);
            var maximumOverlap = Math.Min(previousTail.Length, visibleTail.Length);
            for (var overlap = maximumOverlap; overlap > 0; overlap--)
            {
                if (previousTail[^overlap..].SequenceEqual(visibleTail[..overlap], CopilotRequestMessageComparer.Instance))
                    return visibleTail[overlap..].ToArray();
            }

            return visibleTail.ToArray();
        }

        private static CopilotRequestMessage[] Normalize(IEnumerable<CopilotRequestMessage>? messages)
        {
            return (messages ?? Array.Empty<CopilotRequestMessage>())
                .Select(Normalize)
                .Where(message => !string.IsNullOrEmpty(message.Content))
                .ToArray();
        }

        private static CopilotRequestMessage Normalize(CopilotRequestMessage message)
        {
            var role = string.Equals(message.Role?.Trim(), "assistant", StringComparison.OrdinalIgnoreCase)
                ? "assistant"
                : string.Equals(message.Role?.Trim(), "user", StringComparison.OrdinalIgnoreCase)
                    ? "user"
                    : string.Empty;
            if (role.Length == 0)
                return default;

            var content = (message.Content ?? string.Empty).Trim();
            if (content.Length > CopilotAgentSessionCheckpoint.MaxConversationMemoryContentLength)
            {
                const string suffix = "\n...<conversation memory truncated>";
                content = content[..(CopilotAgentSessionCheckpoint.MaxConversationMemoryContentLength - suffix.Length)] + suffix;
            }
            return new CopilotRequestMessage(role, content);
        }

        private static void AppendCurrent(List<CopilotRequestMessage> messages, string role, string content)
        {
            var normalized = Normalize(new CopilotRequestMessage(role, content));
            if (string.IsNullOrEmpty(normalized.Content))
                return;
            if (messages.Count == 0 || !string.Equals(CreateKey(messages[^1]), CreateKey(normalized), StringComparison.Ordinal))
                messages.Add(normalized);
        }

        private static CopilotRequestMessage[] SelectBounded(IReadOnlyList<CopilotRequestMessage> messages)
        {
            var selected = CopilotConversationHistoryWindow
                .Select(messages, CopilotAgentSessionCheckpoint.MaxConversationMemoryMessages)
                .ToList();
            while (selected.Sum(message => message.Content.Length) > CopilotAgentSessionCheckpoint.MaxConversationMemoryCharacters
                && selected.Count > 2)
            {
                selected.RemoveAt(1);
            }
            return selected.ToArray();
        }

        private static string CreateKey(CopilotRequestMessage message) => message.Role + "\n" + message.Content;

        private static bool AreEqual(CopilotRequestMessage left, CopilotRequestMessage right)
        {
            return string.Equals(left.Role, right.Role, StringComparison.Ordinal)
                && string.Equals(left.Content, right.Content, StringComparison.Ordinal);
        }

        private sealed class CopilotRequestMessageComparer : IEqualityComparer<CopilotRequestMessage>
        {
            public static CopilotRequestMessageComparer Instance { get; } = new();

            public bool Equals(CopilotRequestMessage left, CopilotRequestMessage right) => AreEqual(left, right);

            public int GetHashCode(CopilotRequestMessage message) => HashCode.Combine(message.Role, message.Content);
        }
    }
}

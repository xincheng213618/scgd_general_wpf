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
            string currentAssistantText,
            IEnumerable<string>? currentUserFollowUps = null)
        {
            var merged = MergeChronologically(
                    Normalize(previousMemory),
                    Normalize(visibleHistory))
                .ToList();

            AppendCurrent(merged, "user", currentUserText);
            AppendUserFollowUps(merged, currentUserFollowUps);
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

        internal static IReadOnlyList<string> SelectBoundedUserFollowUps(
            IEnumerable<string>? followUps)
        {
            return SelectBounded((followUps ?? Array.Empty<string>())
                    .Select(content => Normalize(new CopilotRequestMessage("user", content)))
                    .Where(message => !string.IsNullOrEmpty(message.Content))
                    .ToArray())
                .Select(message => message.Content)
                .ToArray();
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

        private static CopilotRequestMessage[] MergeChronologically(
            CopilotRequestMessage[] previousMemory,
            CopilotRequestMessage[] visibleHistory)
        {
            if (previousMemory.Length == 0)
                return visibleHistory.ToArray();
            if (visibleHistory.Length == 0)
                return previousMemory.ToArray();

            // The suffix LCS table lets the merge preserve both input orders while
            // interleaving checkpoint-only injected messages with visible-only history.
            var commonSuffixLengths = new int[
                previousMemory.Length + 1,
                visibleHistory.Length + 1];
            for (var previousIndex = previousMemory.Length - 1; previousIndex >= 0; previousIndex--)
            {
                for (var visibleIndex = visibleHistory.Length - 1; visibleIndex >= 0; visibleIndex--)
                {
                    commonSuffixLengths[previousIndex, visibleIndex] =
                        AreEqual(previousMemory[previousIndex], visibleHistory[visibleIndex])
                            ? commonSuffixLengths[previousIndex + 1, visibleIndex + 1] + 1
                            : Math.Max(
                                commonSuffixLengths[previousIndex + 1, visibleIndex],
                                commonSuffixLengths[previousIndex, visibleIndex + 1]);
                }
            }

            var merged = new List<CopilotRequestMessage>(
                previousMemory.Length + visibleHistory.Length);
            var previousCursor = 0;
            var visibleCursor = 0;
            while (previousCursor < previousMemory.Length
                && visibleCursor < visibleHistory.Length)
            {
                if (AreEqual(
                    previousMemory[previousCursor],
                    visibleHistory[visibleCursor]))
                {
                    merged.Add(previousMemory[previousCursor]);
                    previousCursor++;
                    visibleCursor++;
                    continue;
                }

                if (commonSuffixLengths[previousCursor + 1, visibleCursor]
                    >= commonSuffixLengths[previousCursor, visibleCursor + 1])
                {
                    // Keep checkpoint-only input before the next shared visible message.
                    merged.Add(previousMemory[previousCursor++]);
                }
                else
                {
                    merged.Add(visibleHistory[visibleCursor++]);
                }
            }

            while (previousCursor < previousMemory.Length)
                merged.Add(previousMemory[previousCursor++]);
            while (visibleCursor < visibleHistory.Length)
                merged.Add(visibleHistory[visibleCursor++]);
            return merged.ToArray();
        }

        private static void AppendCurrent(List<CopilotRequestMessage> messages, string role, string content)
        {
            var normalized = Normalize(new CopilotRequestMessage(role, content));
            if (string.IsNullOrEmpty(normalized.Content))
                return;
            if (messages.Count == 0 || !string.Equals(CreateKey(messages[^1]), CreateKey(normalized), StringComparison.Ordinal))
                messages.Add(normalized);
        }

        private static void AppendUserFollowUps(
            List<CopilotRequestMessage> messages,
            IEnumerable<string>? followUps)
        {
            foreach (var followUp in followUps ?? Array.Empty<string>())
            {
                var normalized = Normalize(new CopilotRequestMessage("user", followUp));
                if (!string.IsNullOrEmpty(normalized.Content))
                    messages.Add(normalized);
            }
        }

        private static CopilotRequestMessage[] SelectBounded(IReadOnlyList<CopilotRequestMessage> messages)
        {
            return CopilotConversationHistoryWindow.Select(
                    messages,
                    CopilotAgentSessionCheckpoint.MaxConversationMemoryMessages,
                    CopilotAgentSessionCheckpoint.MaxConversationMemoryCharacters,
                    CopilotAgentSessionCheckpoint.MaxConversationMemoryContentLength)
                .ToArray();
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

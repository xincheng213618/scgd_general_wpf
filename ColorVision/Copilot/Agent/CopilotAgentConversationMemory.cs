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

            // Checkpoint memory is more tightly bounded than the visible request
            // history, so it can contain the initial goal plus only a recent tail.
            // Align both ordered sequences and resume after their last shared item.
            var commonSuffixLengths = BuildCommonSuffixLengths(previous, visible);

            var previousCursor = 0;
            var visibleCursor = 0;
            var lastSharedVisibleIndex = -1;
            while (previousCursor < previous.Length && visibleCursor < visible.Length)
            {
                if (AreEqual(previous[previousCursor], visible[visibleCursor]))
                {
                    lastSharedVisibleIndex = visibleCursor;
                    previousCursor++;
                    visibleCursor++;
                    continue;
                }

                if (commonSuffixLengths[previousCursor + 1, visibleCursor]
                    >= commonSuffixLengths[previousCursor, visibleCursor + 1])
                {
                    // Prefer discarding checkpoint-only messages on a tie so an
                    // identical later visible turn is not mistaken for one seen.
                    previousCursor++;
                }
                else
                {
                    visibleCursor++;
                }
            }

            return visible[(lastSharedVisibleIndex + 1)..];
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
            return new CopilotRequestMessage(role, content)
            {
                IsSteering = message.IsSteering && role == "user",
            };
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
            var commonSuffixLengths = BuildCommonSuffixLengths(previousMemory, visibleHistory);

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
                var normalized = Normalize(new CopilotRequestMessage("user", followUp)
                {
                    IsSteering = true,
                });
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

        private static string CreateKey(CopilotRequestMessage message) =>
            (message.IsSteering ? "steering" : "message")
            + "\n"
            + message.Role
            + "\n"
            + message.Content;

        private static bool AreEqual(CopilotRequestMessage left, CopilotRequestMessage right)
        {
            return string.Equals(left.Role, right.Role, StringComparison.Ordinal)
                && string.Equals(left.Content, right.Content, StringComparison.Ordinal)
                && left.IsSteering == right.IsSteering;
        }

        private static int[,] BuildCommonSuffixLengths(
            CopilotRequestMessage[] previous,
            CopilotRequestMessage[] visible)
        {
            var commonSuffixLengths = new int[previous.Length + 1, visible.Length + 1];
            for (var previousIndex = previous.Length - 1; previousIndex >= 0; previousIndex--)
            {
                for (var visibleIndex = visible.Length - 1; visibleIndex >= 0; visibleIndex--)
                {
                    commonSuffixLengths[previousIndex, visibleIndex] =
                        AreEqual(previous[previousIndex], visible[visibleIndex])
                            ? commonSuffixLengths[previousIndex + 1, visibleIndex + 1] + 1
                            : Math.Max(
                                commonSuffixLengths[previousIndex + 1, visibleIndex],
                                commonSuffixLengths[previousIndex, visibleIndex + 1]);
                }
            }

            return commonSuffixLengths;
        }
    }
}

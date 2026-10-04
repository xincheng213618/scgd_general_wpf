using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public static class CopilotAgentConversationMemory
    {
        private const string TruncationSuffix = "\n...<conversation memory truncated>";
        private const string HistoryTruncationSuffix = "\n...<conversation history truncated>";

        private readonly record struct ComparisonKey(
            CopilotRequestMessage Message,
            CopilotRequestMessage BoundedMessage,
            bool IsTruncated);

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
            var previousKeys = CreateComparisonKeys(previous);
            var visibleKeys = CreateComparisonKeys(visible);
            var commonSuffixLengths = BuildCommonSuffixLengths(previousKeys, visibleKeys);

            var previousCursor = 0;
            var visibleCursor = 0;
            var lastSharedVisibleIndex = -1;
            while (previousCursor < previous.Length && visibleCursor < visible.Length)
            {
                if (AreEqual(previousKeys[previousCursor], visibleKeys[visibleCursor]))
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
                var (body, terminalSuffix) = CopilotChatMessage.SplitModelTerminalEvidence(role, content);
                var ending = TruncationSuffix + (terminalSuffix.Length == 0 ? string.Empty : "\n\n" + terminalSuffix);
                var retainedLength = Math.Min(body.Length,
                    Math.Max(0, CopilotAgentSessionCheckpoint.MaxConversationMemoryContentLength - ending.Length));
                if (retainedLength > 0
                    && retainedLength < body.Length
                    && char.IsHighSurrogate(body[retainedLength - 1])
                    && char.IsLowSurrogate(body[retainedLength]))
                {
                    retainedLength--;
                }
                content = body[..retainedLength] + ending;
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
            var previousKeys = CreateComparisonKeys(previousMemory);
            var visibleKeys = CreateComparisonKeys(visibleHistory);
            var commonSuffixLengths = BuildCommonSuffixLengths(previousKeys, visibleKeys);

            var merged = new List<CopilotRequestMessage>(
                previousMemory.Length + visibleHistory.Length);
            var previousCursor = 0;
            var visibleCursor = 0;
            while (previousCursor < previousMemory.Length
                && visibleCursor < visibleHistory.Length)
            {
                if (AreEqual(
                    previousKeys[previousCursor],
                    visibleKeys[visibleCursor]))
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

        private static ComparisonKey[] CreateComparisonKeys(CopilotRequestMessage[] messages)
        {
            // Match the checkpoint's weighted content limit without shortening the
            // visible messages returned to the caller. Keep complete messages distinct;
            // bounded matching is only valid when one side carries a known truncation.
            return messages.Select(message => new ComparisonKey(
                message,
                CopilotConversationHistoryWindow.Select(
                    [message],
                    1,
                    CopilotAgentSessionCheckpoint.MaxConversationMemoryContentLength,
                    CopilotAgentSessionCheckpoint.MaxConversationMemoryContentLength)[0],
                IsTruncated(message))).ToArray();
        }

        private static bool IsTruncated(CopilotRequestMessage message)
        {
            var (body, _) = CopilotChatMessage.SplitModelTerminalEvidence(message.Role, message.Content);
            body = body.TrimEnd();
            return body.EndsWith(TruncationSuffix, StringComparison.Ordinal)
                || body.EndsWith(HistoryTruncationSuffix, StringComparison.Ordinal);
        }

        private static bool AreEqual(ComparisonKey left, ComparisonKey right)
        {
            return AreMessagesEqual(left.Message, right.Message)
                || ((left.IsTruncated || right.IsTruncated)
                    && AreMessagesEqual(left.BoundedMessage, right.BoundedMessage));
        }

        private static bool AreMessagesEqual(CopilotRequestMessage left, CopilotRequestMessage right)
        {
            return string.Equals(left.Role, right.Role, StringComparison.Ordinal)
                && string.Equals(left.Content, right.Content, StringComparison.Ordinal)
                && left.IsSteering == right.IsSteering;
        }

        private static int[,] BuildCommonSuffixLengths(
            ComparisonKey[] previous,
            ComparisonKey[] visible)
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

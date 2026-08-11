using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotPendingSteeringRecoveryRecord
    {
        public string MessageId { get; set; } = string.Empty;

        public string TaskId { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public DateTimeOffset AcceptedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        internal bool TryGetNormalized(
            out CopilotSteeringMessageSnapshot message,
            out string taskId,
            out DateTimeOffset acceptedAtUtc)
        {
            var messageId = (MessageId ?? string.Empty).Trim();
            taskId = (TaskId ?? string.Empty).Trim();
            var text = (Text ?? string.Empty).Trim();
            acceptedAtUtc = AcceptedAtUtc == default ? DateTimeOffset.UtcNow : AcceptedAtUtc;
            message = new CopilotSteeringMessageSnapshot(messageId, text);
            return messageId.Length is > 0 and <= CopilotSteeringMessagePolicy.MaximumIdentifierCharacters
                && taskId.Length is > 0 and <= CopilotSteeringMessagePolicy.MaximumIdentifierCharacters
                && text.Length is > 0 and <= CopilotSteeringMessagePolicy.MaximumMessageCharacters;
        }
    }

    internal static class CopilotSteeringRecovery
    {
        internal const int MaximumPendingRecoveryRecords = 16;
        internal const int MaximumPendingRecoveryCharacters = 256_000;
        private const string RecoveryHeading = "以下运行中指令尚未送达，请检查后重新发送：";

        internal static bool TrackPending(
            CopilotConversationRecord conversation,
            string taskId,
            CopilotSteeringMessageSnapshot message,
            DateTimeOffset acceptedAtUtc)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            var candidate = new CopilotPendingSteeringRecoveryRecord
            {
                MessageId = message?.MessageId ?? string.Empty,
                TaskId = taskId ?? string.Empty,
                Text = message?.Text ?? string.Empty,
                AcceptedAtUtc = acceptedAtUtc,
            };
            if (!candidate.TryGetNormalized(out var normalizedMessage, out var normalizedTaskId, out var normalizedAcceptedAtUtc))
                return false;

            conversation.PendingSteeringRecoveries ??= new ObservableCollection<CopilotPendingSteeringRecoveryRecord>();
            if (conversation.PendingSteeringRecoveries.Any(record => string.Equals(
                    record?.MessageId,
                    normalizedMessage.MessageId,
                    StringComparison.Ordinal)))
            {
                return false;
            }
            if (conversation.PendingSteeringRecoveries.Count >= MaximumPendingRecoveryRecords
                || conversation.PendingSteeringRecoveries.Sum(record => record?.Text?.Length ?? 0)
                    + normalizedMessage.Text.Length > MaximumPendingRecoveryCharacters)
            {
                return false;
            }

            conversation.PendingSteeringRecoveries.Add(new CopilotPendingSteeringRecoveryRecord
            {
                MessageId = normalizedMessage.MessageId,
                TaskId = normalizedTaskId,
                Text = normalizedMessage.Text,
                AcceptedAtUtc = normalizedAcceptedAtUtc,
            });
            return true;
        }

        internal static bool RemovePending(
            CopilotConversationRecord conversation,
            IEnumerable<CopilotSteeringMessageSnapshot>? messages)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            if (conversation.PendingSteeringRecoveries == null
                || conversation.PendingSteeringRecoveries.Count == 0)
            {
                return false;
            }

            var messageIds = (messages ?? Array.Empty<CopilotSteeringMessageSnapshot>())
                .Select(message => (message?.MessageId ?? string.Empty).Trim())
                .Where(messageId => messageId.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
            if (messageIds.Count == 0)
                return false;

            var changed = false;
            for (var index = conversation.PendingSteeringRecoveries.Count - 1; index >= 0; index--)
            {
                if (!messageIds.Contains(conversation.PendingSteeringRecoveries[index]?.MessageId ?? string.Empty))
                    continue;

                conversation.PendingSteeringRecoveries.RemoveAt(index);
                changed = true;
            }
            return changed;
        }

        internal static bool RestoreToDraft(
            CopilotConversationRecord conversation,
            IEnumerable<string>? messages)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            var recoveryMessages = CopilotSteeringMessagePolicy.SelectForRecovery(messages);
            if (recoveryMessages.Count == 0)
                return false;

            return AppendToDraft(conversation, recoveryMessages);
        }

        internal static bool RestoreMessagesToDraft(
            CopilotConversationRecord conversation,
            IEnumerable<CopilotSteeringMessageSnapshot>? messages)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            var recoveryMessages = CopilotSteeringMessagePolicy
                .SelectForRecovery(messages)
                .Select(message => message.Text)
                .ToArray();
            return recoveryMessages.Length > 0
                && AppendToDraft(conversation, recoveryMessages);
        }

        internal static bool RestorePendingMessagesToDraft(
            CopilotConversationRecord conversation,
            IEnumerable<CopilotSteeringMessageSnapshot>? messages)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            if (conversation.PendingSteeringRecoveries == null
                || conversation.PendingSteeringRecoveries.Count == 0)
            {
                return false;
            }

            var messageIds = (messages ?? Array.Empty<CopilotSteeringMessageSnapshot>())
                .Select(message => (message?.MessageId ?? string.Empty).Trim())
                .Where(messageId => messageId.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
            if (messageIds.Count == 0)
                return false;

            var matchedRecords = conversation.PendingSteeringRecoveries
                .Where(record => record != null && messageIds.Contains(record.MessageId ?? string.Empty))
                .ToArray();
            if (matchedRecords.Length == 0
                || !AppendToDraft(conversation, matchedRecords.Select(record => record.Text).ToArray()))
            {
                return false;
            }

            foreach (var record in matchedRecords)
                conversation.PendingSteeringRecoveries.Remove(record);
            return true;
        }

        internal static bool AreNewMessagesIncludedInCheckpoint(
            CopilotAgentSessionCheckpoint? previousCheckpoint,
            CopilotAgentSessionCheckpoint? checkpoint,
            IEnumerable<CopilotSteeringMessageSnapshot>? messages)
        {
            if (checkpoint == null)
                return false;

            var expectedCounts = (messages ?? Array.Empty<CopilotSteeringMessageSnapshot>())
                .Select(message => (message?.Text ?? string.Empty).Trim())
                .Where(text => text.Length > 0)
                .GroupBy(text => text, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            if (expectedCounts.Count == 0)
                return true;

            var previousCounts = CountCheckpointedSteering(previousCheckpoint);
            var checkpointCounts = CountCheckpointedSteering(checkpoint);
            return expectedCounts.All(expected =>
                checkpointCounts.GetValueOrDefault(expected.Key)
                    - previousCounts.GetValueOrDefault(expected.Key)
                >= expected.Value);
        }

        internal static bool RestorePendingToDrafts(CopilotChatState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            state.RecoveredSteeringCount = 0;
            var changed = false;
            foreach (var conversation in state.Conversations ?? new ObservableCollection<CopilotConversationRecord>())
            {
                if (conversation == null)
                    continue;

                changed |= NormalizePendingRecords(conversation);
                var pending = conversation.PendingSteeringRecoveries;
                if (pending == null || pending.Count == 0)
                    continue;

                var recoveryMessages = pending
                    .Select(record => record.Text)
                    .ToArray();
                if (AppendToDraft(conversation, recoveryMessages))
                    state.RecoveredSteeringCount += recoveryMessages.Length;
                pending.Clear();
                changed = true;
            }
            return changed;
        }

        internal static bool NormalizePendingRecords(CopilotConversationRecord conversation)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            if (conversation.PendingSteeringRecoveries == null)
            {
                conversation.PendingSteeringRecoveries = new ObservableCollection<CopilotPendingSteeringRecoveryRecord>();
                return true;
            }

            var normalized = new List<CopilotPendingSteeringRecoveryRecord>(MaximumPendingRecoveryRecords);
            var seenMessageIds = new HashSet<string>(StringComparer.Ordinal);
            var characterCount = 0;
            foreach (var record in conversation.PendingSteeringRecoveries)
            {
                if (record == null
                    || !record.TryGetNormalized(out var message, out var taskId, out var acceptedAtUtc)
                    || !seenMessageIds.Add(message.MessageId)
                    || normalized.Count >= MaximumPendingRecoveryRecords
                    || characterCount + message.Text.Length > MaximumPendingRecoveryCharacters)
                {
                    continue;
                }

                normalized.Add(new CopilotPendingSteeringRecoveryRecord
                {
                    MessageId = message.MessageId,
                    TaskId = taskId,
                    Text = message.Text,
                    AcceptedAtUtc = acceptedAtUtc,
                });
                characterCount += message.Text.Length;
            }
            if (normalized.Count == conversation.PendingSteeringRecoveries.Count
                && normalized.Zip(conversation.PendingSteeringRecoveries).All(pair =>
                    string.Equals(pair.First.MessageId, pair.Second.MessageId, StringComparison.Ordinal)
                    && string.Equals(pair.First.TaskId, pair.Second.TaskId, StringComparison.Ordinal)
                    && string.Equals(pair.First.Text, pair.Second.Text, StringComparison.Ordinal)
                    && pair.First.AcceptedAtUtc == pair.Second.AcceptedAtUtc))
            {
                return false;
            }

            conversation.PendingSteeringRecoveries.Clear();
            foreach (var record in normalized)
                conversation.PendingSteeringRecoveries.Add(record);
            return true;
        }

        private static Dictionary<string, int> CountCheckpointedSteering(
            CopilotAgentSessionCheckpoint? checkpoint)
        {
            return (checkpoint?.ConversationMemory ?? Array.Empty<CopilotRequestMessage>())
                .Where(message => message.IsSteering && message.Role == "user")
                .Select(message => (message.Content ?? string.Empty).Trim())
                .Where(content => content.Length > 0)
                .GroupBy(content => content, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        }

        private static bool AppendToDraft(
            CopilotConversationRecord conversation,
            IReadOnlyList<string> recoveryMessages)
        {
            if (recoveryMessages.Count == 0)
                return false;

            var existingDraft = (conversation.DraftText ?? string.Empty).TrimEnd();
            var restoredDraft = string.IsNullOrWhiteSpace(existingDraft) && recoveryMessages.Count == 1
                ? recoveryMessages[0]
                : FormatRecoveryNotice(recoveryMessages);
            conversation.DraftText = string.IsNullOrWhiteSpace(existingDraft)
                ? restoredDraft
                : existingDraft + Environment.NewLine + Environment.NewLine + restoredDraft;
            return true;
        }

        internal static string FormatRecoveryNotice(IReadOnlyList<string> messages)
        {
            ArgumentNullException.ThrowIfNull(messages);
            if (messages.Count == 0)
                return string.Empty;

            return RecoveryHeading + Environment.NewLine + Environment.NewLine
                + string.Join(
                    Environment.NewLine + Environment.NewLine,
                    messages.Select((message, index) => $"{index + 1}. {message}"));
        }
    }
}

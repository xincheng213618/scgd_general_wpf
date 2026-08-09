using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public static class CopilotConversationService
    {
        public static bool IsReusableEmpty(CopilotConversationRecord? conversation)
        {
            return conversation != null
                && conversation.Messages.Count == 0
                && conversation.Attachments.Count == 0
                && !conversation.HasAdditionalReadRoots
                && !conversation.HasDraft
                && !conversation.HasComposerStash
                && !conversation.HasCustomTitle
                && !conversation.IsPinned
                && !conversation.IsArchived
                && string.Equals(conversation.Title, CopilotUiText.NewConversationTitle, StringComparison.Ordinal)
                && string.Equals(conversation.PreviewText, CopilotUiText.EmptyConversationPreview, StringComparison.Ordinal)
                && conversation.LastUsageInputTokens == 0
                && conversation.LastUsageOutputTokens == 0
                && conversation.LastUsageTotalTokens == 0
                && conversation.LastUsageCachedInputTokens == null
                && conversation.AccessMode == CopilotAgentAccessMode.ConfirmProtectedActions
                && conversation.AgentSessionCheckpoint == null
                && conversation.Compaction == null
                && conversation.BranchOrigin == null
                && conversation.Goal == null;
        }

        public static bool IsHistory(CopilotConversationRecord? conversation)
        {
            return conversation != null
                && (conversation.HasDraft
                    || conversation.HasComposerStash
                    || conversation.Attachments.Count > 0
                    || conversation.HasAdditionalReadRoots
                    || conversation.Messages.Any(message => !string.IsNullOrWhiteSpace(message.Content)));
        }

        public static CopilotConversationRecord? FindUniqueResumeTarget(
            IEnumerable<CopilotConversationRecord> conversations,
            string? query)
        {
            ArgumentNullException.ThrowIfNull(conversations);
            var normalizedQuery = query?.Trim() ?? string.Empty;
            if (normalizedQuery.Length == 0)
                return null;

            var candidates = conversations.Where(conversation => conversation != null).ToArray();
            var idMatch = candidates.FirstOrDefault(conversation =>
                string.Equals(conversation.Id, normalizedQuery, StringComparison.Ordinal));
            if (idMatch != null)
                return idMatch;

            var titleMatches = candidates
                .Where(conversation => string.Equals(
                    conversation.Title.Trim(),
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
            return titleMatches.Length == 1 ? titleMatches[0] : null;
        }

        internal static CopilotProfileConfig? FindUniqueProfileTarget(
            IEnumerable<CopilotProfileConfig> profiles,
            string? query)
        {
            ArgumentNullException.ThrowIfNull(profiles);
            var normalizedQuery = query?.Trim() ?? string.Empty;
            if (normalizedQuery.Length == 0)
                return null;

            var candidates = profiles.Where(profile => profile != null).ToArray();
            var idMatch = candidates.FirstOrDefault(profile =>
                string.Equals(profile.Id, normalizedQuery, StringComparison.Ordinal));
            if (idMatch != null)
                return idMatch;

            var matches = candidates
                .Where(profile =>
                    string.Equals(profile.DisplayLabel, normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(profile.Model, normalizedQuery, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        internal static int ResolveSearchNavigationIndex(
            int itemCount,
            int selectedIndex,
            bool hasPreviewSelection,
            int direction)
        {
            ArgumentOutOfRangeException.ThrowIfZero(direction);
            if (itemCount <= 0)
                return -1;
            if (!hasPreviewSelection || selectedIndex < 0 || selectedIndex >= itemCount)
                return direction < 0 ? itemCount - 1 : 0;

            return Math.Clamp(selectedIndex + Math.Sign(direction), 0, itemCount - 1);
        }

        internal static int ResolveSearchCommitIndex(
            int itemCount,
            int selectedIndex,
            bool hasPreviewSelection)
        {
            if (itemCount <= 0)
                return -1;

            return hasPreviewSelection && selectedIndex >= 0 && selectedIndex < itemCount
                ? selectedIndex
                : 0;
        }

        internal static bool TryParseAssistantResponseOrdinal(string? value, out int ordinal)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
            {
                ordinal = 1;
                return true;
            }

            return int.TryParse(normalized, out ordinal) && ordinal > 0;
        }

        internal static CopilotChatMessage? FindNthLatestCompletedAssistantResponse(
            CopilotConversationRecord? conversation,
            int ordinal)
        {
            if (conversation == null || ordinal <= 0)
                return null;

            for (var index = conversation.Messages.Count - 1; index >= 0; index--)
            {
                var message = conversation.Messages[index];
                if (message == null
                    || message.IsUser
                    || message.IsThinkingInProgress
                    || message.WasResponseInterrupted
                    || message.IsContentDisplayOnly
                    || string.IsNullOrWhiteSpace(message.Content))
                {
                    continue;
                }

                ordinal--;
                if (ordinal == 0)
                    return message;
            }

            return null;
        }

        public static CopilotConversationRecord ResolveNewTarget(
            ObservableCollection<CopilotConversationRecord> conversations,
            CopilotConversationRecord? selectedConversation,
            CopilotProfileConfig? profile)
        {
            ArgumentNullException.ThrowIfNull(conversations);

            if (IsReusableEmpty(selectedConversation))
                return selectedConversation!;

            return Create(conversations, profile);
        }

        public static CopilotConversationRecord Create(
            ObservableCollection<CopilotConversationRecord> conversations,
            CopilotProfileConfig? profile)
        {
            ArgumentNullException.ThrowIfNull(conversations);

            var conversation = CopilotConversationRecord.CreateEmpty(profile?.Id ?? string.Empty, profile?.DisplayLabel ?? string.Empty);
            Insert(conversations, conversation);
            return conversation;
        }

        public static void Insert(
            ObservableCollection<CopilotConversationRecord> conversations,
            CopilotConversationRecord conversation)
        {
            ArgumentNullException.ThrowIfNull(conversations);
            ArgumentNullException.ThrowIfNull(conversation);
            if (conversations.Contains(conversation))
                return;

            conversations.Insert(GetUnpinnedInsertIndex(conversations), conversation);
        }

        public static void MoveToPreferredIndex(
            ObservableCollection<CopilotConversationRecord> conversations,
            CopilotConversationRecord conversation)
        {
            ArgumentNullException.ThrowIfNull(conversations);
            ArgumentNullException.ThrowIfNull(conversation);

            var currentIndex = conversations.IndexOf(conversation);
            if (currentIndex < 0)
                return;

            var targetIndex = conversation.IsPinned
                ? 0
                : GetRecencyInsertIndex(conversations, conversation, currentIndex);
            if (currentIndex != targetIndex)
                conversations.Move(currentIndex, targetIndex);
        }

        public static bool MarkTurnStarted(
            ObservableCollection<CopilotConversationRecord> conversations,
            CopilotConversationRecord conversation,
            DateTime startedAt)
        {
            ArgumentNullException.ThrowIfNull(conversations);
            ArgumentNullException.ThrowIfNull(conversation);
            var previousIndex = conversations.IndexOf(conversation);
            var changed = conversation.MarkTurnStarted(startedAt);
            MoveToPreferredIndex(conversations, conversation);
            return changed || previousIndex != conversations.IndexOf(conversation);
        }

        public static bool NormalizeOrder(ObservableCollection<CopilotConversationRecord> conversations)
        {
            ArgumentNullException.ThrowIfNull(conversations);
            var indexed = conversations
                .Select((conversation, index) => new { Conversation = conversation, Index = index })
                .Where(item => item.Conversation != null)
                .ToArray();
            var ordered = indexed
                .Where(item => item.Conversation.IsPinned)
                .Concat(indexed
                    .Where(item => !item.Conversation.IsPinned)
                    .OrderByDescending(item => GetRecencyAt(item.Conversation))
                    .ThenBy(item => item.Index))
                .Select(item => item.Conversation)
                .ToArray();
            var changed = false;
            for (var targetIndex = 0; targetIndex < ordered.Length; targetIndex++)
            {
                var currentIndex = conversations.IndexOf(ordered[targetIndex]);
                if (currentIndex == targetIndex)
                    continue;

                conversations.Move(currentIndex, targetIndex);
                changed = true;
            }
            return changed;
        }

        private static int GetRecencyInsertIndex(
            ObservableCollection<CopilotConversationRecord> conversations,
            CopilotConversationRecord conversation,
            int currentIndex)
        {
            var recencyAt = GetRecencyAt(conversation);
            var targetIndex = 0;
            for (var index = 0; index < conversations.Count; index++)
            {
                var candidate = conversations[index];
                if (ReferenceEquals(candidate, conversation))
                    continue;
                if (candidate.IsPinned
                    || GetRecencyAt(candidate) > recencyAt
                    || (GetRecencyAt(candidate) == recencyAt && index < currentIndex))
                {
                    targetIndex++;
                }
            }
            return targetIndex;
        }

        private static DateTime GetRecencyAt(CopilotConversationRecord conversation) =>
            conversation.RecencyAt == default ? conversation.UpdatedAt : conversation.RecencyAt;

        private static int GetUnpinnedInsertIndex(
            ObservableCollection<CopilotConversationRecord> conversations,
            CopilotConversationRecord? exclude = null)
        {
            var count = 0;
            foreach (var conversation in conversations)
            {
                if (ReferenceEquals(conversation, exclude))
                    continue;

                if (!conversation.IsPinned)
                    break;

                count++;
            }

            return count;
        }
    }
}

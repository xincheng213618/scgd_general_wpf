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
                && !conversation.HasDraft;
        }

        public static bool IsHistory(CopilotConversationRecord? conversation)
        {
            return conversation != null
                && (conversation.HasDraft
                    || conversation.Attachments.Count > 0
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

        public static CopilotConversationRecord ResolveNewTarget(
            ObservableCollection<CopilotConversationRecord> conversations,
            CopilotConversationRecord? selectedConversation,
            CopilotProfileConfig? profile)
        {
            ArgumentNullException.ThrowIfNull(conversations);

            if (IsReusableEmpty(selectedConversation))
                return selectedConversation!;

            var reusableConversation = conversations.FirstOrDefault(IsReusableEmpty);
            return reusableConversation ?? Create(conversations, profile);
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

            var targetIndex = conversation.IsPinned ? 0 : GetUnpinnedInsertIndex(conversations, conversation);
            if (currentIndex != targetIndex)
                conversations.Move(currentIndex, targetIndex);
        }

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

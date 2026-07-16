using System;
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
            conversations.Insert(GetUnpinnedInsertIndex(conversations), conversation);
            return conversation;
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

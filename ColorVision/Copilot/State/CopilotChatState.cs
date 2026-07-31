using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotChatState
    {
        public const int CurrentSchemaVersion = 27;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public ObservableCollection<CopilotConversationRecord> Conversations { get; set; } = new();

        public string ActiveConversationId { get; set; } = string.Empty;

        public string ActiveProfileId { get; set; } = string.Empty;

        public bool IsAgentTaskPanelExpanded { get; set; } = true;

        public bool ShouldSerializeIsAgentTaskPanelExpanded() => !IsAgentTaskPanelExpanded;

        public bool ShowMessageTimestamps { get; set; } = true;

        public bool ShouldSerializeShowMessageTimestamps() => !ShowMessageTimestamps;

        public bool UseCompactMessageLayout { get; set; }

        public bool ShouldSerializeUseCompactMessageLayout() => UseCompactMessageLayout;

        public bool EnablePromptHistoryCompletions { get; set; } = true;

        public bool ShouldSerializeEnablePromptHistoryCompletions() => !EnablePromptHistoryCompletions;

        public bool UseMultilineComposer { get; set; }

        public bool ShouldSerializeUseMultilineComposer() => UseMultilineComposer;

        public ObservableCollection<CopilotQueuedFollowUpRecoveryRecord> QueuedFollowUpRecoveries { get; set; } = new();

        public bool ShouldSerializeQueuedFollowUpRecoveries() => QueuedFollowUpRecoveries?.Count > 0;

        internal int RecoveredQueuedFollowUpCount { get; set; }

        internal bool ToggleAgentTaskPanelExpanded()
        {
            IsAgentTaskPanelExpanded = !IsAgentTaskPanelExpanded;
            return IsAgentTaskPanelExpanded;
        }

        internal bool SetShowMessageTimestamps(bool show)
        {
            if (ShowMessageTimestamps == show)
                return false;

            ShowMessageTimestamps = show;
            return true;
        }

        internal bool SetUseCompactMessageLayout(bool useCompactLayout)
        {
            if (UseCompactMessageLayout == useCompactLayout)
                return false;

            UseCompactMessageLayout = useCompactLayout;
            return true;
        }

        internal bool SetEnablePromptHistoryCompletions(bool enabled)
        {
            if (EnablePromptHistoryCompletions == enabled)
                return false;

            EnablePromptHistoryCompletions = enabled;
            return true;
        }

        internal bool SetUseMultilineComposer(bool enabled)
        {
            if (UseMultilineComposer == enabled)
                return false;

            UseMultilineComposer = enabled;
            return true;
        }

        public bool EnsureInitialized(CopilotConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            var changed = false;
            if (Conversations == null)
            {
                Conversations = new ObservableCollection<CopilotConversationRecord>();
                changed = true;
            }
            for (var index = Conversations.Count - 1; index >= 0; index--)
            {
                if (Conversations[index] != null)
                    continue;

                Conversations.RemoveAt(index);
                changed = true;
            }
            var preferredDefaultProfile = config.GetPreferredDefaultProfile();

            if (config.Profiles.Count > 0 && (
                string.IsNullOrWhiteSpace(ActiveProfileId)
                || config.Profiles.All(profile => profile.Id != ActiveProfileId)
                || (preferredDefaultProfile != null
                    && !string.IsNullOrWhiteSpace(ActiveProfileId)
                    && string.Equals(ActiveProfileId, config.Profiles[0].Id, StringComparison.Ordinal)
                    && !string.Equals(ActiveProfileId, preferredDefaultProfile.Id, StringComparison.Ordinal))))
            {
                ActiveProfileId = preferredDefaultProfile?.Id ?? config.Profiles[0].Id;
                changed = true;
            }

            foreach (var conversation in Conversations)
            {
                var interruptedAssistantMessage = conversation.Messages?
                    .LastOrDefault(message => message?.IsUser == false
                        && (message.IsResponsePending || message.IsThinkingInProgress));
                var recoveredAgentRun = CopilotInterruptedAgentRunRecovery.Normalize(conversation, interruptedAssistantMessage);
                changed |= recoveredAgentRun;
                if (!recoveredAgentRun)
                    changed |= CopilotInterruptedResponseRecovery.Normalize(conversation, interruptedAssistantMessage);
                changed |= conversation.EnsureValid();

                if (string.IsNullOrWhiteSpace(conversation.ProfileId) || config.Profiles.All(profile => profile.Id != conversation.ProfileId))
                {
                    conversation.ProfileId = ActiveProfileId;
                    changed = true;
                }

                var profileName = config.FindProfile(conversation.ProfileId)?.DisplayLabel ?? string.Empty;
                if (!string.Equals(conversation.ProfileDisplayName, profileName, StringComparison.Ordinal))
                {
                    conversation.ProfileDisplayName = profileName;
                    changed = true;
                }

                var previousTitle = conversation.Title;
                var previousPreview = conversation.PreviewText;
                conversation.RefreshSummary();
                if (!string.Equals(previousTitle, conversation.Title, StringComparison.Ordinal)
                    || !string.Equals(previousPreview, conversation.PreviewText, StringComparison.Ordinal))
                {
                    changed = true;
                }
            }

            changed |= CopilotQueuedFollowUpRecovery.RestoreToDrafts(this);

            var activeConversations = Conversations
                .Where(conversation => !conversation.IsArchived)
                .ToArray();
            if (activeConversations.Length == 0)
            {
                var conversation = CopilotConversationRecord.CreateEmpty(ActiveProfileId, config.FindProfile(ActiveProfileId)?.DisplayLabel ?? string.Empty);
                Conversations.Insert(0, conversation);
                ActiveConversationId = conversation.Id;
                return true;
            }

            if (string.IsNullOrWhiteSpace(ActiveConversationId)
                || activeConversations.All(conversation => conversation.Id != ActiveConversationId))
            {
                ActiveConversationId = activeConversations[0].Id;
                changed = true;
            }

            return changed;
        }
    }
}

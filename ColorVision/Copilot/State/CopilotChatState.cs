using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotChatState
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public ObservableCollection<CopilotConversationRecord> Conversations { get; set; } = new();

        public string ActiveConversationId { get; set; } = string.Empty;

        public string ActiveProfileId { get; set; } = string.Empty;

        public bool EnsureInitialized(CopilotConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            var changed = false;
            Conversations ??= new ObservableCollection<CopilotConversationRecord>();
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

            if (Conversations.Count == 0)
            {
                var conversation = CopilotConversationRecord.CreateEmpty(ActiveProfileId, config.FindProfile(ActiveProfileId)?.DisplayLabel ?? string.Empty);
                Conversations.Add(conversation);
                ActiveConversationId = conversation.Id;
                return true;
            }

            if (string.IsNullOrWhiteSpace(ActiveConversationId) || Conversations.All(conversation => conversation.Id != ActiveConversationId))
            {
                ActiveConversationId = Conversations[0].Id;
                changed = true;
            }

            return changed;
        }
    }
}

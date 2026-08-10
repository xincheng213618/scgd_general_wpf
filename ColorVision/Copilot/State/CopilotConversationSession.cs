using System;
using System.Collections.ObjectModel;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotConversationSelectionResult(
        bool IsAccepted,
        CopilotConversationRecord? PreviousConversation,
        CopilotConversationRecord? SelectedConversation,
        CopilotProfileConfig? PreviousProfile,
        CopilotProfileConfig? SelectedProfile,
        bool ConversationChanged,
        bool ProfileChanged,
        bool ConversationProfileChanged,
        bool StateChanged)
    {
        public bool Changed => ConversationChanged || ProfileChanged || StateChanged;
    }

    internal readonly record struct CopilotProfileSelectionResult(
        CopilotProfileConfig? PreviousProfile,
        CopilotProfileConfig? SelectedProfile,
        CopilotConversationRecord? SelectedConversation,
        bool ProfileChanged,
        bool ConversationProfileChanged,
        bool StateChanged)
    {
        public bool Changed => ProfileChanged || StateChanged;
    }

    internal sealed class CopilotConversationSession
    {
        private readonly CopilotChatState _state;
        private readonly CopilotConfig _config;

        public CopilotConversationSession(CopilotChatState state, CopilotConfig config)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public ObservableCollection<CopilotConversationRecord> Conversations => _state.Conversations;

        public string ActiveConversationId => _state.ActiveConversationId;

        public string ActiveProfileId => _state.ActiveProfileId;

        public CopilotConversationRecord? SelectedConversation { get; private set; }

        public CopilotProfileConfig? SelectedProfile { get; private set; }

        public CopilotConversationRecord CreateConversation()
        {
            var profile = ResolveProfile(SelectedProfile?.Id)
                ?? ResolveProfile(_state.ActiveProfileId)
                ?? _config.GetPreferredDefaultProfile();
            return CopilotConversationService.Create(Conversations, profile);
        }

        public CopilotConversationSelectionResult SelectConversation(
            CopilotConversationRecord? conversation,
            string? preferredProfileId = null) =>
            SelectConversationCore(conversation, preferredProfileId);

        public CopilotProfileSelectionResult SelectProfile(
            CopilotProfileConfig? profile,
            bool synchronizeConversation)
        {
            var previousProfile = SelectedProfile;
            var previousActiveProfileId = _state.ActiveProfileId;
            var conversation = SelectedConversation;
            var previousConversationProfileId = conversation?.ProfileId ?? string.Empty;
            var previousConversationProfileDisplayName = conversation?.ProfileDisplayName ?? string.Empty;

            SelectedProfile = profile;
            _state.ActiveProfileId = profile?.Id ?? string.Empty;
            if (synchronizeConversation && conversation != null && profile != null)
            {
                conversation.ProfileId = profile.Id;
                conversation.ProfileDisplayName = profile.DisplayLabel;
            }

            var conversationProfileChanged = conversation != null
                && (!string.Equals(previousConversationProfileId, conversation.ProfileId, StringComparison.Ordinal)
                    || !string.Equals(
                        previousConversationProfileDisplayName,
                        conversation.ProfileDisplayName,
                        StringComparison.Ordinal));
            var stateChanged = !string.Equals(previousActiveProfileId, _state.ActiveProfileId, StringComparison.Ordinal)
                || conversationProfileChanged;
            return new CopilotProfileSelectionResult(
                previousProfile,
                SelectedProfile,
                conversation,
                !ReferenceEquals(previousProfile, SelectedProfile),
                conversationProfileChanged,
                stateChanged);
        }

        private CopilotConversationSelectionResult SelectConversationCore(
            CopilotConversationRecord? conversation,
            string? preferredProfileId)
        {
            var previousConversation = SelectedConversation;
            var previousProfile = SelectedProfile;
            if (!IsSelectable(conversation))
            {
                return new CopilotConversationSelectionResult(
                    IsAccepted: false,
                    previousConversation,
                    previousConversation,
                    previousProfile,
                    previousProfile,
                    ConversationChanged: false,
                    ProfileChanged: false,
                    ConversationProfileChanged: false,
                    StateChanged: false);
            }

            var previousActiveConversationId = _state.ActiveConversationId;
            var previousActiveProfileId = _state.ActiveProfileId;
            var previousConversationProfileId = conversation?.ProfileId ?? string.Empty;
            var previousConversationProfileDisplayName = conversation?.ProfileDisplayName ?? string.Empty;

            SelectedConversation = conversation;
            _state.ActiveConversationId = conversation?.Id ?? string.Empty;

            var profile = ResolveProfile(preferredProfileId)
                ?? ResolveProfile(conversation?.ProfileId)
                ?? ResolveProfile(_state.ActiveProfileId)
                ?? _config.GetPreferredDefaultProfile();
            SelectedProfile = profile;
            _state.ActiveProfileId = profile?.Id ?? string.Empty;
            if (conversation != null && profile != null)
            {
                conversation.ProfileId = profile.Id;
                conversation.ProfileDisplayName = profile.DisplayLabel;
            }

            var conversationProfileChanged = conversation != null
                && (!string.Equals(previousConversationProfileId, conversation.ProfileId, StringComparison.Ordinal)
                    || !string.Equals(
                        previousConversationProfileDisplayName,
                        conversation.ProfileDisplayName,
                        StringComparison.Ordinal));
            var stateChanged = !string.Equals(
                    previousActiveConversationId,
                    _state.ActiveConversationId,
                    StringComparison.Ordinal)
                || !string.Equals(previousActiveProfileId, _state.ActiveProfileId, StringComparison.Ordinal)
                || conversationProfileChanged;
            return new CopilotConversationSelectionResult(
                IsAccepted: true,
                previousConversation,
                SelectedConversation,
                previousProfile,
                SelectedProfile,
                !ReferenceEquals(previousConversation, SelectedConversation),
                !ReferenceEquals(previousProfile, SelectedProfile),
                conversationProfileChanged,
                stateChanged);
        }

        private bool IsSelectable(CopilotConversationRecord? conversation) =>
            conversation == null
            || (!conversation.IsArchived && Conversations.Contains(conversation));

        private CopilotProfileConfig? ResolveProfile(string? profileId) =>
            _config.FindProfile(profileId);
    }
}

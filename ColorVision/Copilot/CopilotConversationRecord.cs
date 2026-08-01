using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotConversationRecord : ViewModelBase
    {
        internal const int MaximumTitleCharacters = 120;

        public string Id
        {
            get => _id;
            set
            {
                if (SetProperty(ref _id, NormalizeText(value)))
                    OnPropertyChanged(nameof(HasBranchOrigin));
            }
        }
        private string _id = Guid.NewGuid().ToString("N");

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, NormalizeText(value));
        }
        private string _title = CopilotUiText.NewConversationTitle;

        public bool HasCustomTitle
        {
            get => _hasCustomTitle;
            set => SetProperty(ref _hasCustomTitle, value);
        }
        private bool _hasCustomTitle;

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (SetProperty(ref _isPinned, value))
                {
                    OnPropertyChanged(nameof(PinLabel));
                    OnPropertyChanged(nameof(PinMenuText));
                }
            }
        }
        private bool _isPinned;

        public bool IsArchived
        {
            get => _isArchived;
            set => SetProperty(ref _isArchived, value);
        }
        private bool _isArchived;

        public bool ShouldSerializeIsArchived() => IsArchived;

        public string PreviewText
        {
            get => _previewText;
            set
            {
                if (SetProperty(ref _previewText, value ?? string.Empty))
                    OnPropertyChanged(nameof(ConversationListPreviewText));
            }
        }
        private string _previewText = CopilotUiText.EmptyConversationPreview;

        public string DraftText
        {
            get => _draftText;
            set
            {
                if (SetProperty(ref _draftText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasDraft));
                    OnPropertyChanged(nameof(ConversationListPreviewText));
                }
            }
        }
        private string _draftText = string.Empty;

        public bool ShouldSerializeDraftText() => HasDraft;

        public CopilotAgentMode DraftRequestMode
        {
            get => _draftRequestMode;
            set => SetProperty(
                ref _draftRequestMode,
                Enum.IsDefined(value) ? value : CopilotAgentMode.Auto);
        }
        private CopilotAgentMode _draftRequestMode = CopilotAgentMode.Auto;

        public bool ShouldSerializeDraftRequestMode() =>
            DraftRequestMode != CopilotAgentMode.Auto;

        public CopilotComposerStash? ComposerStash
        {
            get => _composerStash;
            set
            {
                if (SetProperty(ref _composerStash, value))
                {
                    OnPropertyChanged(nameof(HasComposerStash));
                    OnPropertyChanged(nameof(ConversationListPreviewText));
                }
            }
        }
        private CopilotComposerStash? _composerStash;

        public bool ShouldSerializeComposerStash() => HasComposerStash;

        public string ProfileId
        {
            get => _profileId;
            set => SetProperty(ref _profileId, NormalizeText(value));
        }
        private string _profileId = string.Empty;

        public string ProfileDisplayName
        {
            get => _profileDisplayName;
            set => SetProperty(ref _profileDisplayName, NormalizeText(value));
        }
        private string _profileDisplayName = string.Empty;

        public CopilotResponsePersonality ResponsePersonality
        {
            get => _responsePersonality;
            set => SetProperty(ref _responsePersonality, value);
        }
        private CopilotResponsePersonality _responsePersonality;

        public bool ShouldSerializeResponsePersonality() =>
            ResponsePersonality != CopilotResponsePersonality.None;

        public int LastUsageInputTokens
        {
            get => _lastUsageInputTokens;
            set => SetProperty(ref _lastUsageInputTokens, Math.Max(0, value));
        }
        private int _lastUsageInputTokens;

        public int LastUsageOutputTokens
        {
            get => _lastUsageOutputTokens;
            set => SetProperty(ref _lastUsageOutputTokens, Math.Max(0, value));
        }
        private int _lastUsageOutputTokens;

        public int LastUsageTotalTokens
        {
            get => _lastUsageTotalTokens;
            set => SetProperty(ref _lastUsageTotalTokens, Math.Max(0, value));
        }
        private int _lastUsageTotalTokens;

        public int? LastUsageCachedInputTokens
        {
            get => _lastUsageCachedInputTokens;
            set => SetProperty(ref _lastUsageCachedInputTokens, value.HasValue ? Math.Max(0, value.Value) : null);
        }
        private int? _lastUsageCachedInputTokens;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }
        private DateTime _createdAt = DateTime.Now;

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set
            {
                if (SetProperty(ref _updatedAt, value))
                    OnPropertyChanged(nameof(UpdatedLabel));
            }
        }
        private DateTime _updatedAt = DateTime.Now;

        public ObservableCollection<CopilotChatMessage> Messages { get; set; } = new();

        public ObservableCollection<CopilotAttachmentItem> Attachments { get; set; } = new();

        public ObservableCollection<CopilotPendingSteeringRecoveryRecord> PendingSteeringRecoveries { get; set; } = new();

        public bool ShouldSerializePendingSteeringRecoveries() => PendingSteeringRecoveries?.Count > 0;

        public ObservableCollection<string> AdditionalReadRootPaths { get; set; } = new();

        public bool ShouldSerializeAdditionalReadRootPaths() => HasAdditionalReadRoots;

        [JsonIgnore]
        public bool HasAdditionalReadRoots => AdditionalReadRootPaths?.Count > 0;

        public CopilotAgentSessionCheckpoint? AgentSessionCheckpoint { get; set; }

        public CopilotAgentTaskEventJournalSnapshot? LatestAgentTaskEventJournal { get; set; }

        public bool ShouldSerializeLatestAgentTaskEventJournal() =>
            LatestAgentTaskEventJournal?.Events?.Count > 0
            && LatestAgentTaskEventJournal.IsStructurallyValid();

        public CopilotConversationCompaction? Compaction { get; set; }

        public CopilotConversationBranchOrigin? BranchOrigin
        {
            get => _branchOrigin;
            set
            {
                if (SetProperty(ref _branchOrigin, value))
                {
                    OnPropertyChanged(nameof(HasBranchOrigin));
                    OnPropertyChanged(nameof(BranchLabel));
                }
            }
        }
        private CopilotConversationBranchOrigin? _branchOrigin;

        public bool ShouldSerializeBranchOrigin() => BranchOrigin != null;

        public CopilotConversationGoal? Goal
        {
            get => _goal;
            set
            {
                if (SetProperty(ref _goal, value))
                {
                    OnPropertyChanged(nameof(HasGoal));
                    OnPropertyChanged(nameof(GoalDisplayText));
                    OnPropertyChanged(nameof(GoalToolTip));
                }
            }
        }
        private CopilotConversationGoal? _goal;

        public bool ShouldSerializeGoal() => Goal != null;

        [JsonIgnore]
        public CopilotTokenUsage LastUsage => new(
            LastUsageInputTokens,
            LastUsageOutputTokens,
            LastUsageTotalTokens,
            LastUsageCachedInputTokens);

        public void Touch()
        {
            UpdatedAt = DateTime.Now;
        }

        internal bool MarkWorkspaceChangeSetRolledBack(string changeSetId)
        {
            if (string.IsNullOrWhiteSpace(changeSetId))
                return false;

            var changed = false;
            foreach (var trace in Messages
                .SelectMany(message => message.AgentTraceEntries ?? new ObservableCollection<CopilotAgentTraceEntry>())
                .Where(trace => trace != null))
            {
                changed |= trace.MarkWorkspaceChangeSetRolledBack(changeSetId);
            }

            return changed;
        }

        public void SetLastUsage(CopilotTokenUsage usage)
        {
            LastUsageInputTokens = usage.InputTokens;
            LastUsageOutputTokens = usage.OutputTokens;
            LastUsageTotalTokens = usage.EffectiveTotalTokens;
            LastUsageCachedInputTokens = usage.CachedInputTokens;
        }

        public void ClearLastUsage()
        {
            LastUsageInputTokens = 0;
            LastUsageOutputTokens = 0;
            LastUsageTotalTokens = 0;
            LastUsageCachedInputTokens = null;
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    }
}

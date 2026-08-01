using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public sealed class CopilotConversationRecord : ViewModelBase
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

        [JsonIgnore]
        public CopilotAgentAccessMode AccessMode => _accessContext.Mode;

        [JsonIgnore]
        public bool IsFullAccessPreparedForNextTask => _accessContext.IsPreparedForNextTask;

        [JsonIgnore]
        public string FullAccessTaskId => _accessContext.GrantedTaskId;

        [JsonIgnore]
        public string FullAccessWorkspacePath => _accessContext.WorkspacePath;

        [JsonIgnore]
        public DateTimeOffset? FullAccessExpiresAtUtc => _accessContext.ExpiresAtUtc;

        // AccessMode used to be persisted as an indefinite conversation setting. Read and
        // discard that legacy property so reopening the application always restores the
        // safe per-action confirmation posture.
        [JsonProperty(nameof(AccessMode))]
        private CopilotAgentAccessMode PersistedLegacyAccessMode
        {
            set => _legacyAccessModeLoaded = true;
        }
        private bool _legacyAccessModeLoaded;

        [JsonIgnore]
        internal CopilotAgentAccessContext AccessContext => _accessContext;
        private readonly CopilotAgentAccessContext _accessContext = new();

        internal void PrepareFullAccessGrant(
            string workspacePath,
            string? taskId,
            DateTimeOffset expiresAtUtc)
        {
            _accessContext.PrepareFullAccess(Id, workspacePath, taskId, expiresAtUtc);
            NotifyAccessGrantChanged();
        }

        internal bool BindFullAccessGrantToTask(string taskId, string workspacePath)
        {
            var beforeTaskId = FullAccessTaskId;
            var beforeMode = AccessMode;
            var bound = _accessContext.BindToTask(Id, taskId, workspacePath);
            if (beforeMode != AccessMode
                || !string.Equals(beforeTaskId, FullAccessTaskId, StringComparison.Ordinal))
            {
                NotifyAccessGrantChanged();
            }
            return bound;
        }

        internal bool RevokeFullAccessGrant(string? taskId = null)
        {
            if (!_accessContext.Revoke(taskId))
                return false;

            NotifyAccessGrantChanged();
            return true;
        }

        internal bool ExpireFullAccessGrantIfNeeded()
        {
            if (!_accessContext.ExpireIfNeeded())
                return false;

            NotifyAccessGrantChanged();
            return true;
        }

        private void NotifyAccessGrantChanged()
        {
            OnPropertyChanged(nameof(AccessMode));
            OnPropertyChanged(nameof(IsFullAccessPreparedForNextTask));
            OnPropertyChanged(nameof(FullAccessTaskId));
            OnPropertyChanged(nameof(FullAccessWorkspacePath));
            OnPropertyChanged(nameof(FullAccessExpiresAtUtc));
        }

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
        public string UpdatedLabel => UpdatedAt.Date == DateTime.Today ? UpdatedAt.ToString("HH:mm") : UpdatedAt.ToString("M/d");

        [JsonIgnore]
        public bool HasDraft => !string.IsNullOrWhiteSpace(DraftText);

        [JsonIgnore]
        public bool HasComposerStash => ComposerStash?.HasContent == true;

        [JsonIgnore]
        public string ConversationListPreviewText =>
            !string.IsNullOrWhiteSpace(SearchMatchPreviewText)
                ? SearchMatchPreviewText
                : HasDraft
                    ? $"草稿：{BuildPreview(DraftText, 42)}"
                    : HasComposerStash
                        ? BuildComposerStashPreview(ComposerStash!)
                        : PreviewText;

        [JsonIgnore]
        public string SearchMatchPreviewText
        {
            get => _searchMatchPreviewText;
            private set
            {
                if (SetProperty(ref _searchMatchPreviewText, value ?? string.Empty))
                    OnPropertyChanged(nameof(ConversationListPreviewText));
            }
        }
        private string _searchMatchPreviewText = string.Empty;

        internal void SetSearchMatchPreview(string? preview) =>
            SearchMatchPreviewText = preview;

        [JsonIgnore]
        public string PinLabel => IsPinned ? CopilotUiText.PinnedLabel : string.Empty;

        [JsonIgnore]
        public string PinMenuText => IsPinned ? CopilotUiText.UnpinMenuText : CopilotUiText.PinMenuText;

        [JsonIgnore]
        public bool HasBranchOrigin => BranchOrigin?.IsStructurallyValid(Id) == true;

        [JsonIgnore]
        public string BranchLabel => HasBranchOrigin ? "分支" : string.Empty;

        [JsonIgnore]
        public bool HasGoal => Goal?.IsStructurallyValid() == true;

        [JsonIgnore]
        public string GoalDisplayText => Goal == null
            ? string.Empty
            : $"{Goal.State switch
            {
                CopilotConversationGoalState.Active => "持续目标",
                CopilotConversationGoalState.Achieved => "目标已达成",
                _ => "目标已暂停",
            }} · {BuildPreview(Goal.Objective, 120)}";

        [JsonIgnore]
        public string GoalToolTip => Goal == null
            ? string.Empty
            : $"{Goal.State switch
            {
                CopilotConversationGoalState.Active => "活动目标会绑定到后续新 Agent 任务，并在每轮后独立评估。",
                CopilotConversationGoalState.Achieved => "独立完成评估已确认该目标达成。",
                _ => "该目标已暂停，不会自动启动新任务。",
            }}"
                + Environment.NewLine
                + Goal.Objective
                + Environment.NewLine
                + $"{Goal.TurnCount:N0} 轮 · {Goal.EvaluationCount:N0} 次独立评估 · {Goal.TokensUsed:N0} Token"
                + (string.IsNullOrWhiteSpace(Goal.LastEvaluationReason)
                    ? string.Empty
                    : Environment.NewLine + "最近判断：" + Goal.LastEvaluationReason)
                + Environment.NewLine
                + "目标约束完成判定，但不授权写入、工具调用、审批复用或外部副作用。";

        [JsonIgnore]
        public string AgentRunStatusLabel
        {
            get => _agentRunStatusLabel;
            internal set
            {
                if (SetProperty(ref _agentRunStatusLabel, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasAgentRunStatus));
            }
        }
        private string _agentRunStatusLabel = string.Empty;

        [JsonIgnore]
        public bool HasAgentRunStatus => !string.IsNullOrWhiteSpace(AgentRunStatusLabel);

        [JsonIgnore]
        public CopilotTokenUsage LastUsage => new(
            LastUsageInputTokens,
            LastUsageOutputTokens,
            LastUsageTotalTokens,
            LastUsageCachedInputTokens);

        public bool EnsureValid()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (UpdatedAt == default)
            {
                UpdatedAt = CreatedAt;
                changed = true;
            }

            if (_draftText == null)
            {
                DraftText = string.Empty;
                changed = true;
            }
            if (!Enum.IsDefined(DraftRequestMode))
            {
                DraftRequestMode = CopilotAgentMode.Auto;
                changed = true;
            }
            if (ComposerStash != null)
            {
                changed |= ComposerStash.EnsureValid();
                if (!ComposerStash.HasContent)
                {
                    ComposerStash = null;
                    changed = true;
                }
            }
            if (!Enum.IsDefined(ResponsePersonality))
            {
                ResponsePersonality = CopilotResponsePersonality.None;
                changed = true;
            }
            if (_legacyAccessModeLoaded)
            {
                _legacyAccessModeLoaded = false;
                changed = true;
            }
            changed |= _accessContext.Revoke();

            if (Messages == null)
            {
                Messages = new ObservableCollection<CopilotChatMessage>();
                changed = true;
            }
            if (Attachments == null)
            {
                Attachments = new ObservableCollection<CopilotAttachmentItem>();
                changed = true;
            }
            changed |= CopilotSteeringRecovery.NormalizePendingRecords(this);
            if (AdditionalReadRootPaths == null)
            {
                AdditionalReadRootPaths = new ObservableCollection<string>();
                changed = true;
            }
            var normalizedReadRoots = CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(
                AdditionalReadRootPaths);
            if (!AdditionalReadRootPaths.SequenceEqual(
                    normalizedReadRoots,
                    StringComparer.OrdinalIgnoreCase))
            {
                AdditionalReadRootPaths.Clear();
                foreach (var path in normalizedReadRoots)
                    AdditionalReadRootPaths.Add(path);
                changed = true;
            }
            for (var index = Messages.Count - 1; index >= 0; index--)
            {
                if (Messages[index] != null)
                    continue;

                Messages.RemoveAt(index);
                changed = true;
            }
            for (var index = Attachments.Count - 1; index >= 0; index--)
            {
                if (Attachments[index] != null)
                    continue;

                Attachments.RemoveAt(index);
                changed = true;
            }
            if (AgentSessionCheckpoint != null && !AgentSessionCheckpoint.IsStructurallyValid())
            {
                AgentSessionCheckpoint = null;
                changed = true;
            }
            if (LatestAgentTaskEventJournal != null
                && (LatestAgentTaskEventJournal.Events?.Count is not > 0
                    || !LatestAgentTaskEventJournal.IsStructurallyValid()))
            {
                LatestAgentTaskEventJournal = null;
                changed = true;
            }
            if (LatestAgentTaskEventJournal == null
                && AgentSessionCheckpoint?.TaskEventJournal is { Events.Count: > 0 } checkpointJournal)
            {
                changed |= UpdateLatestAgentTaskEventJournal(checkpointJournal);
            }
            if (Compaction != null && !Compaction.IsStructurallyValid())
            {
                Compaction = null;
                changed = true;
            }
            if (BranchOrigin != null && !BranchOrigin.IsStructurallyValid(Id))
            {
                BranchOrigin = null;
                changed = true;
            }
            if (Goal != null && !Goal.IsStructurallyValid())
            {
                Goal = null;
                changed = true;
            }

            var lastUserRequestMode = CopilotAgentMode.Chat;
            foreach (var message in Messages)
            {
                changed |= message.EnsureValid();
                if (message.IsUser)
                {
                    lastUserRequestMode = message.RequestMode;
                }
                else if (message.RequestMode != lastUserRequestMode)
                {
                    message.RequestMode = lastUserRequestMode;
                    changed = true;
                }
            }
            var lastAssistantMessage = Messages.LastOrDefault(message =>
                !message.IsUser
                && !message.WasResponseInterrupted);
            if (lastAssistantMessage != null
                && !lastAssistantMessage.ReportedUsage.HasAny
                && LastUsage.HasAny)
            {
                changed |= lastAssistantMessage.SetReportedUsage(LastUsage);
            }

            foreach (var attachment in Attachments)
            {
                changed |= attachment.EnsureValid();
            }

            return changed;
        }

        internal bool ReplaceAdditionalReadRootPaths(IEnumerable<string>? paths)
        {
            var normalized = CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(paths);
            AdditionalReadRootPaths ??= new ObservableCollection<string>();
            if (AdditionalReadRootPaths.SequenceEqual(normalized, StringComparer.OrdinalIgnoreCase))
                return false;

            AdditionalReadRootPaths.Clear();
            foreach (var path in normalized)
                AdditionalReadRootPaths.Add(path);
            OnPropertyChanged(nameof(AdditionalReadRootPaths));
            OnPropertyChanged(nameof(HasAdditionalReadRoots));
            return true;
        }

        internal bool UpdateLatestAgentTaskEventJournal(CopilotAgentTaskEventJournalSnapshot? journal)
        {
            if (journal?.Events?.Count is not > 0 || !journal.IsStructurallyValid())
                return false;

            var currentEvents = LatestAgentTaskEventJournal?.Events;
            var currentLast = currentEvents?.Count > 0 ? currentEvents[^1] : null;
            var candidateLast = journal.Events[^1];
            if (LatestAgentTaskEventJournal?.IsStructurallyValid() == true
                && currentLast != null
                && string.Equals(currentLast.RunId, candidateLast.RunId, StringComparison.Ordinal)
                && currentLast.Sequence >= candidateLast.Sequence)
            {
                return false;
            }

            LatestAgentTaskEventJournal = journal;
            return true;
        }

        internal IEnumerable<CopilotAttachmentItem> EnumerateReferencedAttachments()
        {
            foreach (var attachment in Attachments?.Where(attachment => attachment != null) ?? Enumerable.Empty<CopilotAttachmentItem>())
                yield return attachment;

            foreach (var attachment in ComposerStash?.Attachments?.Where(attachment => attachment != null) ?? Enumerable.Empty<CopilotAttachmentItem>())
                yield return attachment;

            foreach (var message in Messages?.Where(message => message != null) ?? Enumerable.Empty<CopilotChatMessage>())
            {
                foreach (var attachment in message.Attachments?.Where(attachment => attachment != null) ?? Enumerable.Empty<CopilotAttachmentItem>())
                    yield return attachment;
            }
        }

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

        public void RefreshSummary()
        {
            var firstUserMessage = Messages.FirstOrDefault(message => message.Role == CopilotChatRole.User && !string.IsNullOrWhiteSpace(message.Content));
            var generatedTitle = firstUserMessage == null ? CopilotUiText.NewConversationTitle : BuildPreview(firstUserMessage.Content, 24);
            if (!HasCustomTitle || string.IsNullOrWhiteSpace(Title))
                Title = generatedTitle;

            var lastVisibleMessage = Messages.LastOrDefault(message => !string.IsNullOrWhiteSpace(message.Content));
            if (lastVisibleMessage != null)
            {
                PreviewText = BuildPreview(lastVisibleMessage.Content, 42);
                return;
            }

            PreviewText = Attachments.Count > 0
                ? CopilotUiText.FormatAttachmentMountedCount(Attachments.Count)
                : CopilotUiText.EmptyConversationPreview;
        }

        public void SetCustomTitle(string title)
        {
            Title = title;
            HasCustomTitle = true;
        }

        internal static bool TryNormalizeCustomTitle(string? title, out string normalizedTitle)
        {
            normalizedTitle = NormalizeText(title);
            return normalizedTitle.Length is > 0 and <= MaximumTitleCharacters;
        }

        public void SetGeneratedTitle(string title)
        {
            Title = title;
            HasCustomTitle = true;
        }

        public static CopilotConversationRecord CreateEmpty(string profileId, string profileDisplayName)
        {
            return new CopilotConversationRecord
            {
                HasCustomTitle = false,
                ProfileId = profileId,
                ProfileDisplayName = profileDisplayName,
                Title = CopilotUiText.NewConversationTitle,
                PreviewText = CopilotUiText.EmptyConversationPreview,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };
        }

        private static string BuildPreview(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
        }

        private static string BuildComposerStashPreview(CopilotComposerStash stash)
        {
            var textPreview = BuildPreview(stash.Text, 34);
            if (!string.IsNullOrWhiteSpace(textPreview))
                return $"已暂存：{textPreview}";

            return $"已暂存：{stash.Attachments.Count:N0} 个附件";
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    }
}

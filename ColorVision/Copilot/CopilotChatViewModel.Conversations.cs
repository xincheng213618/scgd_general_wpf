#pragma warning disable CA1001,CA1822,CA1859,CA1861,CA1870,CS4014
using ColorVision.Solution;
using ColorVision.Solution.Workspace;
using ColorVision.Copilot.Mcp;
using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Desktop.Feedback;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public partial class CopilotChatViewModel
    {
        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsConversationEmpty));
            OnPropertyChanged(nameof(InputPlaceholder));
            RefreshCompactHistoryConversations();
            RefreshFilteredConversations();
            RefreshAgentTasks();
            RefreshComposerTokenEstimate();
            RefreshConversationFind();
            RefreshPromptHistorySearchResults();
            NotifyPromptHistoryPrefixCompletionChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        private void Conversations_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshCompactHistoryConversations();
            RefreshFilteredConversations();
            RefreshAgentTasks();
            RefreshConversationRunStatuses();
            OnPropertyChanged(nameof(Conversations));
            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshCompactHistoryConversations()
        {
            var history = Conversations
                .Where(conversation =>
                    !conversation.IsArchived
                    && !ReferenceEquals(conversation, SelectedConversation)
                    && CopilotConversationService.IsHistory(conversation))
                .Take(CompactHistoryLimit)
                .ToArray();

            CompactHistoryConversations.Clear();
            foreach (var conversation in history)
            {
                CompactHistoryConversations.Add(conversation);
            }

            OnPropertyChanged(nameof(HasCompactHistoryConversations));
            OnPropertyChanged(nameof(CanShowCompactHistory));
            OnPropertyChanged(nameof(HasCompactHistoryOverflow));
            OnPropertyChanged(nameof(CompactHistoryFooterText));
        }

        private void RefreshFilteredConversations()
        {
            _conversationSearchDebounceTimer.Stop();
            var terms = (ConversationSearchText ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(MaximumConversationSearchTerms)
                .ToArray();
            var activeConversations = CopilotConversationArchiveService.GetActive(Conversations);
            CopilotConversationRecord[] matches;
            if (terms.Length == 0)
            {
                foreach (var conversation in Conversations)
                    conversation.SetSearchMatchPreview(string.Empty);
                matches = activeConversations.ToArray();
            }
            else
            {
                var matchedConversations = new List<CopilotConversationRecord>();
                foreach (var conversation in Conversations)
                {
                    if (conversation.IsArchived
                        || !CopilotConversationSearchPreview.TryBuild(
                            conversation,
                            terms,
                            out var preview))
                    {
                        conversation.SetSearchMatchPreview(string.Empty);
                        continue;
                    }

                    conversation.SetSearchMatchPreview(preview);
                    matchedConversations.Add(conversation);
                }
                matches = matchedConversations.ToArray();
            }

            FilteredConversations.Clear();
            foreach (var conversation in matches)
                FilteredConversations.Add(conversation);

            OnPropertyChanged(nameof(HasNoConversationSearchResults));
            OnPropertyChanged(nameof(SelectedConversation));
            RefreshConversationBranchFamily();
        }

        private void RefreshConversationBranchFamily()
        {
            ConversationBranchFamily = CopilotConversationBranchService.BuildBranchFamily(
                CopilotConversationArchiveService.GetActive(Conversations),
                SelectedConversation);
            OnPropertyChanged(nameof(ConversationBranchFamily));
            OnPropertyChanged(nameof(HasConversationBranchFamily));
            OnPropertyChanged(nameof(ConversationBranchFamilyLabel));
        }

        private void ScheduleConversationSearchRefresh()
        {
            _conversationSearchDebounceTimer.Stop();
            if (IsConversationSearchEmpty)
            {
                RefreshFilteredConversations();
                return;
            }

            _conversationSearchDebounceTimer.Start();
        }

        internal bool FlushConversationSearchRefresh()
        {
            if (!_conversationSearchDebounceTimer.IsEnabled)
                return false;

            RefreshFilteredConversations();
            return true;
        }

        private void ConversationSearchDebounceTimer_Tick(object? sender, EventArgs e) => RefreshFilteredConversations();

        public void OpenConversationFind()
        {
            OpenConversationFind(ConversationFindText);
        }

        private void OpenConversationFind(string? query)
        {
            DismissLocalCommandResult();
            var previousQuery = ConversationFindText;
            var opened = _conversationFindSession.Open(Messages, query);
            if (opened)
            {
                OnPropertyChanged(nameof(IsConversationFindOpen));
                OnPropertyChanged(nameof(CurrentConversationFindMatch));
                CommandManager.InvalidateRequerySuggested();
            }

            if (!string.Equals(previousQuery, ConversationFindText, StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(ConversationFindText));
                OnPropertyChanged(nameof(HasConversationFindQuery));
            }

            NotifyConversationFindStateChanged();
        }

        public void CloseConversationFind()
        {
            if (!IsConversationFindOpen)
                return;

            _conversationFindSession.Close(Messages);
            OnPropertyChanged(nameof(IsConversationFindOpen));
            OnPropertyChanged(nameof(CurrentConversationFindMatch));
            CommandManager.InvalidateRequerySuggested();
            NotifyConversationFindStateChanged();
        }

        public bool MoveConversationFind(bool previous)
        {
            if (!_conversationFindSession.Move(Messages, previous))
                return false;

            NotifyConversationFindStateChanged();
            return true;
        }

        internal void RefreshConversationFind()
        {
            if (!_conversationFindSession.Refresh(Messages))
                return;

            NotifyConversationFindStateChanged();
        }

        private void NotifyConversationFindStateChanged()
        {
            OnPropertyChanged(nameof(HasConversationFindMatches));
            OnPropertyChanged(nameof(ConversationFindStatusText));
            OnPropertyChanged(nameof(CurrentConversationFindMatch));
            CommandManager.InvalidateRequerySuggested();
        }

        private static string NormalizeConversationSearchText(string? value)
        {
            var normalized = value ?? string.Empty;
            if (normalized.Length <= MaximumConversationSearchCharacters)
                return normalized;

            var retainedLength = MaximumConversationSearchCharacters;
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }

            return normalized[..retainedLength];
        }

        private void RefreshAgentTasks()
        {
            var tasks = CopilotAgentTaskIndex.Build(
                CopilotConversationArchiveService.GetActive(Conversations));
            AgentTasks.Clear();
            foreach (var task in tasks)
                AgentTasks.Add(task);

            OnPropertyChanged(nameof(HasAgentTasks));
            OnPropertyChanged(nameof(AgentTaskCountLabel));
            OnPropertyChanged(nameof(IsAgentTaskListVisible));
            CommandManager.InvalidateRequerySuggested();
        }

        private int CountHistoryConversations()
        {
            return Conversations.Count(conversation =>
                !conversation.IsArchived
                && !ReferenceEquals(conversation, SelectedConversation)
                && CopilotConversationService.IsHistory(conversation));
        }

        private void Attachments_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateChatAttachmentTokenEstimate();
            RefreshComposerTokenEstimate();
            RefreshCompactHistoryConversations();
            if (HasConversationSearchQuery)
                RefreshFilteredConversations();
            OnCurrentLiveContextStateChanged();
            OnActiveDocumentStateChanged();
        }

        private void SelectConversation(CopilotConversationRecord? conversation, bool persist, string? preferredProfileId = null)
        {
            if (conversation?.IsArchived == true)
                return;

            if (IsPromptHistorySearchOpen)
                DismissPromptHistorySearch();

            if (conversation != null && HasConversationSearchQuery && !FilteredConversations.Contains(conversation))
                ConversationSearchText = string.Empty;

            if (ReferenceEquals(_selectedConversation, conversation))
            {
                if (!string.IsNullOrWhiteSpace(preferredProfileId))
                {
                    var preferredProfile = ResolveProfile(preferredProfileId) ?? ResolveProfile(_selectedConversation?.ProfileId);
                    SelectProfile(preferredProfile, syncConversation: true, persist: false);
                }
                RefreshConversationFind();
                return;
            }

            if (IsEditingMessage)
                CancelMessageEdit();

            if (_selectedConversation != null)
                _selectedConversation.Attachments.CollectionChanged -= Attachments_CollectionChanged;

            if (_selectedConversation != null)
                _selectedConversation.Messages.CollectionChanged -= Messages_CollectionChanged;

            CopilotConversationFindSession.ClearHighlights(_selectedConversation?.Messages);
            _selectedConversation = conversation;
            _pendingRequestModeOverride = conversation?.DraftRequestMode is { } restoredMode
                && restoredMode != CopilotAgentMode.Auto
                    ? restoredMode
                    : null;
            _pendingWorkspaceReviewTarget = _pendingRequestModeOverride == CopilotAgentMode.Review
                && conversation?.DraftWorkspaceReviewTarget?.IsStructurallyValid() == true
                    ? conversation.DraftWorkspaceReviewTarget.CreateSnapshot()
                    : null;
            _promptHistoryNavigator.Reset();
            DismissLocalCommandResult();
            if (_selectedConversation != null)
                _selectedConversation.Attachments.CollectionChanged += Attachments_CollectionChanged;

            if (_selectedConversation != null)
                _selectedConversation.Messages.CollectionChanged += Messages_CollectionChanged;

            InputText = _selectedConversation?.DraftText ?? string.Empty;

            OnPropertyChanged(nameof(SelectedConversation));
            OnPropertyChanged(nameof(Messages));
            OnPropertyChanged(nameof(Attachments));
            OnPropertyChanged(nameof(HasAttachments));
            OnPropertyChanged(nameof(HasComposerStash));
            OnPropertyChanged(nameof(ComposerStashToolTip));
            OnPropertyChanged(nameof(IsConversationEmpty));
            OnPropertyChanged(nameof(InputPlaceholder));
            RefreshConversationFind();
            OnComposerAccessModeChanged();
            RefreshPendingActions();
            RefreshConversationBranchFamily();
            RefreshCompactHistoryConversations();
            NotifyHostedRunStateChanged();
            RefreshCompletionNotice();
            PublishSelectedTaskEventJournal();

            _state.ActiveConversationId = conversation?.Id ?? string.Empty;

            var profile = ResolveProfile(preferredProfileId)
                ?? ResolveProfile(conversation?.ProfileId)
                ?? ResolveProfile(_state.ActiveProfileId)
                ?? _config.GetPreferredDefaultProfile();

            SelectProfile(profile, syncConversation: false, persist: false);
            OnComposerRequestModeChanged();

            var shouldPersist = persist;

            if (conversation != null && profile != null)
            {
                conversation.ProfileId = profile.Id;
                conversation.ProfileDisplayName = profile.DisplayLabel;
                conversation.RefreshSummary();
            }

            if (conversation != null && EnsureAssistantHeaders(conversation, profile))
                shouldPersist = true;

            InvalidateChatAttachmentTokenEstimate();
            RefreshComposerTokenEstimate();
            OnCurrentLiveContextStateChanged();
            OnActiveDocumentStateChanged();
            RefreshComposerReferenceSuggestions();

            if (shouldPersist)
                PersistState();
        }

        private void UpdateSelectedConversationDraft(string draftText)
        {
            var conversation = _selectedConversation;
            if (conversation == null || string.Equals(conversation.DraftText, draftText, StringComparison.Ordinal))
                return;

            var hadDraft = conversation.HasDraft;
            conversation.DraftText = draftText;
            if (hadDraft != conversation.HasDraft)
                RefreshCompactHistoryConversations();
            if (HasConversationSearchQuery)
                RefreshFilteredConversations();

            _statePersistenceCoordinator.RequestSave();
        }

        private void SelectProfile(CopilotProfileConfig? profile, bool syncConversation, bool persist)
        {
            if (ReferenceEquals(_selectedProfile, profile))
                return;

            if (_selectedProfile != null)
                _selectedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;

            _selectedProfile = profile;
            if (_selectedProfile != null)
                _selectedProfile.PropertyChanged += SelectedProfile_PropertyChanged;

            OnPropertyChanged(nameof(SelectedProfile));
            OnPropertyChanged(nameof(SelectedProfileToolTip));
            RefreshSelectedProfileReasoningState();

            _state.ActiveProfileId = profile?.Id ?? string.Empty;

            var shouldPersist = persist;

            if (syncConversation && SelectedConversation != null && profile != null)
            {
                SelectedConversation.ProfileId = profile.Id;
                SelectedConversation.ProfileDisplayName = profile.DisplayLabel;
                SelectedConversation.RefreshSummary();

                if (EnsureAssistantHeaders(SelectedConversation, profile))
                    shouldPersist = true;
            }

            if (shouldPersist)
                PersistState();

            RefreshComposerTokenEstimate();
        }

        private void SelectedProfile_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(CopilotProfileConfig.ReasoningMode)
                or nameof(CopilotProfileConfig.ReasoningLabel)
                or nameof(CopilotProfileConfig.VendorType)
                or nameof(CopilotProfileConfig.Name)
                or nameof(CopilotProfileConfig.Model)
                or nameof(CopilotProfileConfig.ProviderType))
            {
                RefreshSelectedProfileReasoningState();
                OnPropertyChanged(nameof(SelectedProfileToolTip));
            }
        }

        private void RefreshSelectedProfileReasoningState()
        {
            OnPropertyChanged(nameof(SelectedProfileReasoningOptions));
            OnPropertyChanged(nameof(SelectedProfileReasoningLabel));
            OnPropertyChanged(nameof(SelectedProfileReasoningToolTip));
            OnPropertyChanged(nameof(HasConfigurableReasoning));
            RefreshLocalCommandSuggestions();
        }

        private CopilotConversationRecord EnsureConversation()
        {
            if (SelectedConversation != null)
                return SelectedConversation;

            var conversation = CreateConversation();
            SelectConversation(conversation, persist: false);
            return conversation;
        }

        private CopilotConversationRecord CreateConversation()
        {
            var profile = SelectedProfile ?? ResolveProfile(_state.ActiveProfileId) ?? _config.GetPreferredDefaultProfile();
            return CopilotConversationService.Create(Conversations, profile);
        }

        private static CopilotProfileConfig CreateConversationRequestProfile(
            CopilotProfileConfig profile,
            CopilotConversationRecord? conversation)
        {
            return CopilotResponsePresentationGuidance.CreateRequestProfile(
                profile,
                conversation?.ResponsePersonality ?? CopilotResponsePersonality.None);
        }

        private void UpdateConversationMetadata(CopilotConversationRecord conversation, bool touch)
        {
            if (touch)
                conversation.Touch();

            if (SelectedProfile != null)
            {
                conversation.ProfileId = SelectedProfile.Id;
                conversation.ProfileDisplayName = SelectedProfile.DisplayLabel;
            }

            conversation.RefreshSummary();
            RefreshFilteredConversations();
            RefreshComposerTokenEstimate();
        }

        private async Task ApplyGeneratedConversationTitleAsync(
            CopilotConversationRecord conversation,
            CopilotConversationTitleGenerationResult result,
            Func<bool> isCurrentGeneration,
            CancellationToken cancellationToken)
        {
            if (!CanApplyAuxiliaryConversationResult(conversation))
                return;

            var application = Application.Current;
            if (application == null)
                return;

            await CopilotUiDispatcher.InvokeAsync(application.Dispatcher, () =>
            {
                if (!CanApplyAuxiliaryConversationResult(conversation))
                    return false;

                conversation.RecordTitleGenerationUsage(result.Usage, result.CompletedAtUtc);
                var shouldApplyTitle = !cancellationToken.IsCancellationRequested
                    && isCurrentGeneration()
                    && !conversation.HasCustomTitle
                    && !string.IsNullOrWhiteSpace(result.Title);
                if (shouldApplyTitle)
                {
                    conversation.SetGeneratedTitle(result.Title!);
                    RefreshFilteredConversations();
                }
                PersistState();
                return shouldApplyTitle;
            }, CancellationToken.None).ConfigureAwait(false);
        }

        private bool CanApplyAuxiliaryConversationResult(CopilotConversationRecord? conversation) =>
            Volatile.Read(ref _disposeState) == 0
            && conversation != null
            && Conversations.Contains(conversation);

        private CopilotNonBlockingCancellationSource BeginAuxiliaryOperation()
        {
            var cancellation = new CopilotNonBlockingCancellationSource();
            _auxiliaryOperationCancellations.Add(cancellation);
            return cancellation;
        }

        private void CompleteAuxiliaryOperation(CopilotNonBlockingCancellationSource cancellation)
        {
            _auxiliaryOperationCancellations.Remove(cancellation);
            cancellation.Dispose();
        }

        private void CancelAllAuxiliaryOperations()
        {
            var cancellations = _auxiliaryOperationCancellations.ToArray();
            _auxiliaryOperationCancellations.Clear();
            foreach (var cancellation in cancellations)
                cancellation.RequestCancellation();
        }

        private void RenameConversation(CopilotConversationRecord? conversation)
        {
            if (conversation == null || !CanRenameConversation(conversation))
                return;

            var window = new CopilotTextInputWindow(
                "Rename Chat",
                "Enter a new chat name",
                conversation.Title,
                maximumLength: CopilotConversationRecord.MaximumTitleCharacters)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.ResultText))
                return;

            TryApplyConversationTitle(conversation!, window.ResultText);
        }

        private bool CanRenameConversation(CopilotConversationRecord? conversation) =>
            Volatile.Read(ref _disposeState) == 0
            && conversation != null
            && Conversations.Contains(conversation);

        private bool TryApplyConversationTitle(CopilotConversationRecord conversation, string? requestedTitle)
        {
            if (!CopilotConversationRecord.TryNormalizeCustomTitle(requestedTitle, out var normalizedTitle))
                return false;

            _conversationTitleCoordinator.Cancel(conversation.Id);
            conversation.SetCustomTitle(normalizedTitle);
            RefreshFilteredConversations();
            PersistState();
            return true;
        }

        private bool CanExportConversation(CopilotConversationRecord? conversation) => !_isExportingConversation
            && Volatile.Read(ref _disposeState) == 0
            && CopilotConversationMarkdownExporter.CanExport(conversation);


    }
}

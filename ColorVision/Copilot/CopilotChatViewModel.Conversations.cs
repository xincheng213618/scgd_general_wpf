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

            UpdateProjection(CompactHistoryConversations, history);

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
            IEnumerable<CopilotConversationRecord> candidates =
                CopilotConversationArchiveService.GetActive(Conversations);
            if (IsActivityViewOpen)
            {
                candidates = candidates
                    .Where(conversation => conversation.HasAgentRunStatus)
                    .OrderBy(GetConversationActivityPriority);
            }

            CopilotConversationRecord[] matches;
            Dictionary<CopilotConversationRecord, string>? previews = null;
            if (terms.Length == 0)
            {
                matches = candidates.ToArray();
            }
            else
            {
                var matchedConversations = new List<CopilotConversationRecord>();
                previews = new Dictionary<CopilotConversationRecord, string>();
                foreach (var conversation in candidates)
                {
                    if (!CopilotConversationSearchPreview.TryBuild(
                            conversation,
                            terms,
                            out var preview))
                    {
                        continue;
                    }

                    previews[conversation] = preview;
                    matchedConversations.Add(conversation);
                }
                matches = matchedConversations.ToArray();
            }

            foreach (var conversation in Conversations)
            {
                var preview = previews?.GetValueOrDefault(conversation) ?? string.Empty;
                if (!string.Equals(conversation.SearchMatchPreviewText, preview, StringComparison.Ordinal))
                    conversation.SetSearchMatchPreview(preview);
            }
            UpdateProjection(FilteredConversations, matches);

            OnPropertyChanged(nameof(HasNoConversationSearchResults));
            OnPropertyChanged(nameof(HasNoActivityConversations));
            OnPropertyChanged(nameof(SelectedConversation));
            RefreshConversationBranchFamily();
            NotifyConversationActivitySummary();
        }

        private static void UpdateProjection<T>(
            ObservableCollection<T> projection,
            IReadOnlyList<T> desired) where T : class
        {
            if (projection.SequenceEqual(desired, ReferenceEqualityComparer.Instance))
                return;

            var retained = new HashSet<T>(desired, ReferenceEqualityComparer.Instance);
            for (var index = projection.Count - 1; index >= 0; index--)
            {
                if (!retained.Contains(projection[index]))
                    projection.RemoveAt(index);
            }

            for (var index = 0; index < desired.Count; index++)
            {
                var item = desired[index];
                if (index < projection.Count && ReferenceEquals(projection[index], item))
                    continue;

                var previousIndex = -1;
                for (var candidateIndex = index + 1; candidateIndex < projection.Count; candidateIndex++)
                {
                    if (ReferenceEquals(projection[candidateIndex], item))
                    {
                        previousIndex = candidateIndex;
                        break;
                    }
                }
                if (previousIndex >= 0)
                    projection.Move(previousIndex, index);
                else
                    projection.Insert(index, item);
            }
        }

        private int GetConversationActivityPriority(CopilotConversationRecord conversation)
        {
            if (conversation.AgentActivity?.State == CopilotConversationActivityState.NeedsInput)
                return 0;
            if (string.Equals(ActiveHostedRun?.ConversationId, conversation.Id, StringComparison.Ordinal)
                && (ActiveUserQuestion?.IsPending == true
                    || _approvalCoordinator.HasPendingActionsForConversation(conversation.Id)))
            {
                return 0;
            }

            return conversation.AgentActivity?.State switch
            {
                CopilotConversationActivityState.Blocked => 1,
                CopilotConversationActivityState.Ready => 2,
                _ => 3,
            };
        }

        private void NotifyConversationActivitySummary()
        {
            OnPropertyChanged(nameof(ActivityConversationCount));
            OnPropertyChanged(nameof(ActivityConversationCountLabel));
            OnPropertyChanged(nameof(HasConversationActivity));
            OnPropertyChanged(nameof(HasUnreadConversationActivity));
            OnPropertyChanged(nameof(HasNoActivityConversations));
            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshConversationActivityView()
        {
            if (IsActivityViewOpen)
                RefreshFilteredConversations();
            else
                NotifyConversationActivitySummary();
        }

        private void ToggleActivityView()
        {
            IsActivityViewOpen = !IsActivityViewOpen;
        }

        private void MarkAllActivityRead()
        {
            var changed = false;
            foreach (var conversation in Conversations.Where(conversation => !conversation.IsArchived))
                changed |= conversation.AcknowledgeAgentActivityByViewing();
            if (!changed)
                return;

            ClearCompletedAgentRunNotice();
            RefreshAgentRunNotice();
            RefreshConversationActivityView();
            PersistState(immediate: true);
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
            var previousMatch = CurrentConversationFindMatch;
            if (!_conversationFindSession.Refresh(Messages))
                return;

            NotifyConversationFindStateChanged(notifyCurrentMatch: !ReferenceEquals(previousMatch, CurrentConversationFindMatch));
        }

        private void NotifyConversationFindStateChanged(bool notifyCurrentMatch = true)
        {
            OnPropertyChanged(nameof(HasConversationFindMatches));
            OnPropertyChanged(nameof(ConversationFindStatusText));
            if (notifyCurrentMatch)
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
            var existingTasks = new Dictionary<(CopilotConversationRecord, CopilotChatMessage), CopilotAgentTaskSummary>();
            foreach (var task in AgentTasks)
                existingTasks.TryAdd((task.Conversation, task.Message), task);
            var projectedTasks = new List<CopilotAgentTaskSummary>(tasks.Count);
            foreach (var task in tasks)
            {
                if (existingTasks.TryGetValue((task.Conversation, task.Message), out var existing))
                {
                    existing.RefreshPresentation(task.AttentionKind);
                    projectedTasks.Add(existing);
                }
                else
                {
                    projectedTasks.Add(task);
                }
            }
            UpdateProjection(AgentTasks, projectedTasks);

            OnPropertyChanged(nameof(HasAgentTasks));
            OnPropertyChanged(nameof(IsAgentTaskPanelVisible));
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
            OnPropertyChanged(nameof(Attachments));
            OnPropertyChanged(nameof(HasAttachments));
            RefreshCompactHistoryConversations();
            if (HasConversationSearchQuery)
                RefreshFilteredConversations();
            OnCurrentLiveContextStateChanged();
            OnActiveDocumentStateChanged();
            RefreshComposerTokenEstimate();
        }

        private void SelectConversation(CopilotConversationRecord? conversation, bool persist, string? preferredProfileId = null)
        {
            if (conversation?.IsArchived == true
                || (conversation != null && !Conversations.Contains(conversation)))
                return;

            if (IsPromptHistorySearchOpen)
                DismissPromptHistorySearch();

            if (conversation != null && HasConversationSearchQuery && !FilteredConversations.Contains(conversation))
                ConversationSearchText = string.Empty;

            var previousConversation = SelectedConversation;
            var conversationChanged = !ReferenceEquals(previousConversation, conversation);
            if (conversationChanged && IsEditingMessage)
                CancelMessageEdit();

            if (conversationChanged && previousConversation != null)
            {
                previousConversation.Attachments.CollectionChanged -= Attachments_CollectionChanged;
                previousConversation.Messages.CollectionChanged -= Messages_CollectionChanged;
                CopilotConversationFindSession.ClearHighlights(previousConversation.Messages);
            }

            var selection = _conversationSession.SelectConversation(
                conversation,
                preferredProfileId);
            if (!selection.IsAccepted)
                return;

            var selectedConversation = selection.SelectedConversation;
            var activityAcknowledged = selectedConversation?.AcknowledgeAgentActivityByViewing() == true;
            if (activityAcknowledged)
                RefreshConversationActivityView();
            if (!selection.ConversationChanged)
            {
                ApplySelectedProfileTransition(
                    selection.PreviousProfile,
                    selection.SelectedProfile);
                var shouldPersistSameSelection = activityAcknowledged || (persist && selection.StateChanged);
                if (selection.ConversationProfileChanged && selectedConversation != null)
                    selectedConversation.RefreshSummary();
                if (selectedConversation != null
                    && EnsureAssistantHeaders(selectedConversation, selection.SelectedProfile))
                {
                    shouldPersistSameSelection = true;
                }
                RefreshConversationFind();
                RefreshComposerTokenEstimate();
                if (shouldPersistSameSelection)
                    PersistState();
                return;
            }

            _pendingAgentRecoveryRequest = null;
            _promptHistoryNavigator.Reset();
            DismissLocalCommandResult();
            if (selectedConversation != null)
            {
                selectedConversation.Attachments.CollectionChanged += Attachments_CollectionChanged;
                selectedConversation.Messages.CollectionChanged += Messages_CollectionChanged;
            }

            _composerSession.Load(selectedConversation);
            SynchronizeSelectedConversationComposerDraft();
            _ = CaptureHostedTurnSnapshot(Array.Empty<CopilotAttachmentItem>());
            NotifyComposerTextChanged(synchronizeDraft: false);

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

            ApplySelectedProfileTransition(
                selection.PreviousProfile,
                selection.SelectedProfile);
            OnComposerRequestModeChanged();

            var shouldPersist = activityAcknowledged || (persist && selection.StateChanged);
            if (selection.ConversationProfileChanged && selectedConversation != null)
                selectedConversation.RefreshSummary();

            if (selectedConversation != null
                && EnsureAssistantHeaders(selectedConversation, selection.SelectedProfile))
                shouldPersist = true;

            OnCurrentLiveContextStateChanged();
            OnActiveDocumentStateChanged();
            RefreshComposerReferenceSuggestions();

            if (shouldPersist)
                PersistState();
        }

        private bool SynchronizeSelectedConversationComposerDraft()
        {
            var conversation = SelectedConversation;
            if (conversation == null)
                return false;

            var hadDraft = conversation.HasDraft;
            var textChanged = !string.Equals(
                conversation.DraftText,
                _composerSession.Text,
                StringComparison.Ordinal);
            var changed = textChanged;
            if (textChanged)
                conversation.DraftText = _composerSession.Text;

            if (conversation.DraftRequestMode != _composerSession.RequestMode)
            {
                conversation.DraftRequestMode = _composerSession.RequestMode;
                changed = true;
            }

            var reviewTarget = _composerSession.WorkspaceReviewTarget;
            if (!ReviewTargetsEqual(conversation.DraftWorkspaceReviewTarget, reviewTarget))
            {
                conversation.DraftWorkspaceReviewTarget = reviewTarget;
                changed = true;
            }

            var skillReference = _composerSession.AgentSkillReference;
            if (!SkillReferencesEqual(conversation.DraftAgentSkillReference, skillReference))
            {
                conversation.DraftAgentSkillReference = skillReference;
                changed = true;
            }

            if (!changed)
                return false;

            if (hadDraft != conversation.HasDraft)
                RefreshCompactHistoryConversations();
            if (textChanged && HasConversationSearchQuery)
                RefreshFilteredConversations();

            _statePersistenceCoordinator.RequestSave();
            return true;
        }

        private static bool ReviewTargetsEqual(
            CopilotWorkspaceReviewTargetContext? left,
            CopilotWorkspaceReviewTargetContext? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;

            return left.Target == right.Target
                && string.Equals(left.Revision, right.Revision, StringComparison.Ordinal);
        }

        private void SelectProfile(CopilotProfileConfig? profile, bool syncConversation, bool persist)
        {
            var selection = _conversationSession.SelectProfile(
                profile,
                synchronizeConversation: syncConversation);
            if (!selection.Changed)
                return;

            ApplySelectedProfileTransition(
                selection.PreviousProfile,
                selection.SelectedProfile);
            var shouldPersist = persist && selection.StateChanged;
            if (selection.ConversationProfileChanged
                && selection.SelectedConversation != null)
            {
                selection.SelectedConversation.RefreshSummary();

                if (EnsureAssistantHeaders(
                    selection.SelectedConversation,
                    selection.SelectedProfile))
                {
                    shouldPersist = true;
                }
            }

            if (shouldPersist)
                PersistState();

            RefreshComposerTokenEstimate();
        }

        private void ApplySelectedProfileTransition(
            CopilotProfileConfig? previousProfile,
            CopilotProfileConfig? selectedProfile)
        {
            if (ReferenceEquals(previousProfile, selectedProfile))
                return;

            if (previousProfile != null)
                previousProfile.PropertyChanged -= SelectedProfile_PropertyChanged;
            if (selectedProfile != null)
                selectedProfile.PropertyChanged += SelectedProfile_PropertyChanged;

            OnPropertyChanged(nameof(SelectedProfile));
            OnPropertyChanged(nameof(SelectedProfileToolTip));
            RefreshSelectedProfileReasoningState();
        }

        private void SelectedProfile_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CopilotProfileConfig.DisplayLabel)
                && sender is CopilotProfileConfig profile
                && ReferenceEquals(profile, SelectedProfile)
                && SelectedConversation is { } conversation
                && string.Equals(conversation.ProfileId, profile.Id, StringComparison.Ordinal)
                && !string.Equals(
                    conversation.ProfileDisplayName,
                    profile.DisplayLabel,
                    StringComparison.Ordinal))
            {
                conversation.ProfileDisplayName = profile.DisplayLabel;
                UpdateConversationMetadata(conversation, touch: false);
                _statePersistenceCoordinator.RequestSave();
            }

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

            var conversation = _conversationSession.CreateConversation();
            SelectConversation(conversation, persist: false);
            return conversation;
        }

        private CopilotConversationRecord CreateConversation() =>
            _conversationSession.CreateConversation();

        private static CopilotProfileConfig CreateConversationRequestProfile(
            CopilotProfileConfig profile,
            CopilotConversationRecord? conversation,
            CopilotProjectInstructionDiscoveryOptions? codexConfigOptions = null)
        {
            var personality = CopilotResponsePersonalitySelection.Resolve(
                conversation,
                codexConfigOptions);
            return CopilotResponsePresentationGuidance.CreateRequestProfile(
                profile,
                personality.Personality,
                codexConfigOptions?.ModelInstructions);
        }

        private CopilotProfileConfig CreateCurrentConversationRequestProfile(
            CopilotProfileConfig profile,
            CopilotConversationRecord? conversation)
        {
            var codexConfigOptions = CaptureHostedTurnSnapshot(
                Array.Empty<CopilotAttachmentItem>()).ProjectInstructionDiscoveryOptions;
            return CreateConversationRequestProfile(profile, conversation, codexConfigOptions);
        }

        private void UpdateConversationMetadata(CopilotConversationRecord conversation, bool touch)
        {
            if (touch)
                conversation.Touch();

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
                    RefreshAgentTasks();
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
            RefreshAgentTasks();
            PersistState();
            return true;
        }

        private bool CanExportConversation(CopilotConversationRecord? conversation) => !_isExportingConversation
            && Volatile.Read(ref _disposeState) == 0
            && CopilotConversationMarkdownExporter.CanExport(conversation);


    }
}

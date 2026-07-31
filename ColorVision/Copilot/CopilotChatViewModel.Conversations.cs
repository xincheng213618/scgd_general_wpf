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
            IsConversationFindOpen = true;
            var normalized = CopilotConversationFindNavigator.NormalizeQuery(query);
            if (!string.Equals(ConversationFindText, normalized, StringComparison.Ordinal))
                ConversationFindText = normalized;
            else
                RefreshConversationFind();
        }

        public void CloseConversationFind()
        {
            if (!IsConversationFindOpen)
                return;

            ClearConversationFindState(Messages);
            _conversationFindNavigator.Refresh([], string.Empty);
            IsConversationFindOpen = false;
            NotifyConversationFindStateChanged();
        }

        public bool MoveConversationFind(bool previous)
        {
            if (!IsConversationFindOpen || !_conversationFindNavigator.Move(previous))
                return false;

            ApplyConversationFindState();
            NotifyConversationFindStateChanged();
            return true;
        }

        internal void RefreshConversationFind()
        {
            if (!IsConversationFindOpen)
                return;

            _conversationFindNavigator.Refresh(Messages, ConversationFindText);
            ApplyConversationFindState();
            NotifyConversationFindStateChanged();
        }

        private void ApplyConversationFindState()
        {
            var matches = _conversationFindNavigator.Matches.ToHashSet();
            var current = _conversationFindNavigator.Current;
            foreach (var message in Messages)
                message.SetConversationFindState(matches.Contains(message), ReferenceEquals(message, current));
        }

        private static void ClearConversationFindState(IEnumerable<CopilotChatMessage>? messages)
        {
            foreach (var message in messages ?? Array.Empty<CopilotChatMessage>())
                message?.SetConversationFindState(isMatch: false, isCurrent: false);
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

            ClearConversationFindState(_selectedConversation?.Messages);
            _selectedConversation = conversation;
            _pendingRequestModeOverride = conversation?.DraftRequestMode is { } restoredMode
                && restoredMode != CopilotAgentMode.Auto
                    ? restoredMode
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

            _stateSaveScheduler.RequestSave();
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
            BringConversationToFront(conversation);
            RefreshFilteredConversations();
            RefreshComposerTokenEstimate();
        }

        private void QueueConversationTitleGeneration(CopilotConversationRecord conversation, CopilotProfileConfig requestProfile)
        {
            if (Volatile.Read(ref _disposeState) == 1
                || !CopilotConversationTitleGenerator.TryCreateRequest(conversation, requestProfile, out var request))
                return;

            CancelConversationTitleGeneration(conversation.Id);
            var generation = new CopilotNonBlockingCancellationSource();
            _conversationTitleGenerations[conversation.Id] = generation;
            _ = GenerateConversationTitleAsync(conversation, request, generation);
        }

        private async Task GenerateConversationTitleAsync(
            CopilotConversationRecord conversation,
            CopilotConversationTitleRequest request,
            CopilotNonBlockingCancellationSource generation)
        {
            try
            {
                var cancellationToken = generation.Token;
                var generatedTitle = await _conversationTitleGenerator.GenerateAsync(request, cancellationToken);
                if (string.IsNullOrWhiteSpace(generatedTitle) || cancellationToken.IsCancellationRequested || Application.Current == null)
                    return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (cancellationToken.IsCancellationRequested
                        || !IsCurrentConversationTitleGeneration(conversation.Id, generation)
                        || !Conversations.Contains(conversation)
                        || conversation.HasCustomTitle)
                    {
                        return;
                    }

                    conversation.SetGeneratedTitle(generatedTitle);
                    RefreshFilteredConversations();
                    PersistState();
                });
            }
            catch (OperationCanceledException) when (generation.IsCancellationRequested)
            {
            }
            catch
            {
            }
            finally
            {
                CompleteConversationTitleGeneration(conversation.Id, generation);
            }
        }

        private bool IsCurrentConversationTitleGeneration(string conversationId, CopilotNonBlockingCancellationSource generation) =>
            _conversationTitleGenerations.TryGetValue(conversationId, out var current) && ReferenceEquals(current, generation);

        private void CompleteConversationTitleGeneration(string conversationId, CopilotNonBlockingCancellationSource generation)
        {
            if (IsCurrentConversationTitleGeneration(conversationId, generation))
                _conversationTitleGenerations.Remove(conversationId);
            generation.Dispose();
        }

        private void CancelConversationTitleGeneration(string conversationId)
        {
            if (!_conversationTitleGenerations.Remove(conversationId, out var generation))
                return;

            generation.RequestCancellation();
        }

        private void CancelAllConversationTitleGenerations()
        {
            var generations = _conversationTitleGenerations.Values.ToArray();
            _conversationTitleGenerations.Clear();
            foreach (var generation in generations)
                generation.RequestCancellation();
        }

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

        private void BringConversationToFront(CopilotConversationRecord conversation)
        {
            CopilotConversationService.MoveToPreferredIndex(Conversations, conversation);
            _state.ActiveConversationId = conversation.Id;
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

            CancelConversationTitleGeneration(conversation.Id);
            conversation.SetCustomTitle(normalizedTitle);
            RefreshFilteredConversations();
            PersistState();
            return true;
        }

        private bool CanExportConversation(CopilotConversationRecord? conversation) => !_isExportingConversation
            && Volatile.Read(ref _disposeState) == 0
            && CopilotConversationMarkdownExporter.CanExport(conversation);

        private async Task ExportConversationFromCommandAsync(CopilotLocalCommand command, string requestedFileName)
        {
            var conversation = SelectedConversation;
            if (!CanExportConversation(conversation))
            {
                ShowLocalCommandResult(
                    command,
                    _isExportingConversation
                        ? "已有会话导出正在执行，请完成后再试。"
                        : "当前会话还没有可导出的已完成消息。");
                return;
            }

            if (!string.IsNullOrWhiteSpace(requestedFileName))
            {
                if (!CopilotConversationMarkdownExporter.TryNormalizeFileNameHint(
                    requestedFileName,
                    out var fileName,
                    out var errorMessage))
                {
                    ShowLocalCommandResult(command, errorMessage);
                    return;
                }

                await ExportConversationAsync(conversation, fileName);
                return;
            }

            var snapshot = CopilotConversationMarkdownExporter.Capture(conversation!);
            var cancellation = BeginAuxiliaryOperation();
            _isExportingConversation = true;
            ShowLocalCommandResult(command, "正在生成当前会话的可见 Markdown 快照。");
            CommandManager.InvalidateRequerySuggested();
            try
            {
                var markdown = await Task.Run(
                    () => CopilotConversationMarkdownExporter.BuildMarkdown(snapshot, cancellation.Token),
                    cancellation.Token);
                if (Volatile.Read(ref _disposeState) == 1)
                    return;
                if (!TrySetClipboardText(markdown, out var errorMessage))
                {
                    ShowLocalCommandResult(command, "复制失败：" + errorMessage);
                    return;
                }

                ShowLocalCommandResult(
                    command,
                    $"已复制当前会话的可见 Markdown（{snapshot.Messages.Count:N0} 条消息，{markdown.Length:N0} 个字符）。");
            }
            finally
            {
                _isExportingConversation = false;
                CompleteAuxiliaryOperation(cancellation);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task OpenFeedbackAsync(string report)
        {
            DismissLocalCommandResult();
            var draft = CopilotFeedbackDraftBuilder.Create(SelectedConversation, report);
            string? temporaryConversationPath = null;
            try
            {
                if (draft.HasConversationAttachment)
                {
                    temporaryConversationPath = Path.Combine(
                        Path.GetTempPath(),
                        $"ColorVision_Copilot_Conversation_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.md");
                    await File.WriteAllTextAsync(
                        temporaryConversationPath,
                        draft.ConversationMarkdown,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }

                if (Volatile.Read(ref _disposeState) == 1)
                    return;

                var attachmentPaths = temporaryConversationPath == null
                    ? Array.Empty<string>()
                    : new[] { temporaryConversationPath };
                var window = new FeedbackWindow(draft.Report, attachmentPaths)
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };
                window.ShowDialog();
            }
            finally
            {
                try
                {
                    if (temporaryConversationPath != null && File.Exists(temporaryConversationPath))
                        File.Delete(temporaryConversationPath);
                }
                catch
                {
                }
            }
        }

        private async Task ExportConversationAsync(
            CopilotConversationRecord? conversation,
            string? suggestedFileName = null)
        {
            if (!CanExportConversation(conversation))
                return;

            var dialog = new SaveFileDialog
            {
                AddExtension = true,
                CheckPathExists = true,
                DefaultExt = ".md",
                FileName = suggestedFileName ?? CopilotConversationMarkdownExporter.BuildFileName(conversation!),
                Filter = "Markdown 文档|*.md|文本文件|*.txt|所有文件|*.*",
                OverwritePrompt = true,
                Title = "导出 Copilot 会话",
            };

            if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true)
                return;

            var snapshot = CopilotConversationMarkdownExporter.Capture(conversation!);
            var cancellation = BeginAuxiliaryOperation();
            _isExportingConversation = true;
            LocalCommandResultTitle = "正在导出会话";
            LocalCommandResultText = dialog.FileName;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                var markdown = await Task.Run(
                    () => CopilotConversationMarkdownExporter.BuildMarkdown(snapshot, cancellation.Token),
                    cancellation.Token);
                await WriteConversationExportAsync(dialog.FileName, markdown, cancellation.Token);
                if (Volatile.Read(ref _disposeState) == 1)
                    return;

                LocalCommandResultTitle = "会话已导出";
                LocalCommandResultText = dialog.FileName;
            }
            finally
            {
                _isExportingConversation = false;
                CompleteAuxiliaryOperation(cancellation);
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private static async Task WriteConversationExportAsync(string filePath, string content, CancellationToken cancellationToken)
        {
            var destinationPath = Path.GetFullPath(filePath);
            var directoryPath = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException("导出目录不存在或已不可用。");

            var temporaryPath = Path.Combine(directoryPath, $".copilot-export-{Guid.NewGuid():N}.tmp");
            try
            {
                await File.WriteAllTextAsync(
                    temporaryPath,
                    content,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch
                {
                }
            }
        }

        private void DeleteCurrentConversation(CopilotLocalCommand command)
        {
            var target = SelectedConversation;
            if (!CanDeleteConversation(target))
            {
                ShowLocalCommandResult(
                    command,
                    "当前状态不能永久删除会话；请先结束运行、导出或其他独占操作。");
                return;
            }

            if (TryDeleteConversation(target, out var deletedTitle))
            {
                ShowLocalCommandResult(
                    command,
                    $"已永久删除“{deletedTitle}”。本地消息、草稿和托管附件已移除，不能通过 /unarchive 恢复。");
            }
        }

        private void DeleteConversation(CopilotConversationRecord? conversation) =>
            TryDeleteConversation(conversation, out _);

        private bool TryDeleteConversation(
            CopilotConversationRecord? conversation,
            out string deletedTitle)
        {
            deletedTitle = string.Empty;
            if (!CanDeleteConversation(conversation))
                return false;

            var target = conversation!;
            var activeBackgroundCommands =
                CopilotBackgroundShellCommandRegistry.Shared.GetSnapshots(target.Id)
                    .Count(snapshot => snapshot.IsActive);
            if (activeBackgroundCommands > 0)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"无法永久删除“{target.Title}”：当前会话还有 {activeBackgroundCommands:N0} 条后台命令在运行。"
                    + $"{Environment.NewLine}{Environment.NewLine}请先切换到该会话，使用 /ps 查看并停止后台命令；进程树未改变。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            var retentionBlocker = GetConversationRetentionBlocker(target);
            if (retentionBlocker != CopilotConversationRetentionBlocker.None)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"无法永久删除“{target.Title}”：{CopilotConversationRetentionPolicy.Describe(retentionBlocker)}。"
                    + $"{Environment.NewLine}{Environment.NewLine}请先处理或明确放弃该状态；若只想隐藏安全会话，请使用 /archive。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (MessageBox.Show(
                Application.Current.GetActiveWindow(),
                $"永久删除“{target.Title}”？"
                + $"{Environment.NewLine}{Environment.NewLine}本地消息、草稿和托管附件会被移除，且不能通过 /unarchive 恢复。"
                + $"{Environment.NewLine}若只想隐藏，请选择“否”并使用 /archive。",
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return false;
            }

            deletedTitle = target.Title;
            var wasSelected = ReferenceEquals(target, SelectedConversation);
            CancelConversationTitleGeneration(target.Id);
            var managedAttachments = target.EnumerateReferencedAttachments().ToArray();
            ClearAgentRunNoticeForConversation(target.Id);
            AcknowledgeCompletionNotices(target.Id);

            var currentIndex = Conversations.IndexOf(target);
            if (!Conversations.Remove(target))
            {
                deletedTitle = string.Empty;
                return false;
            }

            RemoveQueuedFollowUpRecoveryRecords(target.Id);
            CopilotBackgroundShellCommandRegistry.Shared.ClearCompleted(target.Id);
            CopilotShellCommandOutputArchiveRegistry.Shared.ClearConversation(
                target.Id);
            RemoveManagedAttachmentFiles(managedAttachments);

            if (wasSelected)
            {
                var replacement = CopilotConversationRetentionPolicy.FindNearestActive(
                    Conversations,
                    currentIndex)
                    ?? CreateConversation();
                SelectConversation(replacement, persist: false);
            }

            PersistState(immediate: true);
            return true;
        }

        private bool CanDeleteConversation(CopilotConversationRecord? conversation) =>
            Volatile.Read(ref _disposeState) == 0
            && conversation != null
            && Conversations.Contains(conversation)
            && !IsBusy
            && !HasExclusiveLocalOperation
            && !_isExportingConversation;

        private void RemoveQueuedFollowUpRecoveryRecords(string conversationId)
        {
            if (_state.QueuedFollowUpRecoveries == null)
                return;

            for (var index = _state.QueuedFollowUpRecoveries.Count - 1; index >= 0; index--)
            {
                if (string.Equals(
                    _state.QueuedFollowUpRecoveries[index]?.ConversationId,
                    conversationId,
                    StringComparison.Ordinal))
                {
                    _state.QueuedFollowUpRecoveries.RemoveAt(index);
                }
            }
        }

        private void TogglePinConversation(CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return;

            conversation.IsPinned = !conversation.IsPinned;
            CopilotConversationService.MoveToPreferredIndex(Conversations, conversation);
            PersistState();
        }

    }
}

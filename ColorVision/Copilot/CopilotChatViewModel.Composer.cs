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
        public bool TryAcceptPromptHistoryPrefixCompletion()
        {
            if (!TryResolvePromptHistoryPrefixCompletion(out var completion))
                return false;

            InputText = completion.FullText;
            return true;
        }

        private bool TryResolvePromptHistoryPrefixCompletion(
            out CopilotPromptHistoryPrefixCompletion completion)
        {
            completion = default;
            return PromptHistoryCompletionsEnabled
                && !IsEditingMessage
                && !IsPromptHistorySearchOpen
                && !IsComposerReferenceMentionActive
                && !HasLocalCommandSuggestions
                && CopilotPromptHistoryPrefixCompletionResolver.TryResolve(
                    SelectedConversation?.Messages,
                    InputText,
                    out completion);
        }

        private void NotifyPromptHistoryPrefixCompletionChanged()
        {
            OnPropertyChanged(nameof(HasPromptHistoryPrefixCompletion));
            OnPropertyChanged(nameof(PromptHistoryPrefixCompletionText));
        }

        public bool TryNavigatePromptHistory(bool previous)
        {
            if (IsEditingMessage
                || IsPromptHistorySearchOpen
                || !_promptHistoryNavigator.TryNavigate(SelectedConversation, InputText, previous, out var text))
            {
                return false;
            }

            ApplyPromptHistoryText(text);
            return true;
        }

        public bool CancelPromptHistoryNavigation()
        {
            if (!_promptHistoryNavigator.TryCancel(out var draft))
                return false;

            ApplyPromptHistoryText(draft);
            return true;
        }

        private void ApplyPromptHistoryText(string text)
        {
            _isApplyingPromptHistory = true;
            try
            {
                InputText = text;
            }
            finally
            {
                _isApplyingPromptHistory = false;
            }
        }

        public bool TryOpenPromptHistorySearch()
        {
            var conversation = SelectedConversation;
            if (IsPromptHistorySearchOpen || IsBusy || IsEditingMessage || conversation == null)
                return false;

            _promptHistorySearchScope = CopilotPromptHistorySearchScope.CurrentConversation;
            var initialResults = CopilotPromptHistorySearch.Search(conversation.Messages, string.Empty);
            if (initialResults.Count == 0)
            {
                initialResults = CopilotPromptHistorySearch.SearchAll(
                    CopilotConversationArchiveService.GetActive(Conversations),
                    string.Empty);
                _promptHistorySearchScope = CopilotPromptHistorySearchScope.AllConversations;
            }
            if (initialResults.Count == 0)
                return false;

            _promptHistorySearchConversationId = conversation.Id;
            _promptHistorySearchDraft = InputText;
            _promptHistoryNavigator.Reset();
            DismissComposerReferenceSuggestions();
            IsPromptHistorySearchOpen = true;
            if (InputText.Length > 0)
                InputText = string.Empty;
            else
                RefreshPromptHistorySearchResults();
            return true;
        }

        public bool TryTogglePromptHistorySearchScope()
        {
            if (!IsPromptHistorySearchOpen)
                return false;

            _promptHistorySearchScope =
                _promptHistorySearchScope == CopilotPromptHistorySearchScope.CurrentConversation
                    ? CopilotPromptHistorySearchScope.AllConversations
                    : CopilotPromptHistorySearchScope.CurrentConversation;
            RefreshPromptHistorySearchResults();
            return true;
        }

        public void DismissPromptHistorySearch()
        {
            if (!IsPromptHistorySearchOpen)
                return;

            ClosePromptHistorySearch(_promptHistorySearchDraft);
        }

        public bool TryNavigatePromptHistorySearch(bool previous)
        {
            if (!HasPromptHistorySearchResults)
                return false;

            var currentIndex = SelectedPromptHistorySearchResult == null
                ? -1
                : PromptHistorySearchResults.IndexOf(SelectedPromptHistorySearchResult);
            var nextIndex = CopilotSuggestionSelection.Move(
                currentIndex,
                PromptHistorySearchResults.Count,
                previous);
            SelectedPromptHistorySearchResult = PromptHistorySearchResults[nextIndex];
            return true;
        }

        public bool TryCompletePromptHistorySearch(
            CopilotPromptHistorySearchItem? result = null)
        {
            result ??= SelectedPromptHistorySearchResult ?? PromptHistorySearchResults.FirstOrDefault();
            if (!IsPromptHistorySearchOpen
                || result == null
                || !PromptHistorySearchResults.Contains(result))
            {
                return false;
            }

            ClosePromptHistorySearch(result.Text);
            return true;
        }

        private void RefreshPromptHistorySearchResults()
        {
            var conversation = SelectedConversation;
            if (!IsPromptHistorySearchOpen
                || conversation == null
                || !string.Equals(
                    conversation.Id,
                    _promptHistorySearchConversationId,
                    StringComparison.Ordinal))
            {
                return;
            }

            var preferredText = SelectedPromptHistorySearchResult?.Text;
            var results = _promptHistorySearchScope == CopilotPromptHistorySearchScope.AllConversations
                ? CopilotPromptHistorySearch.SearchAll(
                    CopilotConversationArchiveService.GetActive(Conversations),
                    InputText)
                : CopilotPromptHistorySearch.Search(conversation.Messages, InputText);
            PromptHistorySearchResults.Clear();
            foreach (var result in results)
                PromptHistorySearchResults.Add(result);

            SelectedPromptHistorySearchResult = PromptHistorySearchResults.FirstOrDefault(item =>
                string.Equals(item.Text, preferredText, StringComparison.Ordinal))
                ?? PromptHistorySearchResults.FirstOrDefault();
            OnPropertyChanged(nameof(HasPromptHistorySearchResults));
            OnPropertyChanged(nameof(PromptHistorySearchHeader));
            OnPropertyChanged(nameof(PromptHistorySearchStatusText));
            OnPropertyChanged(nameof(PromptHistorySearchScopeLabel));
            OnPropertyChanged(nameof(InputPlaceholder));
        }

        private void ClosePromptHistorySearch(string restoredText)
        {
            IsPromptHistorySearchOpen = false;
            _promptHistorySearchConversationId = string.Empty;
            _promptHistorySearchDraft = string.Empty;
            _promptHistorySearchScope = CopilotPromptHistorySearchScope.CurrentConversation;
            PromptHistorySearchResults.Clear();
            SelectedPromptHistorySearchResult = null;
            InputText = restoredText ?? string.Empty;
            OnPropertyChanged(nameof(HasPromptHistorySearchResults));
            OnPropertyChanged(nameof(PromptHistorySearchHeader));
            OnPropertyChanged(nameof(PromptHistorySearchStatusText));
            OnPropertyChanged(nameof(PromptHistorySearchScopeLabel));
        }

        public bool TryToggleComposerStash(int caretIndex, out int restoredCaretIndex)
        {
            restoredCaretIndex = -1;
            var conversation = SelectedConversation;
            if (conversation == null)
                return false;

            if (IsPromptHistorySearchOpen)
            {
                ShowComposerStashFeedback("暂存不可用", "请先完成或关闭历史请求搜索。");
                return true;
            }
            if (IsEditingMessage)
            {
                ShowComposerStashFeedback("暂存不可用", "请先完成或取消当前消息编辑。");
                return true;
            }
            if (HasExclusiveLocalOperation)
            {
                ShowComposerStashFeedback("暂存不可用", "请等待当前附件、上下文或会话压缩操作完成。");
                return true;
            }

            var hasComposerContent = InputText.Length > 0 || conversation.Attachments.Count > 0;
            if (hasComposerContent)
            {
                if (conversation.HasComposerStash)
                {
                    ShowComposerStashFeedback(
                        "已有暂存草稿",
                        "现有暂存不会被覆盖。请先发送或移走当前输入，再在空输入框按 Ctrl+S 恢复。");
                    return true;
                }

                var capturedStash = CopilotComposerStash.Capture(
                    InputText,
                    caretIndex,
                    ResolveComposerRequestMode(),
                    conversation.Attachments);
                conversation.ComposerStash = capturedStash;
                conversation.Attachments.Clear();
                InputText = string.Empty;
                ClearPendingRequestModeOverride();
                UpdateAttachmentsState(conversation);
                NotifyComposerStashChanged();
                ShowComposerStashFeedback(
                    "草稿已暂存",
                    $"已保存 {capturedStash.Text.Length:N0} 个字符和 {capturedStash.Attachments.Count:N0} 个附件；空输入框按 Ctrl+S 可恢复。");
                return true;
            }

            var stash = conversation.ComposerStash;
            if (stash?.HasContent != true)
            {
                ShowComposerStashFeedback("没有暂存草稿", "请先输入内容或添加附件，再按 Ctrl+S 暂存。");
                return true;
            }

            var attachmentSnapshots = stash.CreateAttachmentSnapshots();
            conversation.ComposerStash = null;
            foreach (var attachment in attachmentSnapshots)
                conversation.Attachments.Add(attachment);
            InputText = stash.Text;
            SetPendingRequestModeOverride(stash.RequestMode);
            restoredCaretIndex = Math.Clamp(stash.CaretIndex, 0, InputText.Length);
            UpdateAttachmentsState(conversation);
            NotifyComposerStashChanged();
            ShowComposerStashFeedback(
                "草稿已恢复",
                $"已恢复 {InputText.Length:N0} 个字符和 {attachmentSnapshots.Count:N0} 个附件；内容尚未发送。");
            return true;
        }

        private void NotifyComposerStashChanged()
        {
            OnPropertyChanged(nameof(HasComposerStash));
            OnPropertyChanged(nameof(ComposerStashToolTip));
            RefreshCompactHistoryConversations();
            if (HasConversationSearchQuery)
                RefreshFilteredConversations();
        }

        private void ShowComposerStashFeedback(string title, string text)
        {
            LocalCommandResultTitle = title;
            LocalCommandResultText = text;
        }


        public IReadOnlyList<CopilotReasoningOption> SelectedProfileReasoningOptions => CopilotReasoningCapabilities.GetOptions(SelectedProfile);

        public string SelectedProfileReasoningLabel => CopilotReasoningCapabilities.GetLabel(CopilotReasoningCapabilities.GetEffectiveMode(SelectedProfile));

        public string SelectedProfileReasoningToolTip => CopilotReasoningCapabilities.GetToolTip(SelectedProfile);

        public bool HasConfigurableReasoning => CopilotReasoningCapabilities.HasConfigurableReasoning(SelectedProfile);

        public void SetSelectedProfileReasoningMode(CopilotReasoningMode mode)
        {
            var profile = SelectedProfile;
            if (profile == null || !HasConfigurableReasoning)
                return;

            var normalized = CopilotReasoningCapabilities.Normalize(profile.VendorType, mode);
            if (profile.ReasoningMode == normalized)
                return;

            profile.ReasoningMode = normalized;
            PersistConfig();
            RefreshSelectedProfileReasoningState();
        }
        private string _inputText = string.Empty;

        public string InputPlaceholder => IsPromptHistorySearchOpen
            ? $"搜索{PromptHistorySearchScopeLabel}的可见历史请求"
            : IsEditingMessage
            ? $"修改后按 {ComposerSubmitShortcutLabel} 重新发送"
            : IsViewingActiveRun
                ? IsAnsweringUserQuestion
                    ? $"输入问题答案并按 {ComposerSubmitShortcutLabel}；也可直接选择上方选项"
                    : ActiveHostedRun?.State switch
                    {
                        CopilotHostedRunState.PauseRequested => "任务正在暂停 · 当前输入会保留到任务结束",
                        CopilotHostedRunState.CancelRequested => "任务正在取消 · 当前输入会保留到任务结束",
                        _ when IsAgentRequestActive => $"{ComposerSubmitShortcutLabel} {DefaultFollowUpActionLabel} · Tab {AlternateFollowUpActionLabel} · Ctrl+Enter 立即接管 · @ 关联",
                        _ => "正在生成回复 · 可使用 /status",
                    }
                : ResolveComposerRequestMode() == CopilotAgentMode.Plan
                    ? "计划模式 · 输入任务；只读分析，不执行修改"
                : IsConversationEmpty ? "随心输入 · @ 关联 · / 或 $ 命令" : "要求后续变更 · @ 关联 · / 或 $ 命令";

        private string ComposerSubmitShortcutLabel => UseMultilineComposer ? "Shift+Enter" : "Enter";

        private string DefaultFollowUpActionLabel =>
            DefaultFollowUpBehavior == CopilotFollowUpBehavior.Queue ? "排队" : "调整";

        private string AlternateFollowUpActionLabel =>
            DefaultFollowUpBehavior == CopilotFollowUpBehavior.Queue ? "调整" : "排队";

        private string ResolveFollowUpShortcut(CopilotFollowUpBehavior behavior) =>
            behavior == DefaultFollowUpBehavior ? ComposerSubmitShortcutLabel : "Tab";

        public bool IsEditingMessage => !string.IsNullOrWhiteSpace(_editingConversationId)
            && !string.IsNullOrWhiteSpace(_editingUserMessageId);

        public bool CanOpenExpandedComposerEditor =>
            !IsPromptHistorySearchOpen && !IsComposerReferenceMentionActive;

        public string EditingMessageStatusText => "正在编辑上一条请求；发送后将替换原回复";

        public bool IsInputEmpty => string.IsNullOrWhiteSpace(InputText);

        public IReadOnlyList<CopilotLocalCommand> LocalCommandSuggestions
        {
            get
            {
                if (IsEditingMessage || IsPromptHistorySearchOpen)
                    return Array.Empty<CopilotLocalCommand>();

                var composerContext = ResolveLocalCommandComposerContext();
                if (!CopilotLocalCommandAvailabilityPolicy.CanShowSuggestions(composerContext))
                    return Array.Empty<CopilotLocalCommand>();

                var input = (InputText ?? string.Empty).TrimStart();
                if (input.Length == 0 || input[0] is not '/' and not '$')
                    return Array.Empty<CopilotLocalCommand>();
                return CopilotLocalCommandCatalog.Suggest(
                    input,
                    DiscoverComposerSkills(),
                    Profiles,
                    SelectedProfile,
                    composerContext,
                    SelectedConversation);
            }
        }

        public string LocalCommandSuggestionHeader => ResolveLocalCommandComposerContext()
            == CopilotLocalCommandComposerContext.ActiveRun
                ? "运行中可用命令或 Skill"
                : "/ 或 $ 命令";

        public bool HasLocalCommandSuggestions => LocalCommandSuggestions.Count > 0;

        public int SelectedLocalCommandSuggestionIndex
        {
            get => _selectedLocalCommandSuggestionIndex;
            set => SetProperty(ref _selectedLocalCommandSuggestionIndex, value);
        }

        public bool TryNavigateLocalCommandSuggestion(bool previous)
        {
            var suggestions = LocalCommandSuggestions;
            if (suggestions.Count == 0)
            {
                SelectedLocalCommandSuggestionIndex = -1;
                return false;
            }

            SelectedLocalCommandSuggestionIndex = CopilotSuggestionSelection.Move(
                SelectedLocalCommandSuggestionIndex,
                suggestions.Count,
                previous);
            return true;
        }

        public bool HasComposerReferenceSuggestions => ComposerReferenceSuggestions.Count > 0;

        public bool IsComposerReferenceMentionActive
        {
            get => _isComposerReferenceMentionActive;
            private set
            {
                if (!SetProperty(ref _isComposerReferenceMentionActive, value))
                    return;

                OnPropertyChanged(nameof(IsComposerReferencePopoverOpen));
                OnPropertyChanged(nameof(HasComposerReferenceStatus));
                OnPropertyChanged(nameof(ComposerReferenceStatusText));
                OnPropertyChanged(nameof(CanOpenExpandedComposerEditor));
                NotifyPromptHistoryPrefixCompletionChanged();
            }
        }

        public bool IsComposerReferenceSearchPending
        {
            get => _isComposerReferenceSearchPending;
            private set
            {
                if (!SetProperty(ref _isComposerReferenceSearchPending, value))
                    return;

                OnPropertyChanged(nameof(HasComposerReferenceStatus));
                OnPropertyChanged(nameof(ComposerReferenceStatusText));
            }
        }

        public bool IsComposerReferencePopoverOpen => IsComposerReferenceMentionActive;

        public bool HasComposerReferenceStatus =>
            IsComposerReferenceMentionActive && !HasComposerReferenceSuggestions;

        public string ComposerReferenceStatusText => IsComposerReferenceSearchPending
            ? "正在索引工作区文件…"
            : "未找到关联项，请继续输入或按 Esc 关闭";

        public CopilotComposerReferenceItem? SelectedComposerReference
        {
            get => _selectedComposerReference;
            set => SetProperty(ref _selectedComposerReference, value);
        }

        public bool TryNavigateComposerReference(bool previous)
        {
            if (!HasComposerReferenceSuggestions)
                return false;

            var currentIndex = SelectedComposerReference == null
                ? -1
                : ComposerReferenceSuggestions.IndexOf(SelectedComposerReference);
            var nextIndex = CopilotSuggestionSelection.Move(
                currentIndex,
                ComposerReferenceSuggestions.Count,
                previous);
            SelectedComposerReference = ComposerReferenceSuggestions[nextIndex];
            return true;
        }

        public bool TryCompleteComposerReference(CopilotComposerReferenceItem? reference = null)
        {
            reference ??= SelectedComposerReference ?? ComposerReferenceSuggestions.FirstOrDefault();
            if (reference == null
                || !CopilotComposerReferenceCatalog.TryParseMention(InputText, out var mention))
            {
                return false;
            }

            var conversation = EnsureConversation();
            var associated = reference.Kind == CopilotComposerReferenceKind.File
                ? AddResolvedFileAttachments([reference.Value], conversation) > 0
                    || conversation.Attachments.Any(item =>
                        (item.Type is CopilotAttachmentType.File or CopilotAttachmentType.Image)
                        && string.Equals(item.Value, reference.Value, StringComparison.OrdinalIgnoreCase))
                : AttachExternalContextSnapshot(
                    conversation,
                    reference.Title,
                    reference.SourceId,
                    [new CopilotContextItem
                    {
                        Id = reference.SourceId,
                        Title = reference.Title,
                        Summary = reference.Subtitle,
                        Content = reference.ContextContent,
                    }]);
            if (!associated)
                return false;

            InputText = CopilotComposerReferenceCatalog.CompleteMention(InputText, mention, reference.Title);
            return true;
        }

        public void DismissComposerReferenceSuggestions()
        {
            CancelComposerReferenceRefresh(resetSession: true);
            IsComposerReferenceMentionActive = false;
            IsComposerReferenceSearchPending = false;
            ClearComposerReferenceSuggestions();
        }

        private void ClearComposerReferenceSuggestions()
        {
            ComposerReferenceSuggestions.Clear();
            SelectedComposerReference = null;
            OnPropertyChanged(nameof(HasComposerReferenceSuggestions));
            OnPropertyChanged(nameof(HasComposerReferenceStatus));
        }

        private void RefreshComposerReferenceSuggestions()
        {
            if (IsPromptHistorySearchOpen)
            {
                DismissComposerReferenceSuggestions();
                return;
            }

            var input = InputText;
            if (!CopilotComposerReferenceCatalog.TryParseMention(input, out var mention))
            {
                DismissComposerReferenceSuggestions();
                return;
            }

            var workspaceRoot = SolutionManager.GetInstance().CurrentSolutionExplorer?.DirectoryInfo?.FullName ?? string.Empty;
            var previousValue = SelectedComposerReference?.Value;
            var sessionKey = string.Join('\n',
                SelectedConversation?.Id ?? string.Empty,
                workspaceRoot,
                mention.StartIndex,
                input[..mention.StartIndex]);
            var refreshIndex = !string.Equals(
                sessionKey,
                _composerReferenceSessionKey,
                StringComparison.Ordinal);
            _composerReferenceSessionKey = sessionKey;

            CancelComposerReferenceRefresh(resetSession: false);
            IsComposerReferenceMentionActive = true;
            var version = Interlocked.Increment(ref _composerReferenceRefreshVersion);
            var cancellation = new CopilotNonBlockingCancellationSource();
            _composerReferenceRefreshCts = cancellation;
            var immediateSuggestions = CopilotComposerReferenceCatalog.SearchImmediate(
                mention.Query,
                _activeDocumentPath);
            ApplyComposerReferenceSuggestions(immediateSuggestions, previousValue);

            if (!string.IsNullOrWhiteSpace(workspaceRoot))
            {
                IsComposerReferenceSearchPending = true;
                _ = RefreshWorkspaceComposerReferencesAsync(
                    mention.Query,
                    workspaceRoot,
                    refreshIndex,
                    immediateSuggestions,
                    previousValue,
                    version,
                    cancellation.Token);
            }
            else
            {
                IsComposerReferenceSearchPending = false;
            }
        }

        private async Task RefreshWorkspaceComposerReferencesAsync(
            string query,
            string workspaceRoot,
            bool refreshIndex,
            IReadOnlyList<CopilotComposerReferenceItem> immediateSuggestions,
            string? preferredValue,
            long version,
            CancellationToken cancellationToken)
        {
            try
            {
                var workspaceSuggestions = await CopilotComposerReferenceCatalog.SearchWorkspaceReferencesAsync(
                    workspaceRoot,
                    query,
                    refreshIndex,
                    cancellationToken);
                if (cancellationToken.IsCancellationRequested
                    || version != Volatile.Read(ref _composerReferenceRefreshVersion)
                    || Volatile.Read(ref _disposeState) == 1)
                {
                    return;
                }

                IsComposerReferenceSearchPending = false;
                var merged = CopilotComposerReferenceCatalog.MergeSearchResults(
                    query,
                    immediateSuggestions,
                    workspaceSuggestions);
                ApplyComposerReferenceSuggestions(merged, preferredValue);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch
            {
                // Template and menu references remain available if file indexing fails.
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested
                    && version == Volatile.Read(ref _composerReferenceRefreshVersion)
                    && Volatile.Read(ref _disposeState) == 0)
                {
                    IsComposerReferenceSearchPending = false;
                }
            }
        }

        private void ApplyComposerReferenceSuggestions(
            IReadOnlyList<CopilotComposerReferenceItem> suggestions,
            string? preferredValue)
        {
            ComposerReferenceSuggestions.Clear();
            foreach (var suggestion in suggestions)
                ComposerReferenceSuggestions.Add(suggestion);

            SelectedComposerReference = ComposerReferenceSuggestions.FirstOrDefault(item =>
                string.Equals(item.Value, preferredValue, StringComparison.OrdinalIgnoreCase))
                ?? ComposerReferenceSuggestions.FirstOrDefault();
            OnPropertyChanged(nameof(HasComposerReferenceSuggestions));
            OnPropertyChanged(nameof(HasComposerReferenceStatus));
        }

        private void CancelComposerReferenceRefresh(bool resetSession)
        {
            Interlocked.Increment(ref _composerReferenceRefreshVersion);
            var cancellation = Interlocked.Exchange(ref _composerReferenceRefreshCts, null);
            if (cancellation != null)
            {
                cancellation.RequestCancellation();
                cancellation.Dispose();
            }
            if (resetSession)
                _composerReferenceSessionKey = string.Empty;
        }

        public bool TryCompleteLocalCommand(CopilotLocalCommand? command = null)
        {
            var suggestions = LocalCommandSuggestions;
            command ??= GetSelectedLocalCommandSuggestion(suggestions);
            if (command == null)
                return false;

            InputText = command.CompletionText;
            return true;
        }

        internal bool TryCompleteLocalCommandForSubmission()
        {
            var suggestions = LocalCommandSuggestions;
            var command = GetSelectedLocalCommandSuggestion(suggestions);
            if (command == null)
                return true;

            InputText = command.CompletionText;
            return !command.RequiresMoreInputAfterCompletion;
        }

        private CopilotLocalCommand? GetSelectedLocalCommandSuggestion(
            IReadOnlyList<CopilotLocalCommand> suggestions)
        {
            var selectedIndex = CopilotSuggestionSelection.Normalize(
                SelectedLocalCommandSuggestionIndex,
                suggestions.Count);
            if (selectedIndex < 0)
                return null;

            SelectedLocalCommandSuggestionIndex = selectedIndex;
            return suggestions[selectedIndex];
        }

        private void RefreshLocalCommandSuggestions()
        {
            var suggestions = LocalCommandSuggestions;
            var selectedIndex = CopilotSuggestionSelection.Reset(suggestions.Count);

            OnPropertyChanged(nameof(LocalCommandSuggestions));
            SelectedLocalCommandSuggestionIndex = selectedIndex;
            OnPropertyChanged(nameof(HasLocalCommandSuggestions));
        }
    }
}

#pragma warning disable CA1822
using System;
using System.Linq;

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
                    conversation.Attachments,
                    _pendingWorkspaceReviewTarget,
                    _pendingAgentSkillReference);
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
            SetPendingWorkspaceReviewTarget(stash.WorkspaceReviewTarget);
            SetPendingAgentSkillReference(stash.AgentSkillReference);
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

    }
}

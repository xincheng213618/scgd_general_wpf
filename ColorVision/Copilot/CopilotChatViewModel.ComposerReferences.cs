#pragma warning disable CA1822
using ColorVision.Solution;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public partial class CopilotChatViewModel
    {
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

        public string ComposerReferenceHeader =>
            _currentCodexConfigOptions.ConfiguredMentionsV2Enabled
                ? "@ 关联模板、菜单或文件"
                : "@ 关联文件 · mentions_v2 已关闭";

        public string ComposerReferenceMenuHeader =>
            _currentCodexConfigOptions.ConfiguredMentionsV2Enabled
                ? "关联模板、菜单或文件（@）"
                : "关联文件（@ · mentions_v2 已关闭）";

        public string ComposerReferenceMenuToolTip =>
            _currentCodexConfigOptions.ConfiguredMentionsV2Enabled
                ? "在当前光标位置插入 @ 并打开统一关联候选。"
                : "在当前光标位置插入 @ 并打开旧版文件候选；features.mentions_v2=false 不列出模板或菜单。";

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
                _activeDocumentPath,
                _currentCodexConfigOptions.ConfiguredMentionsV2Enabled);
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

    }
}

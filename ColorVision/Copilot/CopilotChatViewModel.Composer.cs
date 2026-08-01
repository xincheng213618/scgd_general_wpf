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

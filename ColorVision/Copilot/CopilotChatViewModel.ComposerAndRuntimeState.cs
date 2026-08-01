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
    public partial class CopilotChatViewModel : ViewModelBase, IDisposable
    {
        public bool IsConversationEmpty => Messages.Count == 0;

        public string ConversationSearchText
        {
            get => _conversationSearchText;
            set
            {
                var normalizedValue = NormalizeConversationSearchText(value);
                if (!SetProperty(ref _conversationSearchText, normalizedValue))
                    return;

                OnPropertyChanged(nameof(IsConversationSearchEmpty));
                OnPropertyChanged(nameof(HasConversationSearchQuery));
                ScheduleConversationSearchRefresh();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsConversationSearchEmpty => string.IsNullOrWhiteSpace(ConversationSearchText);

        public bool HasConversationSearchQuery => !IsConversationSearchEmpty;

        public bool HasNoConversationSearchResults => HasConversationSearchQuery && FilteredConversations.Count == 0;

        public bool IsConversationFindOpen => _conversationFindSession.IsOpen;

        public string ConversationFindText
        {
            get => _conversationFindSession.Query;
            set
            {
                if (!_conversationFindSession.SetQuery(Messages, value))
                    return;

                OnPropertyChanged(nameof(ConversationFindText));
                OnPropertyChanged(nameof(HasConversationFindQuery));
                if (IsConversationFindOpen)
                    NotifyConversationFindStateChanged();
            }
        }

        public bool HasConversationFindQuery => _conversationFindSession.HasQuery;

        public bool HasConversationFindMatches => _conversationFindSession.HasMatches;

        public string ConversationFindStatusText => _conversationFindSession.StatusText;

        public CopilotChatMessage? CurrentConversationFindMatch => _conversationFindSession.Current;

        public bool HasAttachments => Attachments.Count > 0;

        public bool HasComposerAttachmentItems => HasAttachments || HasAvailableCurrentLiveContext;

        public bool HasActiveDocument => !string.IsNullOrWhiteSpace(_activeDocumentPath);

        public bool IsActiveDocumentAttached => HasActiveDocument && Attachments.Any(item =>
            (item.Type is CopilotAttachmentType.File or CopilotAttachmentType.Image)
            && string.Equals(item.Value, _activeDocumentPath, StringComparison.OrdinalIgnoreCase));

        public bool CanAttachActiveDocument => !IsBusy && HasActiveDocument && !IsActiveDocumentAttached;

        public string ActiveDocumentAttachmentMenuText
        {
            get
            {
                if (!HasActiveDocument)
                    return "添加当前文件（当前没有打开的文件）";

                var fileName = Path.GetFileName(_activeDocumentPath);
                return IsActiveDocumentAttached
                    ? $"当前文件已附加：{fileName}"
                    : $"添加当前文件：{fileName}";
            }
        }

        public string LocalCommandResultTitle
        {
            get => _localCommandResultTitle;
            private set => SetProperty(ref _localCommandResultTitle, value ?? string.Empty);
        }

        public string LocalCommandResultText
        {
            get => _localCommandResultText;
            private set
            {
                if (!SetProperty(ref _localCommandResultText, value ?? string.Empty))
                    return;

                OnPropertyChanged(nameof(HasLocalCommandResult));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasLocalCommandResult => !string.IsNullOrWhiteSpace(LocalCommandResultText);

        public bool HasCurrentLiveContext => _currentLiveContext != null;

        public bool HasAvailableCurrentLiveContext => _currentLiveContext?.SnapshotItems?.Count > 0
            && !IsCurrentLiveContextAttached;

        public bool CanAttachCurrentLiveContext => !IsBusy && HasAvailableCurrentLiveContext;

        public bool IsCurrentLiveContextAttached => _currentLiveContext != null
            && SelectedConversation?.Attachments.Any(item => item.Type == CopilotAttachmentType.Context
                && string.Equals(item.Source, _currentLiveContext.SourceId, StringComparison.Ordinal)) == true;

        public string CurrentLiveContextAttachmentLabel
        {
            get
            {
                var label = string.IsNullOrWhiteSpace(_currentLiveContext?.AttachmentTitle)
                    ? _currentLiveContext?.Title
                    : _currentLiveContext.AttachmentTitle;
                return string.IsNullOrWhiteSpace(label) ? "上下文" : label.Trim();
            }
        }

        public string EmptyStateText => _config.IsConfigured
            ? Properties.Resources.CopilotSelectHistoryOrNew
            : Properties.Resources.CopilotConfigureModelFirst;

        public string PrimaryActionGlyph
        {
            get
            {
                if (IsPromptHistorySearchOpen)
                    return "✓";
                if (HasExclusiveLocalOperation)
                    return "■";
                if (IsViewingActiveRun)
                {
                    return ActiveHostedRunInteraction.PrimaryAction switch
                    {
                        CopilotHostedRunPrimaryAction.Cancel => "×",
                        CopilotHostedRunPrimaryAction.None => "…",
                        _ => "■",
                    };
                }
                return IsViewingQueuedRun ? "×" : "↑";
            }
        }

        public string PrimaryActionToolTip
        {
            get
            {
                if (IsPromptHistorySearchOpen)
                    return HasPromptHistorySearchResults
                        ? "把选中的历史请求恢复到输入框"
                        : "请修改历史搜索关键词";
                if (_isCompactingConversation)
                    return "停止上下文压缩";
                if (_fileAttachmentCts != null)
                    return "停止处理附件";
                if (_webPageAttachmentCts != null)
                    return "停止读取网页附件";
                if (IsViewingQueuedRun)
                    return "取消这个排队任务";
                if (IsViewingActiveRun)
                {
                    return ActiveHostedRun?.State switch
                    {
                        CopilotHostedRunState.PauseRequested => "正在暂停当前 Agent 任务；再次点击将改为取消",
                        CopilotHostedRunState.CancelRequested => "正在取消当前任务",
                        _ => IsAgentRequestActive ? "停止当前 Agent 任务" : Properties.Resources.CopilotStopGeneration,
                    };
                }
                if (IsBusy)
                {
                    var admission = EvaluateComposerRequestAdmission(ResolveComposerRequestMode());
                    return GetRequestAdmissionText(admission);
                }

                var action = Properties.Resources.CopilotSend;
                var preview = BuildComposerRequestPreview();
                return string.IsNullOrWhiteSpace(preview)
                    ? action
                    : $"{action}{Environment.NewLine}{Environment.NewLine}{preview}";
            }
        }

        public CopilotConversationRecord? SelectedConversation
        {
            get => _selectedConversation;
            set => SelectConversation(value, persist: true);
        }

        public CopilotAgentAccessMode ComposerAccessMode =>
            SelectedConversation?.AccessMode ?? CopilotAgentAccessMode.ConfirmProtectedActions;

        public bool IsComposerFullAccess => ComposerAccessMode == CopilotAgentAccessMode.FullAccess;

        public bool IsComposerConfirmAccess => !IsComposerFullAccess;

        public string ComposerAccessModeLabel => !IsComposerFullAccess
            ? "按需确认"
            : SelectedConversation?.IsFullAccessPreparedForNextTask == true
                ? "自动复核 · 下一任务"
                : "自动复核 · 本任务";

        public string ComposerAccessModeToolTip => IsComposerFullAccess
            ? BuildFullAccessToolTip()
            : "受保护操作执行前逐次确认。可为下一任务临时授权；已有待审批操作始终需要单独决定。";

        internal bool TrySelectConversation(string? conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId) || !CanSwitchConversation)
                return false;

            var conversation = Conversations.FirstOrDefault(item =>
                string.Equals(item.Id, conversationId.Trim(), StringComparison.Ordinal));
            if (conversation == null)
                return false;

            SelectConversation(conversation, persist: true, preferredProfileId: conversation.ProfileId);
            return true;
        }

        public CopilotProfileConfig? SelectedProfile
        {
            get => _selectedProfile;
            set => SelectProfile(value, syncConversation: true, persist: true);
        }

        public string SelectedProfileToolTip
        {
            get
            {
                var profile = SelectedProfile;
                if (profile == null)
                    return "No model profile is selected.";

                var builder = new StringBuilder();
                builder.AppendLine(profile.DisplayLabel);
                builder.AppendLine(profile.SecondaryLabel);
                builder.AppendLine($"推理：{profile.ReasoningLabel}");

                if (!string.IsNullOrWhiteSpace(profile.BaseUrl))
                    builder.AppendLine(profile.BaseUrl.Trim());

                return builder.ToString().TrimEnd();
            }
        }

        public string ComposerTokenSummary
        {
            get => _composerTokenSummary;
            private set => SetProperty(ref _composerTokenSummary, value ?? string.Empty);
        }
        private string _composerTokenSummary = "Token usage appears after sending";

        public string ComposerTokenDetails
        {
            get => _composerTokenDetails;
            private set => SetProperty(ref _composerTokenDetails, value ?? string.Empty);
        }
        private string _composerTokenDetails = "Local estimates are disabled. This panel shows only token usage returned by the API.";

        public bool IsConversationContextReduced
        {
            get => _isConversationContextReduced;
            private set
            {
                if (SetProperty(ref _isConversationContextReduced, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }
        private bool _isConversationContextReduced;

        public string ConversationContextCompactionToolTip
        {
            get => _conversationContextCompactionToolTip;
            private set => SetProperty(ref _conversationContextCompactionToolTip, value ?? string.Empty);
        }
        private string _conversationContextCompactionToolTip = string.Empty;

        public string ConversationContextUsageLabel
        {
            get => _conversationContextUsageLabel;
            private set => SetProperty(ref _conversationContextUsageLabel, value ?? string.Empty);
        }
        private string _conversationContextUsageLabel = "历史 0%";

        public string ConversationContextUsageToolTip
        {
            get => _conversationContextUsageToolTip;
            private set => SetProperty(ref _conversationContextUsageToolTip, value ?? string.Empty);
        }
        private string _conversationContextUsageToolTip = string.Empty;

        public bool IsConversationContextUnderPressure
        {
            get => _isConversationContextUnderPressure;
            private set => SetProperty(ref _isConversationContextUnderPressure, value);
        }
        private bool _isConversationContextUnderPressure;

        public bool IsProviderRateLimitStatusVisible
        {
            get => _isProviderRateLimitStatusVisible;
            private set => SetProperty(ref _isProviderRateLimitStatusVisible, value);
        }
        private bool _isProviderRateLimitStatusVisible;

        public string ProviderRateLimitStatusLabel
        {
            get => _providerRateLimitStatusLabel;
            private set => SetProperty(ref _providerRateLimitStatusLabel, value ?? string.Empty);
        }
        private string _providerRateLimitStatusLabel = string.Empty;

        public string ProviderRateLimitStatusToolTip
        {
            get => _providerRateLimitStatusToolTip;
            private set => SetProperty(ref _providerRateLimitStatusToolTip, value ?? string.Empty);
        }
        private string _providerRateLimitStatusToolTip = string.Empty;

        public bool IsProviderRateLimitUnderPressure
        {
            get => _isProviderRateLimitUnderPressure;
            private set => SetProperty(ref _isProviderRateLimitUnderPressure, value);
        }
        private bool _isProviderRateLimitUnderPressure;

        public string InputText
        {
            get => _inputText;
            set
            {
                var normalizedValue = value ?? string.Empty;
                if (SetProperty(ref _inputText, normalizedValue))
                {
                    if (!_isApplyingPromptHistory)
                        _promptHistoryNavigator.Reset();
                    if (IsPromptHistorySearchOpen)
                        RefreshPromptHistorySearchResults();
                    else
                        UpdateSelectedConversationDraft(normalizedValue);
                    OnPropertyChanged(nameof(IsInputEmpty));
                    RefreshLocalCommandSuggestions();
                    OnPropertyChanged(nameof(CanSubmitUserQuestionAnswer));
                    OnPropertyChanged(nameof(CanSteerCurrentRun));
                    OnPropertyChanged(nameof(CanQueueCurrentRunFollowUp));
                    RefreshComposerReferenceSuggestions();
                    RefreshComposerTokenEstimate();
                    NotifyPromptHistoryPrefixCompletionChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public int ComposerMaximumCharacters => CopilotConversationHistoryWindow.MaximumContentCharacterLimit;

        public bool IsPromptHistorySearchOpen
        {
            get => _isPromptHistorySearchOpen;
            private set
            {
                if (!SetProperty(ref _isPromptHistorySearchOpen, value))
                    return;

                OnPropertyChanged(nameof(InputPlaceholder));
                OnPropertyChanged(nameof(PrimaryActionGlyph));
                OnPropertyChanged(nameof(PrimaryActionToolTip));
                OnPropertyChanged(nameof(HasPromptHistorySearchResults));
                OnPropertyChanged(nameof(PromptHistorySearchHeader));
                OnPropertyChanged(nameof(PromptHistorySearchStatusText));
                OnPropertyChanged(nameof(CanOpenExpandedComposerEditor));
                RefreshLocalCommandSuggestions();
                NotifyPromptHistoryPrefixCompletionChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasPromptHistorySearchResults =>
            IsPromptHistorySearchOpen && PromptHistorySearchResults.Count > 0;

        public string PromptHistorySearchHeader =>
            $"历史请求 · {PromptHistorySearchScopeLabel} · {PromptHistorySearchResults.Count:N0}";

        public string PromptHistorySearchStatusText => HasPromptHistorySearchResults
            ? "继续输入可筛选；Ctrl+S 切换范围，选中后只恢复到输入框，不会自动发送。"
            : "没有匹配的可见历史请求；Ctrl+S 可切换范围，或按 Esc 关闭。";

        public string PromptHistorySearchScopeLabel =>
            _promptHistorySearchScope == CopilotPromptHistorySearchScope.AllConversations
                ? "全部会话"
                : "当前会话";

        public CopilotPromptHistorySearchItem? SelectedPromptHistorySearchResult
        {
            get => _selectedPromptHistorySearchResult;
            set => SetProperty(ref _selectedPromptHistorySearchResult, value);
        }

        public bool IsNavigatingPromptHistory => _promptHistoryNavigator.IsActive;

        public bool HasPromptHistoryPrefixCompletion =>
            TryResolvePromptHistoryPrefixCompletion(out _);

        public string PromptHistoryPrefixCompletionText =>
            TryResolvePromptHistoryPrefixCompletion(out var completion)
                ? completion.FullText
                : string.Empty;


        private CopilotHostedAgentRun? ActiveHostedRun => _taskHost.ActiveRun;

        private CopilotHostedRunInteraction ActiveHostedRunInteraction =>
            CopilotHostedRunInteractionPolicy.Evaluate(ActiveHostedRun?.State ?? CopilotHostedRunState.Completed);

        private CopilotHostedAgentRun? SelectedHostedRun => _taskHost.FindRunByConversationId(SelectedConversation?.Id);

        private bool IsAgentRequestActive => ActiveHostedRun?.IsAgent == true;

        private bool IsViewingActiveRun => string.Equals(ActiveHostedRun?.ConversationId, SelectedConversation?.Id, StringComparison.Ordinal);

        private bool IsViewingQueuedRun => SelectedHostedRun?.State == CopilotHostedRunState.Queued;

        internal bool CanShowConversationRewindShortcut =>
            SelectedConversation != null
            && !IsBusy
            && !IsEditingMessage
            && CanSwitchConversation;

        private CopilotLocalCommandComposerContext ResolveLocalCommandComposerContext()
        {
            if (IsAnsweringUserQuestion)
                return CopilotLocalCommandComposerContext.AwaitingUserAnswer;
            if (IsViewingQueuedRun)
                return CopilotLocalCommandComposerContext.QueuedRun;
            return IsViewingActiveRun
                ? CopilotLocalCommandComposerContext.ActiveRun
                : CopilotLocalCommandComposerContext.Idle;
        }

        private CopilotChatMessage? ActiveUserQuestionMessage
        {
            get
            {
                var run = ActiveHostedRun;
                if (run?.IsAgent != true)
                    return null;
                var conversation = Conversations.FirstOrDefault(item =>
                    string.Equals(item.Id, run.ConversationId, StringComparison.Ordinal));
                return conversation?.Messages.LastOrDefault(message =>
                    !message.IsUser
                    && message.UserQuestion?.IsPending == true
                    && string.Equals(message.UserQuestion.TaskId, run.Id, StringComparison.Ordinal));
            }
        }

        private CopilotUserQuestionSnapshot? ActiveUserQuestion => ActiveUserQuestionMessage?.UserQuestion;

        public bool IsAnsweringUserQuestion => IsBusy
            && IsAgentRequestActive
            && IsViewingActiveRun
            && ActiveHostedRunInteraction.AcceptsNewInput
            && ActiveUserQuestion?.IsPending == true;

        public bool CanSubmitUserQuestionAnswer => IsAnsweringUserQuestion
            && CopilotUserQuestionSnapshot.TryNormalizeAnswer(InputText, out _);

        public bool CanSteerCurrentRun => IsBusy
            && IsAgentRequestActive
            && IsViewingActiveRun
            && ActiveHostedRunInteraction.AcceptsNewInput
            && !IsAnsweringUserQuestion
            && !IsInputEmpty;

        public bool CanQueueCurrentRunFollowUp => CanSteerCurrentRun
            && ActiveHostedRun is { } activeRun
            && SelectedConversation is { } conversation
            && _taskHost.EvaluateFollowUpAdmission(conversation.Id, activeRun.Mode).IsAllowed;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                if (value && IsPromptHistorySearchOpen)
                    DismissPromptHistorySearch();
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSwitchConversation));
                OnPropertyChanged(nameof(CanSelectProfile));
                OnPropertyChanged(nameof(PrimaryActionGlyph));
                OnPropertyChanged(nameof(PrimaryActionToolTip));
                OnPropertyChanged(nameof(AttachmentMenuToolTip));
                OnPropertyChanged(nameof(CanAttachCurrentLiveContext));
                OnPropertyChanged(nameof(IsAnsweringUserQuestion));
                OnPropertyChanged(nameof(CanSubmitUserQuestionAnswer));
                OnPropertyChanged(nameof(CanSteerCurrentRun));
                OnPropertyChanged(nameof(CanQueueCurrentRunFollowUp));
                OnPropertyChanged(nameof(InputPlaceholder));
                RefreshComposerTokenEstimate();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private bool _isBusy;

        public bool CanSwitchConversation => !IsBusy || IsAgentRequestActive;

        public bool CanSelectProfile => !IsBusy && Profiles.Count > 0;

        public bool IsMcpEnabled => _config.McpEnabled;

        public bool IsMcpRunning => _config.McpEnabled && CopilotMcpServer.Instance.IsRunning;

        public bool IsControlModeVisible => _config.McpEnabled || HasPendingMcpActions || HasRecentMcpFailures;

        public bool HasPendingMcpActions => _hasPendingMcpActions;

        public bool HasRecentMcpFailures => _hasRecentMcpFailures;

        public string McpStatusLabel
        {
            get
            {
                var pendingCount = CopilotMcpConfirmationStore.Instance.PendingCount;
                if (pendingCount > 0)
                    return pendingCount == 1 ? "等待确认" : $"等待确认 {pendingCount}";

                if (HasRecentMcpFailures)
                    return "控制异常";

                if (!_config.McpEnabled)
                    return string.Empty;

                return CopilotMcpServer.Instance.IsRunning ? "控制运行中" : "控制停止";
            }
        }

        public string McpStatusToolTip
        {
            get => BuildMcpDiagnosticsReport(verbose: false);
        }

    }
}


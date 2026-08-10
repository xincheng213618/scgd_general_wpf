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
        public ObservableCollection<CopilotConversationRecord> Conversations => _conversationSession.Conversations;

        public event EventHandler? ConversationSearchRequested;

        public event EventHandler? ProfileSelectionRequested;

        public event EventHandler? ReasoningSelectionRequested;

        public event EventHandler? AccessModeSelectionRequested;

        internal event EventHandler<CopilotChatMessageNavigationRequestedEventArgs>? MessageNavigationRequested;

        public ObservableCollection<CopilotConversationRecord> CompactHistoryConversations { get; } = new();

        public ObservableCollection<CopilotConversationRecord> FilteredConversations { get; } = new();

        public IReadOnlyList<CopilotConversationBranchFamilyMember> ConversationBranchFamily { get; private set; } =
            Array.Empty<CopilotConversationBranchFamilyMember>();

        public bool HasConversationBranchFamily => ConversationBranchFamily.Count > 1;

        public string ConversationBranchFamilyLabel =>
            $"会话树 · {ConversationBranchFamily.Count.ToString(System.Globalization.CultureInfo.CurrentCulture)}";

        public ObservableCollection<CopilotAgentTaskSummary> AgentTasks { get; } = new();

        public bool HasAgentTasks => AgentTasks.Count > 0;

        public string AgentTaskCountLabel => AgentTasks.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);

        public bool IsAgentTaskPanelExpanded => _state.IsAgentTaskPanelExpanded;

        public bool IsAgentTaskListVisible => HasAgentTasks && IsAgentTaskPanelExpanded;

        public string AgentTaskPanelToggleGlyph => IsAgentTaskPanelExpanded ? "▾" : "▸";

        public string AgentTaskPanelToolTip =>
            $"{(IsAgentTaskPanelExpanded ? "收起" : "展开")} Agent 任务（Ctrl+T）";

        public bool ShowMessageTimestamps => _state.ShowMessageTimestamps;

        public bool PromptHistoryCompletionsEnabled => _state.EnablePromptHistoryCompletions;

        public bool UseCompactMessageLayout => _state.UseCompactMessageLayout;

        public bool UseMultilineComposer => _state.UseMultilineComposer;

        public CopilotFollowUpBehavior DefaultFollowUpBehavior =>
            CopilotFollowUpPreference.Normalize(_state.DefaultFollowUpBehavior);

        public string SteerActionToolTip =>
            $"把输入作为新指令加入当前 Agent 运行（{ResolveFollowUpShortcut(CopilotFollowUpBehavior.Steer)}）";

        public string QueueFollowUpToolTip =>
            $"排到当前 Agent 任务结束后再执行（{ResolveFollowUpShortcut(CopilotFollowUpBehavior.Queue)}）";

        public string FollowUpQueueHintText =>
            $"{ComposerSubmitShortcutLabel} {DefaultFollowUpActionLabel} · Tab {AlternateFollowUpActionLabel} · Ctrl+Enter 立即接管";

        public string ComposerInputToolTip => UseMultilineComposer
            ? "多行模式：Enter 换行，Shift+Enter 发送；Agent 运行中 Ctrl+Enter 取消当前轮并立即执行输入；↑/↓ 浏览请求历史；补全列表中可用 → 接受；Ctrl+R 搜索历史；Ctrl+S 暂存或恢复草稿；Ctrl+E 展开编辑"
            : "标准模式：Enter 发送，Shift+Enter 换行；Agent 运行中 Ctrl+Enter 取消当前轮并立即执行输入；↑/↓ 浏览请求历史；补全列表中可用 → 接受；Ctrl+R 搜索历史；Ctrl+S 暂存或恢复草稿；Ctrl+E 展开编辑";

        public Thickness MessageListPadding =>
            CopilotCompactMessageLayout.Resolve(UseCompactMessageLayout).MessageListPadding;

        public Thickness MessageItemMargin =>
            CopilotCompactMessageLayout.Resolve(UseCompactMessageLayout).MessageItemMargin;

        public Thickness UserMessagePadding =>
            CopilotCompactMessageLayout.Resolve(UseCompactMessageLayout).UserMessagePadding;

        public Thickness AssistantActionsMargin =>
            CopilotCompactMessageLayout.Resolve(UseCompactMessageLayout).AssistantActionsMargin;

        public ObservableCollection<CopilotQueuedFollowUp> QueuedFollowUps => _followUpQueue.Items;

        public bool HasQueuedFollowUps => QueuedFollowUps.Count > 0;

        public string QueuedFollowUpCountLabel => QueuedFollowUps.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);

        public string AgentRunNoticeText
        {
            get => _agentRunNoticeText;
            private set
            {
                if (SetProperty(ref _agentRunNoticeText, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasAgentRunNotice));
            }
        }

        public bool HasAgentRunNotice => !string.IsNullOrWhiteSpace(AgentRunNoticeText);

        public string CompletionNoticeText
        {
            get => _completionNoticeText;
            private set
            {
                if (SetProperty(ref _completionNoticeText, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasCompletionNotice));
            }
        }

        public bool HasCompletionNotice => !string.IsNullOrWhiteSpace(CompletionNoticeText);

        public string StateRecoveryNoticeText { get; private set; } = string.Empty;

        public string StateRecoveryNoticeToolTip { get; private set; } = string.Empty;

        public bool HasStateRecoveryNotice => !string.IsNullOrWhiteSpace(StateRecoveryNoticeText);

        public string StatePersistenceNoticeText
        {
            get => _statePersistenceNoticeText;
            private set
            {
                if (SetProperty(ref _statePersistenceNoticeText, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasStatePersistenceNotice));
            }
        }

        public string StatePersistenceNoticeToolTip
        {
            get => _statePersistenceNoticeToolTip;
            private set => SetProperty(ref _statePersistenceNoticeToolTip, value ?? string.Empty);
        }

        public bool HasStatePersistenceNotice => !string.IsNullOrWhiteSpace(StatePersistenceNoticeText);

        public bool HasCompactHistoryConversations => CompactHistoryConversations.Count > 0;

        public bool CanShowCompactHistory => _config.IsConfigured && HasCompactHistoryConversations;

        public bool HasCompactHistoryOverflow => CountHistoryConversations() > CompactHistoryLimit;

        public string CompactHistoryFooterText
        {
            get
            {
                var count = CountHistoryConversations();
                return count > CompactHistoryLimit ? count.ToString(System.Globalization.CultureInfo.CurrentCulture) : string.Empty;
            }
        }

        public ObservableCollection<CopilotProfileConfig> Profiles => _config.Profiles;

        public ObservableCollection<CopilotChatMessage> Messages => SelectedConversation?.Messages ?? _emptyMessages;

        public ObservableCollection<CopilotAttachmentItem> Attachments => SelectedConversation?.Attachments ?? _emptyAttachments;

        public bool HasComposerStash => SelectedConversation?.HasComposerStash == true;

        public string ComposerStashToolTip
        {
            get
            {
                var stash = SelectedConversation?.ComposerStash;
                if (stash?.HasContent != true)
                    return "按 Ctrl+S 暂存当前输入、附件和请求模式";

                return $"恢复暂存草稿（Ctrl+S）"
                    + Environment.NewLine
                    + $"{stash.Text.Length:N0} 个字符 · {stash.Attachments.Count:N0} 个附件 · {FormatComposerRequestMode(stash.RequestMode)}模式";
            }
        }

        public ObservableCollection<CopilotComposerReferenceItem> ComposerReferenceSuggestions => _composerReferenceSuggestions;

        public ObservableCollection<CopilotPromptHistorySearchItem> PromptHistorySearchResults => _promptHistorySearchResults;

        public ObservableCollection<ConfirmableAction> PendingActions => _pendingActions;

        public bool HasPendingActions => _pendingActions.Count > 0;

        public bool HasPendingActionFeedback => !string.IsNullOrWhiteSpace(PendingActionFeedbackText);

        public bool HasPendingActionPanel => HasPendingActions || HasPendingActionFeedback;

        public string PendingActionPanelTitle
        {
            get
            {
                var count = _pendingActions.Count;
                if (count == 0)
                    return "受保护操作";

                return count == 1
                    ? "等待确认的受保护操作"
                    : $"{count} 个受保护操作等待确认";
            }
        }

        public string PendingActionPanelSummary
        {
            get
            {
                if (_pendingActions.Count == 0)
                    return "当前没有等待确认的受保护操作。";

                var nextDeadline = _pendingActions
                    .OrderBy(action => action.ExpiresAt)
                    .FirstOrDefault()?.ReviewDeadlineLabel ?? string.Empty;

                var actionBehavior = _pendingActions.Any(action => action.ResumesAgentOnApproval)
                    ? "批准后，Agent 将在同一任务中继续执行。"
                    : _pendingActions.Any(action => action.ExecuteOnApproval)
                        ? "批准后将立即在应用内执行；是否保存仍由你决定。"
                        : "外部 MCP 操作批准后，调用方仍需提交 confirm_action。";
                return string.IsNullOrWhiteSpace(nextDeadline) ? actionBehavior : $"{actionBehavior} 最近一项{nextDeadline}。";
            }
        }

        public string PendingActionPanelToolTip
        {
            get
            {
                if (_pendingActions.Count == 0)
                    return PendingActionPanelSummary;

                return string.Join(Environment.NewLine, _pendingActions.Select(action =>
                    $"{action.Title}｜来源：{action.RequesterLabel}｜任务：{action.TaskScopeLabel}｜风险：{action.RiskDisplayLabel}｜{action.ReviewDeadlineLabel}"));
            }
        }

        public string PendingActionFeedbackText
        {
            get => _pendingActionFeedbackText;
            private set
            {
                if (SetProperty(ref _pendingActionFeedbackText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasPendingActionFeedback));
                    OnPropertyChanged(nameof(HasPendingActionPanel));
                    OnPropertyChanged(nameof(PendingActionPanelTitle));
                    OnPropertyChanged(nameof(PendingActionPanelSummary));
                    OnPropertyChanged(nameof(PendingActionPanelToolTip));
                }
            }
        }

        public ICommand SendCommand { get; }

        public ICommand NewChatCommand { get; }

        public ICommand CompactConversationCommand { get; }

        public ICommand ShowContextDiagnosticsCommand { get; }

        public ICommand ShowUsageDiagnosticsCommand { get; }

        public ICommand ClearConversationSearchCommand { get; }

        public ICommand OpenConversationFindCommand { get; }

        public ICommand CloseConversationFindCommand { get; }

        public ICommand FindPreviousConversationMatchCommand { get; }

        public ICommand FindNextConversationMatchCommand { get; }

        public ICommand SelectConversationCommand { get; }

        public ICommand PrimaryActionCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand AddFileAttachmentCommand { get; }

        public ICommand AttachActiveDocumentCommand { get; }

        public ICommand AddContextAttachmentCommand { get; }

        public ICommand AddWebPageAttachmentCommand { get; }

        public ICommand PasteImageAttachmentCommand { get; }

        public ICommand AttachCurrentLiveContextCommand { get; }

        public string AttachmentMenuToolTip => IsBusy
            ? "响应期间无法更改附件"
            : "添加附件";

        public ICommand CopyMessageCommand { get; }

        public ICommand CopyLatestResponseCommand { get; }

        public ICommand BranchConversationCommand { get; }

        public ICommand OpenBranchOriginCommand { get; }

        public ICommand EditMessageCommand { get; }

        public ICommand CancelMessageEditCommand { get; }

        public ICommand RetryMessageCommand { get; }

        public ICommand RefreshMessageCommand { get; }

        public ICommand ContinueAgentTasksCommand { get; }

        public ICommand ExecuteApprovedPlanCommand { get; }

        public ICommand ContinuePlanningCommand { get; }

        public ICommand RequestWorkspaceRollbackCommand { get; }

        public ICommand OpenWorkspaceChangeFileCommand { get; }

        public ICommand OpenAgentTaskCommand { get; }

        public ICommand ResumeAgentTaskCommand { get; }

        public ICommand DismissAgentTaskCommand { get; }

        public ICommand ToggleAgentTaskPanelCommand { get; }

        public ICommand OpenAgentRunNoticeCommand { get; }

        public ICommand OpenCompletionNoticeCommand { get; }

        public ICommand SteerCommand { get; }

        public ICommand SubmitUserQuestionAnswerCommand { get; }

        public ICommand AnswerUserQuestionOptionCommand { get; }

        public ICommand QueueFollowUpCommand { get; }

        public ICommand SendQueuedFollowUpNowCommand { get; }

        public ICommand EditQueuedFollowUpCommand { get; }

        public ICommand MoveQueuedFollowUpUpCommand { get; }

        public ICommand MoveQueuedFollowUpDownCommand { get; }

        public ICommand DeleteQueuedFollowUpCommand { get; }

        public ICommand OpenAttachmentCommand { get; }

        public ICommand RemoveAttachmentCommand { get; }

        public ICommand RenameConversationCommand { get; }

        public ICommand ExportConversationCommand { get; }

        public ICommand RetryStatePersistenceCommand { get; }

        public ICommand DeleteConversationCommand { get; }

        public ICommand TogglePinConversationCommand { get; }

        public ICommand CopyPendingActionIdCommand { get; }

        public ICommand CopyPendingActionPayloadCommand { get; }

        public ICommand ApprovePendingActionCommand { get; }

        public ICommand RejectPendingActionCommand { get; }

        public ICommand DismissLocalCommandResultCommand { get; }

        public ICommand CompleteLocalCommandCommand { get; }

        public ICommand SelectComposerReferenceCommand { get; }

        public ICommand SelectPromptHistorySearchResultCommand { get; }

        public ICommand AcceptPromptHistoryPrefixCompletionCommand { get; }

        public ICommand SetComposerAccessModeCommand { get; }

    }
}

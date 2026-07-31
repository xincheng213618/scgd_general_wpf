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
        private const int CompactHistoryLimit = 4;
        private const int CompactSummaryOutputTokens = 4096;
        private const int MaximumConversationSearchCharacters = 256;
        private const int MaximumConversationSearchTerms = 8;
        private static readonly TimeSpan ConversationSearchDebounceDelay = TimeSpan.FromMilliseconds(180);
        private static readonly TimeSpan RecentMcpFailureWindow = TimeSpan.FromMinutes(15);
        private readonly CopilotChatService _chatService;
        private readonly CopilotConversationTitleCoordinator _conversationTitleCoordinator;
        private readonly ICopilotGoalCompletionEvaluator _goalCompletionEvaluator;
        private readonly ICopilotTurnRuntime _turnRuntime;
        private readonly CopilotAgentTaskHost _taskHost;
        private readonly CopilotLocalGitDiffService _localGitDiffService;
        private readonly CopilotPromptHistoryNavigator _promptHistoryNavigator = new();
        private readonly CopilotConversationFindSession _conversationFindSession = new();
        private readonly CopilotConfig _config;
        private readonly ICopilotChatStateStore _stateStore;
        private readonly CopilotChatStatePersistenceCoordinator _statePersistenceCoordinator;
        private readonly ObservableCollection<CopilotChatMessage> _emptyMessages = new();
        private readonly ObservableCollection<CopilotAttachmentItem> _emptyAttachments = new();
        private readonly ObservableCollection<ConfirmableAction> _pendingActions = new();
        private readonly ObservableCollection<CopilotComposerReferenceItem> _composerReferenceSuggestions = new();
        private readonly ObservableCollection<CopilotPromptHistorySearchItem> _promptHistorySearchResults = new();
        private readonly Dictionary<string, CopilotQueuedFollowUp> _queuedFollowUpsByRunId = new(StringComparer.Ordinal);
        private readonly CopilotCompletionNoticeCenter _completionNoticeCenter = new();
        private readonly HashSet<CopilotNonBlockingCancellationSource> _auxiliaryOperationCancellations = new();
        private readonly DispatcherTimer _conversationSearchDebounceTimer;
        private readonly DispatcherTimer _pendingActionExpiryTimer;
        private CopilotNonBlockingCancellationSource? _pendingActionFeedbackCts;
        private CopilotNonBlockingCancellationSource? _compactConversationCts;
        private CopilotNonBlockingCancellationSource? _fileAttachmentCts;
        private CopilotNonBlockingCancellationSource? _webPageAttachmentCts;
        private CopilotNonBlockingCancellationSource? _composerReferenceRefreshCts;
        private CopilotLiveContext? _currentLiveContext;
        private CopilotChatState _state = new();
        private CopilotConversationRecord? _selectedConversation;
        private CopilotProfileConfig? _selectedProfile;
        private CopilotAgentMode? _pendingRequestModeOverride;
        private CopilotAgentRecoveryRequest? _pendingAgentRecoveryRequest;
        private string _activeDocumentPath = string.Empty;
        private string _pendingActionFeedbackText = string.Empty;
        private string _agentRunNoticeConversationId = string.Empty;
        private string _agentRunNoticeText = string.Empty;
        private string _completedAgentRunNoticeConversationId = string.Empty;
        private string _completedAgentRunNoticeText = string.Empty;
        private CopilotCompletionNotice? _completionNotice;
        private string _completionNoticeText = string.Empty;
        private string _statePersistenceNoticeText = string.Empty;
        private string _statePersistenceNoticeToolTip = string.Empty;
        private string _localCommandResultTitle = string.Empty;
        private string _localCommandResultText = string.Empty;
        private string _editingConversationId = string.Empty;
        private string _editingUserMessageId = string.Empty;
        private CopilotComposerDraftSnapshot? _composerDraftBeforeMessageEdit;
        private CopilotComposerReferenceItem? _selectedComposerReference;
        private string _conversationSearchText = string.Empty;
        private string _composerReferenceSessionKey = string.Empty;
        private string _promptHistorySearchConversationId = string.Empty;
        private string _promptHistorySearchDraft = string.Empty;
        private CopilotPromptHistorySearchScope _promptHistorySearchScope;
        private bool _isComposerReferenceMentionActive;
        private bool _isComposerReferenceSearchPending;
        private bool _isPromptHistorySearchOpen;
        private bool _hasPendingMcpActions;
        private bool _hasRecentMcpFailures;
        private bool _isApplyingPromptHistory;
        private bool _isExportingConversation;
        private bool _isInspectingGitDiff;
        private bool _isCompactingConversation;
        private bool _isRetryingStatePersistence;
        private long _composerReferenceRefreshVersion;
        private int _selectedLocalCommandSuggestionIndex = -1;
        private CopilotPromptHistorySearchItem? _selectedPromptHistorySearchResult;
        private int _disposeState;

        public CopilotChatViewModel()
            : this(new CopilotChatService())
        {
        }

        public CopilotChatViewModel(CopilotChatService chatService)
            : this(chatService, CopilotChatStateStore.Instance)
        {
        }

        public CopilotChatViewModel(CopilotChatService chatService, ICopilotChatStateStore stateStore)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _conversationTitleCoordinator = new CopilotConversationTitleCoordinator(
                new CopilotConversationTitleGenerator(_chatService),
                ApplyGeneratedConversationTitleAsync);
            _goalCompletionEvaluator = new CopilotGoalCompletionEvaluator(_chatService);
            _turnRuntime = new CopilotTurnRuntime(_chatService);
            _taskHost = CopilotAgentTaskHost.Shared;
            _localGitDiffService = new CopilotLocalGitDiffService();
            _config = CopilotConfig.Instance;
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _statePersistenceCoordinator = new CopilotChatStatePersistenceCoordinator(
                _stateStore,
                () => _state,
                () => Application.Current?.Dispatcher,
                ReportStatePersistenceError,
                ReportStatePersistenceSuccess);
            _conversationSearchDebounceTimer = new DispatcherTimer
            {
                Interval = ConversationSearchDebounceDelay,
            };
            _conversationSearchDebounceTimer.Tick += ConversationSearchDebounceTimer_Tick;
            _currentLiveContext = CopilotLiveContextRegistry.Current;
            _activeDocumentPath = TryGetActiveDocumentPath();

            if (Application.Current != null)
            {
                Application.Current.Exit -= Application_Exit;
                Application.Current.Exit += Application_Exit;
            }

            WorkspaceManager.ContentIdSelected -= WorkspaceManager_ContentIdSelected;
            WorkspaceManager.ContentIdSelected += WorkspaceManager_ContentIdSelected;
            CopilotLiveContextRegistry.CurrentChanged -= CopilotLiveContextRegistry_CurrentChanged;
            CopilotLiveContextRegistry.CurrentChanged += CopilotLiveContextRegistry_CurrentChanged;
            CopilotMcpConfirmationStore.Instance.ActionsChanged -= ConfirmationStore_ActionsChanged;
            CopilotMcpConfirmationStore.Instance.ActionsChanged += ConfirmationStore_ActionsChanged;
            CopilotMcpConfirmationStore.Instance.ActionStatusChanged -= ConfirmationStore_ActionStatusChanged;
            CopilotMcpConfirmationStore.Instance.ActionStatusChanged += ConfirmationStore_ActionStatusChanged;
            WeakEventManager<CopilotAgentTaskHost, CopilotAgentTaskHostChangedEventArgs>.RemoveHandler(_taskHost, nameof(CopilotAgentTaskHost.Changed), TaskHost_Changed);
            WeakEventManager<CopilotAgentTaskHost, CopilotAgentTaskHostChangedEventArgs>.AddHandler(_taskHost, nameof(CopilotAgentTaskHost.Changed), TaskHost_Changed);

            if (_config.EnsureInitialized())
                PersistConfig();

            _state = _stateStore.Load();
            var stateChanged = _state.EnsureInitialized(_config);
            stateChanged |= CopilotSteeringRecovery.RestorePendingToDrafts(_state);
            stateChanged |= CopilotConversationGoalRecovery.PauseActiveGoalsAfterProcessRestart(
                _state,
                DateTimeOffset.UtcNow);
            _stateStore.CleanupOrphanedAttachments(_state);
            InitializeStateRecoveryNotice();
            if (stateChanged)
                PersistState();

            Conversations.CollectionChanged += Conversations_CollectionChanged;

            var initialConversation = Conversations.Count > 0
                ? Conversations[0]
                : CopilotConversationRecord.CreateEmpty(_state.ActiveProfileId, string.Empty);

            if (Conversations.Count == 0)
                Conversations.Add(initialConversation);

            SelectConversation(Conversations.FirstOrDefault(conversation => conversation.Id == _state.ActiveConversationId) ?? initialConversation, persist: false);

            SendCommand = new RelayCommand(_ => ExecuteSendOrSteer());
            NewChatCommand = new RelayCommand(_ => StartNewChat(), _ => CanSwitchConversation);
            CompactConversationCommand = new RelayCommand(
                _ => CompactConversationFromUi(),
                _ => IsConversationContextReduced && !IsBusy && !_isCompactingConversation && SelectedConversation != null);
            ShowContextDiagnosticsCommand = new RelayCommand(
                _ => ShowContextDiagnosticsFromUi(),
                _ => SelectedConversation != null);
            ShowUsageDiagnosticsCommand = new RelayCommand(_ => ShowUsageDiagnosticsFromUi());
            ClearConversationSearchCommand = new RelayCommand(_ => ConversationSearchText = string.Empty, _ => HasConversationSearchQuery);
            OpenConversationFindCommand = new RelayCommand(_ => OpenConversationFind(), _ => SelectedConversation != null);
            CloseConversationFindCommand = new RelayCommand(_ => CloseConversationFind(), _ => IsConversationFindOpen);
            FindPreviousConversationMatchCommand = new RelayCommand(_ => MoveConversationFind(previous: true), _ => HasConversationFindMatches);
            FindNextConversationMatchCommand = new RelayCommand(_ => MoveConversationFind(previous: false), _ => HasConversationFindMatches);
            SelectConversationCommand = new RelayCommand<CopilotConversationRecord>(
                conversation => SelectConversation(conversation, persist: true),
                conversation => CanSwitchConversation && conversation != null);
            PrimaryActionCommand = new RelayCommand(_ => ExecutePrimaryAction());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings(), _ => !IsBusy);
            AddFileAttachmentCommand = new RelayCommand(_ => RunUiOperation(AddFileAttachmentAsync, "附加文件"), _ => !IsBusy);
            AttachActiveDocumentCommand = new RelayCommand(_ => AttachActiveDocument(), _ => CanAttachActiveDocument);
            AddContextAttachmentCommand = new RelayCommand(_ => AddContextAttachment(), _ => !IsBusy);
            AddWebPageAttachmentCommand = new RelayCommand(_ => RunUiOperation(AddWebPageAttachmentAsync, "附加网页"), _ => !IsBusy);
            PasteImageAttachmentCommand = new RelayCommand(_ => PasteImageAttachment(), _ => !IsBusy);
            AttachCurrentLiveContextCommand = new RelayCommand(_ => AttachCurrentLiveContext(), _ => CanAttachCurrentLiveContext);
            CopyMessageCommand = new RelayCommand<CopilotChatMessage>(CopyMessage, message => !string.IsNullOrWhiteSpace(message?.Content));
            CopyLatestResponseCommand = new RelayCommand(
                _ => CopyAssistantResponse(CopilotLocalCommandCatalog.FindExact("/copy")!, string.Empty),
                _ => Volatile.Read(ref _disposeState) == 0 && SelectedConversation != null);
            BranchConversationCommand = new RelayCommand<CopilotChatMessage>(BranchConversation, CanBranchConversation);
            OpenBranchOriginCommand = new RelayCommand<CopilotConversationRecord>(OpenBranchOrigin, CanOpenBranchOrigin);
            EditMessageCommand = new RelayCommand<CopilotChatMessage>(BeginEditMessage, CanEditMessage);
            CancelMessageEditCommand = new RelayCommand(_ => CancelMessageEdit(), _ => IsEditingMessage);
            RetryMessageCommand = new RelayCommand<CopilotChatMessage>(message => RunUiOperation(() => RetryMessageAsync(message, refreshExternalContext: false), "重新生成回复"), CanRegenerateMessage);
            RefreshMessageCommand = new RelayCommand<CopilotChatMessage>(message => RunUiOperation(() => RetryMessageAsync(message, refreshExternalContext: true), "刷新附件与网页后重新生成"), CanRegenerateMessage);
            ContinueAgentTasksCommand = new RelayCommand<CopilotChatMessage>(ContinueAgentTasks, CanContinueAgentTasks);
            ExecuteApprovedPlanCommand = new RelayCommand<CopilotChatMessage>(ExecuteApprovedPlan, CanExecuteApprovedPlan);
            ContinuePlanningCommand = new RelayCommand<CopilotChatMessage>(ContinuePlanning, CanContinuePlanning);
            RequestWorkspaceRollbackCommand = new RelayCommand<CopilotAgentTraceEntry>(RequestWorkspaceRollback, CanRequestWorkspaceRollback);
            OpenWorkspaceChangeFileCommand = new RelayCommand<CopilotWorkspaceChangeFile>(OpenWorkspaceChangeFile, CanOpenWorkspaceChangeFile);
            OpenAgentTaskCommand = new RelayCommand<CopilotAgentTaskSummary>(OpenAgentTask, task => task != null && CanSwitchConversation);
            ResumeAgentTaskCommand = new RelayCommand<CopilotAgentTaskSummary>(ResumeAgentTask, CanResumeAgentTask);
            DismissAgentTaskCommand = new RelayCommand<CopilotAgentTaskSummary>(DismissAgentTask, task => task != null && !IsBusy);
            ToggleAgentTaskPanelCommand = new RelayCommand(_ => ToggleAgentTaskPanel(), _ => HasAgentTasks);
            OpenAgentRunNoticeCommand = new RelayCommand(_ => OpenAgentRunNotice(), _ => HasAgentRunNotice);
            OpenCompletionNoticeCommand = new RelayCommand(
                _ => OpenCompletionNotice(),
                _ => CanOpenCompletionNotice());
            SteerCommand = new RelayCommand(_ => TrySteerCurrentRun(), _ => CanSteerCurrentRun);
            SubmitUserQuestionAnswerCommand = new RelayCommand(
                _ => TryAnswerCurrentUserQuestion(InputText),
                _ => CanSubmitUserQuestionAnswer);
            AnswerUserQuestionOptionCommand = new RelayCommand<CopilotUserQuestionOption>(
                AnswerUserQuestionOption,
                CanAnswerUserQuestionOption);
            QueueFollowUpCommand = new RelayCommand(_ => TryQueueCurrentRunFollowUp(), _ => CanQueueCurrentRunFollowUp);
            SendQueuedFollowUpNowCommand = new RelayCommand<CopilotQueuedFollowUp>(
                SendQueuedFollowUpNow,
                CanSendQueuedFollowUpNow);
            EditQueuedFollowUpCommand = new RelayCommand<CopilotQueuedFollowUp>(EditQueuedFollowUp, CanEditQueuedFollowUp);
            MoveQueuedFollowUpUpCommand = new RelayCommand<CopilotQueuedFollowUp>(
                item => MoveQueuedFollowUp(item, -1),
                item => item?.CanMoveUp == true);
            MoveQueuedFollowUpDownCommand = new RelayCommand<CopilotQueuedFollowUp>(
                item => MoveQueuedFollowUp(item, 1),
                item => item?.CanMoveDown == true);
            DeleteQueuedFollowUpCommand = new RelayCommand<CopilotQueuedFollowUp>(DeleteQueuedFollowUp, item => item != null);
            OpenAttachmentCommand = new RelayCommand<CopilotAttachmentItem>(OpenAttachment, attachment => attachment != null);
            RemoveAttachmentCommand = new RelayCommand<CopilotAttachmentItem>(RemoveAttachment, attachment => !IsBusy && attachment != null);
            RenameConversationCommand = new RelayCommand<CopilotConversationRecord>(RenameConversation, CanRenameConversation);
            ExportConversationCommand = new RelayCommand<CopilotConversationRecord>(
                conversation => RunUiOperation(() => ExportConversationAsync(conversation), "导出会话"),
                CanExportConversation);
            RetryStatePersistenceCommand = new RelayCommand(_ => RunUiOperation(RetryStatePersistenceAsync, "重试保存会话"), _ => CanRetryStatePersistence());
            DeleteConversationCommand = new RelayCommand<CopilotConversationRecord>(DeleteConversation, CanDeleteConversation);
            TogglePinConversationCommand = new RelayCommand<CopilotConversationRecord>(TogglePinConversation, conversation => !IsBusy && conversation != null);
            CopyPendingActionIdCommand = new RelayCommand<ConfirmableAction>(CopyPendingActionId, action => action != null);
            CopyPendingActionPayloadCommand = new RelayCommand<ConfirmableAction>(CopyPendingActionPayload, action => action != null);
            ApprovePendingActionCommand = new RelayCommand<ConfirmableAction>(action => RunUiOperation(
                () => ApprovePendingActionAsync(action),
                "执行已批准操作",
                message => SetPendingActionFeedback("执行失败：" + message)), CanReviewPendingAction);
            RejectPendingActionCommand = new RelayCommand<ConfirmableAction>(RejectPendingAction, CanReviewPendingAction);
            DismissLocalCommandResultCommand = new RelayCommand(_ => DismissLocalCommandResult(), _ => HasLocalCommandResult);
            CompleteLocalCommandCommand = new RelayCommand(command => TryCompleteLocalCommand(command as CopilotLocalCommand), _ => HasLocalCommandSuggestions);
            SelectComposerReferenceCommand = new RelayCommand<CopilotComposerReferenceItem>(
                reference => TryCompleteComposerReference(reference),
                reference => reference != null);
            SelectPromptHistorySearchResultCommand = new RelayCommand<CopilotPromptHistorySearchItem>(
                result => TryCompletePromptHistorySearch(result),
                result => IsPromptHistorySearchOpen && result != null);
            AcceptPromptHistoryPrefixCompletionCommand = new RelayCommand(
                _ => TryAcceptPromptHistoryPrefixCompletion(),
                _ => HasPromptHistoryPrefixCompletion);
            SetComposerAccessModeCommand = new RelayCommand(
                mode =>
                {
                    if (mode is CopilotAgentAccessMode accessMode)
                        SetComposerAccessMode(accessMode);
                },
                _ => SelectedConversation != null);

            _pendingActionExpiryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5),
            };
            _pendingActionExpiryTimer.Tick += (_, _) => RefreshTimedAccessAndPendingActions();
            _pendingActionExpiryTimer.Start();

            RefreshPendingActions();
            RefreshComposerTokenEstimate();
            RefreshCompactHistoryConversations();
            RefreshFilteredConversations();
            RefreshAgentTasks();
            IsBusy = _taskHost.IsActive;
            NotifyHostedRunStateChanged();
            CopilotBackgroundShellCommandRegistry.Shared.CommandCompleted -= BackgroundShellCommandRegistry_CommandCompleted;
            CopilotBackgroundShellCommandRegistry.Shared.CommandCompleted += BackgroundShellCommandRegistry_CommandCompleted;
            CopilotBackgroundShellCommandRegistry.Shared.OutputMonitorEvent -= BackgroundShellCommandRegistry_OutputMonitorEvent;
            CopilotBackgroundShellCommandRegistry.Shared.OutputMonitorEvent += BackgroundShellCommandRegistry_OutputMonitorEvent;
        }

        public ObservableCollection<CopilotConversationRecord> Conversations => _state.Conversations;

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

        public ObservableCollection<CopilotQueuedFollowUp> QueuedFollowUps { get; } = new();

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


        private CopilotAgentMode ResolveComposerRequestMode()
        {
            return _pendingRequestModeOverride ?? CopilotAgentMode.Auto;
        }

        private static string FormatComposerRequestMode(CopilotAgentMode mode) => mode switch
        {
            CopilotAgentMode.Chat => "聊天",
            CopilotAgentMode.Auto => "自动",
            CopilotAgentMode.Explain => "解释",
            CopilotAgentMode.Web => "网页",
            CopilotAgentMode.Code => "代码",
            CopilotAgentMode.Review => "审查",
            CopilotAgentMode.Diagnose => "诊断",
            CopilotAgentMode.Plan => "计划",
            _ => "自动",
        };

        private bool CanScheduleComposerRequest(CopilotAgentMode mode)
        {
            return Volatile.Read(ref _disposeState) == 0
                && !HasExclusiveLocalOperation
                && EvaluateComposerRequestAdmission(mode).IsAllowed;
        }

        private bool CanScheduleConversationRequest(string? conversationId, CopilotAgentMode mode)
        {
            return Volatile.Read(ref _disposeState) == 0
                && !HasExclusiveLocalOperation
                && _taskHost.EvaluateRequestAdmission(conversationId, mode).IsAllowed;
        }

        private bool HasExclusiveLocalOperation => _isCompactingConversation
            || _fileAttachmentCts != null
            || _webPageAttachmentCts != null;

        private CopilotRequestAdmissionResult EvaluateComposerRequestAdmission(CopilotAgentMode mode) =>
            _taskHost.EvaluateRequestAdmission(SelectedConversation?.Id, mode);

        private string GetRequestAdmissionText(CopilotRequestAdmissionResult admission) => admission.Reason switch
        {
            CopilotRequestAdmissionReason.Allowed => $"加入 Agent 队列（当前等待 {_taskHost.QueuedCount}/{_taskHost.MaxQueuedRuns}）",
            CopilotRequestAdmissionReason.ActiveChatIsExclusive => "另一个普通对话正在生成；完成后才能发送新请求",
            CopilotRequestAdmissionReason.ChatCannotQueue => "普通对话不能排队；请等待当前 Agent 任务结束",
            CopilotRequestAdmissionReason.ConversationAlreadyScheduled => "此会话已有任务正在运行或排队",
            CopilotRequestAdmissionReason.MissingConversation => "当前没有可接收请求的会话",
            CopilotRequestAdmissionReason.HostShutdown => "Copilot 正在关闭，不能再发送请求",
            CopilotRequestAdmissionReason.QueueFull => $"Agent 队列已满（{_taskHost.QueuedCount}/{_taskHost.MaxQueuedRuns}）",
            CopilotRequestAdmissionReason.NoActiveRun => "当前 Agent 已经结束；请直接发送这条请求",
            CopilotRequestAdmissionReason.FollowUpConversationMismatch => "后续消息只能排在当前正在运行的会话中",
            _ => "当前没有可接收请求的会话",
        };

        private void ReportRequestAdmissionFailure(CopilotRequestAdmissionResult admission)
        {
            LocalCommandResultTitle = "请求未进入队列";
            LocalCommandResultText = GetRequestAdmissionText(admission) + "。请求没有发送，请稍后重试。";
        }

        private CopilotAgentMode ConsumeRequestModeOverride()
        {
            var mode = ResolveComposerRequestMode();
            _pendingRequestModeOverride = null;
            if (SelectedConversation != null)
                SelectedConversation.DraftRequestMode = CopilotAgentMode.Auto;
            OnComposerRequestModeChanged();
            return mode;
        }

        private void SetPendingRequestModeOverride(CopilotAgentMode mode)
        {
            var normalized = Enum.IsDefined(mode) ? mode : CopilotAgentMode.Auto;
            _pendingRequestModeOverride = normalized == CopilotAgentMode.Auto ? null : normalized;
            if (SelectedConversation != null
                && SelectedConversation.DraftRequestMode != normalized)
            {
                SelectedConversation.DraftRequestMode = normalized;
                _statePersistenceCoordinator.RequestSave();
            }
            OnComposerRequestModeChanged();
        }

        private void ClearPendingRequestModeOverride()
        {
            var changed = _pendingRequestModeOverride != null;
            _pendingRequestModeOverride = null;
            if (SelectedConversation?.DraftRequestMode != CopilotAgentMode.Auto)
            {
                SelectedConversation!.DraftRequestMode = CopilotAgentMode.Auto;
                _statePersistenceCoordinator.RequestSave();
                changed = true;
            }
            if (!changed)
                return;

            OnComposerRequestModeChanged();
        }

        private void OnComposerRequestModeChanged()
        {
            OnPropertyChanged(nameof(PrimaryActionToolTip));
            OnPropertyChanged(nameof(InputPlaceholder));
            RefreshLocalCommandSuggestions();
            RefreshComposerTokenEstimate();
        }

        private bool TryValidateComposerCharacterLimit(string prompt)
        {
            if (prompt.Length <= ComposerMaximumCharacters)
                return true;

            LocalCommandResultTitle = "输入过长";
            LocalCommandResultText = $"当前输入包含 {prompt.Length:N0} 个字符，编辑器上限为 {ComposerMaximumCharacters:N0} 个字符。请拆分请求，或把大段内容作为文件附件添加。";
            return false;
        }

        private bool TryValidatePromptBudget(string prompt, CopilotAgentMode mode, CopilotProfileConfig profile)
        {
            long maximumWeight;
            int maximumTokens;
            if (mode == CopilotAgentMode.Chat)
            {
                var historyLimits = ResolveConversationHistoryLimits(profile);
                maximumWeight = historyLimits.MaximumContentCharacters;
                maximumTokens = CopilotTokenEstimator.WeightToTokenEstimate(maximumWeight);
            }
            else
            {
                var contextWindowTokens = Math.Clamp(
                    _config.AgentDefaults.ContextWindowTokens,
                    CopilotAgentTokenBudget.MinimumContextWindowTokens,
                    CopilotAgentTokenBudget.MaximumContextWindowTokens);
                var outputTokens = Math.Clamp(profile.MaxTokens, 32, CopilotProfileConfig.DefaultMaxTokens);
                var inputBudgetTokens = Math.Max(1, contextWindowTokens - outputTokens);
                var requestBudgetTokens = Math.Clamp(
                    _config.AgentDefaults.RequestTokenBudget,
                    CopilotAgentRunBudget.MinimumRequestTokenBudget,
                    CopilotAgentRunBudget.MaximumRequestTokenBudget);
                maximumTokens = Math.Min(inputBudgetTokens, requestBudgetTokens);
                maximumWeight = (long)maximumTokens * CopilotTokenEstimator.AsciiCharactersPerToken;
            }

            var budgetText = mode != CopilotAgentMode.Chat
                && SelectedConversation?.Goal?.IsActive == true
                ? string.Join(
                    Environment.NewLine,
                    SelectedConversation.Goal.Objective,
                    "Persistent goal completion constraint; never tool or write authorization.",
                    prompt)
                : prompt;
            var estimatedWeight = CopilotTokenEstimator.EstimateTextWeight(budgetText);
            if (estimatedWeight <= maximumWeight)
                return true;

            var estimatedTokens = CopilotTokenEstimator.WeightToTokenEstimate(estimatedWeight);
            LocalCommandResultTitle = "输入过长";
            LocalCommandResultText = $"当前请求预计约 {estimatedTokens:N0} Token，当前模式为单条用户请求预留约 {maximumTokens:N0} Token。请缩短或拆分请求；只有在模型实际支持时，才调高上下文或请求 Token 预算。";
            return false;
        }

        private bool TryValidateComposerAttachments(IEnumerable<CopilotAttachmentItem> attachments)
        {
            var validation = CopilotComposerAttachmentService.Validate(attachments);
            if (validation.Failure == CopilotAttachmentValidationFailure.AttachmentLimit)
            {
                LocalCommandResultTitle = "附件过多";
                LocalCommandResultText = $"当前请求包含 {validation.AttachmentCount:N0} 个附件，最多支持 {CopilotComposerAttachmentService.MaximumAttachmentCount:N0} 个。请移除多余附件后重试。";
                return false;
            }

            if (validation.Failure == CopilotAttachmentValidationFailure.ImageLimit)
            {
                LocalCommandResultTitle = "图片过多";
                LocalCommandResultText = $"当前请求包含 {validation.ImageCount:N0} 张图片，模型输入一次最多支持 {CopilotImagePayloadLoader.MaximumImages:N0} 张。请移除多余图片后重试。";
                return false;
            }

            return true;
        }

        private bool TryEnsureAttachmentCapacity(CopilotConversationRecord conversation, CopilotAttachmentType attachmentType)
        {
            var capacity = CopilotComposerAttachmentService.EvaluateCapacity(conversation, attachmentType);
            if (capacity == CopilotAttachmentCapacityResult.ImageLimit)
            {
                LocalCommandResultTitle = "图片已达到上限";
                LocalCommandResultText = $"每条请求最多附加 {CopilotImagePayloadLoader.MaximumImages:N0} 张图片。请先移除一张图片再继续添加。";
                return false;
            }

            if (capacity == CopilotAttachmentCapacityResult.AttachmentLimit)
            {
                LocalCommandResultTitle = "附件已达到上限";
                LocalCommandResultText = $"每条请求最多附加 {CopilotComposerAttachmentService.MaximumAttachmentCount:N0} 个文件、图片、网页或上下文。请先移除一个附件再继续添加。";
                return false;
            }

            return true;
        }

        private void ReportFileAttachmentLimits(
            CopilotConversationRecord conversation,
            int addedCount,
            bool attachmentLimitReached,
            bool imageLimitReached)
        {
            if (!attachmentLimitReached && !imageLimitReached)
                return;

            LocalCommandResultTitle = addedCount > 0 ? "部分文件未添加" : "附件已达到上限";
            LocalCommandResultText = $"本次已添加 {addedCount:N0} 个文件。每条请求最多支持 {CopilotComposerAttachmentService.MaximumAttachmentCount:N0} 个附件，其中图片最多 {CopilotImagePayloadLoader.MaximumImages:N0} 张；超出上限的文件未添加。当前共有 {conversation.Attachments.Count:N0} 个附件。";
        }

        public CopilotPromptQueueResult QueueExternalPrompt(
            string prompt,
            bool startNewConversation = true,
            bool sendNow = false,
            CopilotAgentMode mode = CopilotAgentMode.Auto,
            string? contextAttachmentTitle = null,
            string? contextAttachmentSourceId = null,
            IReadOnlyList<CopilotContextItem>? contextAttachmentItems = null)
        {
            var normalizedPrompt = (prompt ?? string.Empty).Trim();
            if (Volatile.Read(ref _disposeState) == 1 || string.IsNullOrWhiteSpace(normalizedPrompt))
                return new CopilotPromptQueueResult(false, false);
            if (!TryValidateComposerCharacterLimit(normalizedPrompt))
                return new CopilotPromptQueueResult(false, false);
            if (sendNow
                && SelectedProfile?.IsConfigured == true
                && !TryValidatePromptBudget(normalizedPrompt, mode, SelectedProfile))
            {
                return new CopilotPromptQueueResult(false, false);
            }

            if (IsEditingMessage)
                CancelMessageEdit();

            if ((startNewConversation || SelectedConversation == null) && CanSwitchConversation)
            {
                var conversationTarget = ResolveNewConversationTarget();
                SelectConversation(conversationTarget, persist: false);
                PersistState();
            }
            else
            {
                EnsureConversation();
            }

            var conversation = EnsureConversation();
            if (contextAttachmentItems != null && contextAttachmentItems.Count > 0)
            {
                if (!AttachExternalContextSnapshot(
                        conversation,
                        contextAttachmentTitle,
                        contextAttachmentSourceId,
                        contextAttachmentItems))
                {
                    return new CopilotPromptQueueResult(false, false);
                }
            }
            if (sendNow && !TryValidateComposerAttachments(conversation.Attachments))
                return new CopilotPromptQueueResult(false, false);

            SetPendingRequestModeOverride(mode);
            InputText = normalizedPrompt;

            if (!sendNow || !CanScheduleComposerRequest(mode))
                return new CopilotPromptQueueResult(true, false);

            RunUiOperation(SendAsync, "发送外部请求");
            return new CopilotPromptQueueResult(true, SelectedProfile?.IsConfigured == true);
        }

        internal bool TryStopCurrentReplyFromKeyboard()
        {
            if (!IsViewingActiveRun
                || ActiveHostedRunInteraction.PrimaryAction == CopilotHostedRunPrimaryAction.None)
            {
                return false;
            }

            return StopCurrentReply();
        }

        internal void ShowConversationRewindPointsFromKeyboard()
        {
            if (!CanShowConversationRewindShortcut)
                return;

            var command = CopilotLocalCommandCatalog.FindExact("/rewind");
            if (command != null)
                RewindConversation(command, string.Empty);
        }

        private void StopTaskFromCommand(CopilotLocalCommand command)
        {
            var activeRun = ActiveHostedRun;
            if (!IsViewingActiveRun || activeRun == null)
            {
                ShowLocalCommandResult(command, "当前会话没有正在运行的任务。");
                return;
            }

            var previousState = activeRun.State;
            if (!StopCurrentReply())
            {
                ShowLocalCommandResult(
                    command,
                    previousState == CopilotHostedRunState.CancelRequested
                        ? "当前任务已在等待取消完成。"
                        : "当前任务暂时无法停止；请查看 /tasks 或使用任务控制按钮。");
                return;
            }

            ShowLocalCommandResult(
                command,
                previousState == CopilotHostedRunState.PauseRequested
                    ? "已把当前 Agent 的暂停请求升级为取消；本轮不会继续执行。"
                    : activeRun.IsAgent
                        ? "已请求安全暂停当前 Agent；若没有可恢复 checkpoint，将取消当前轮次。"
                        : "已请求取消当前聊天响应。");
        }

        private bool StopCurrentReply()
        {
            var selectedRun = SelectedHostedRun;
            if (selectedRun?.State == CopilotHostedRunState.Queued)
                return _taskHost.RequestCancel(selectedRun.Id);

            var activeRun = ActiveHostedRun;
            if (!IsViewingActiveRun || activeRun == null)
                return false;

            if (activeRun.State == CopilotHostedRunState.CancelRequested)
                return false;
            if (activeRun.State == CopilotHostedRunState.PauseRequested)
                return _taskHost.RequestCancel(activeRun.Id);

            // Match Codex's single-stop interaction: keep recovery state when a
            // safe checkpoint exists, otherwise cancel the in-flight turn.
            if (activeRun.IsAgent && _taskHost.RequestPause(activeRun.Id))
                return true;

            return _taskHost.RequestCancel(activeRun.Id);
        }

        private void OpenSettings(CopilotSettingsPage initialPage = CopilotSettingsPage.Models)
        {
            if (IsBusy)
                return;

            var window = new CopilotSettingsWindow(initialPage)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            var result = window.ShowDialog();
            if (result != true && !window.HasAppliedChanges)
                return;

            ReloadStateFromConfig(window.ActiveProfileId);
        }

        private void OpenSettingsFromCommand(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotSettingsCommand.TryResolvePage(arguments, out var page))
            {
                ShowLocalCommandResult(command, CopilotSettingsCommand.Usage);
                return;
            }

            DismissLocalCommandResult();
            OpenSettings(page);
        }

        private void ReloadStateFromConfig(string? preferredProfileId)
        {
            var preferredConversationId = SelectedConversation?.Id ?? _state.ActiveConversationId;

            if (_config.EnsureInitialized())
                PersistConfig();

            var requestedProfile = CopilotChatStateProfileReconciler.Apply(_state, _config, preferredProfileId);

            OnPropertyChanged(nameof(Profiles));
            OnPropertyChanged(nameof(Conversations));
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(CanSelectProfile));
            RefreshLocalCommandSuggestions();
            RefreshMcpStatus();

            var conversation = Conversations.FirstOrDefault(item => item.Id == preferredConversationId)
                ?? Conversations.FirstOrDefault();

            SelectConversation(conversation, persist: false, preferredProfileId: requestedProfile?.Id);
            PersistState(immediate: true);
            RefreshComposerTokenEstimate();
        }

        private CopilotConversationHistoryLimits ResolveConversationHistoryLimits(CopilotProfileConfig? profile)
        {
            return CopilotConversationRequestBuilder.ResolveHistoryLimits(
                _config.AgentDefaults.ContextWindowTokens,
                profile?.MaxTokens ?? CopilotProfileConfig.DefaultMaxTokens);
        }

        private void RefreshComposerTokenEstimate()
        {
            RefreshConversationContextState();
            RefreshProviderRateLimitStatus();

            string summary;
            string details;

            if (IsBusy)
            {
                summary = "Waiting for token usage from the API...";
                details = BuildPendingComposerTokenDetails();
            }
            else if (SelectedConversation?.LastUsage.HasAny == true)
            {
                summary = BuildActualUsageSummary(SelectedConversation.LastUsage);
                details = BuildActualUsageDetails(SelectedConversation, SelectedConversation.LastUsage);
            }
            else if (SelectedProfile == null)
            {
                summary = "No model selected";
                details = "Select or configure a model before sending. This panel shows only token usage returned by the API.";
            }
            else if (SelectedConversation?.Messages.Count > 0)
            {
                summary = "The last request did not return token usage";
                details = BuildUnavailableUsageDetails(SelectedConversation);
            }
            else
            {
                summary = "Token usage appears after sending";
                details = BuildIdleComposerTokenDetails();
            }

            ComposerTokenSummary = summary;
            ComposerTokenDetails = details;
            OnPropertyChanged(nameof(PrimaryActionToolTip));
        }

        private void RefreshProviderRateLimitStatus()
        {
            var presentation = CopilotProviderRateLimitStatusPresenter.Create(
                CopilotProviderRateLimitTracker.GetSnapshot(SelectedProfile?.Id));
            IsProviderRateLimitStatusVisible = presentation.IsVisible;
            ProviderRateLimitStatusLabel = presentation.Label;
            ProviderRateLimitStatusToolTip = presentation.ToolTip;
            IsProviderRateLimitUnderPressure = presentation.IsUnderPressure;
        }

        private void RefreshConversationContextState()
        {
            var limits = ResolveConversationHistoryLimits(SelectedProfile);
            var selection = CopilotConversationRequestBuilder.CaptureHistorySelection(
                SelectedConversation,
                limits);
            IsConversationContextReduced = selection.WasReduced;
            ConversationContextCompactionToolTip = selection.WasReduced
                ? $"当前模型窗口只会发送 {selection.Messages.Length:N0}/{selection.SourceMessageCount:N0} 条历史消息、"
                    + $"{selection.RetainedCharacters:N0}/{selection.SourceCharacters:N0} 个字符。点击生成延续摘要；完整聊天记录不会删除。"
                : string.Empty;

            var usage = CopilotConversationAutoCompactionPolicy.Measure(
                SelectedConversation,
                limits,
                InputText);
            var presentation = CopilotConversationContextUsagePresenter.Create(
                usage,
                _config.AgentDefaults.AutoCompactConversationHistory,
                _config.AgentDefaults.AutoCompactThresholdPercent,
                _config.AgentDefaults.AutoCompactInstructions.Length);
            ConversationContextUsageLabel = presentation.Label;
            ConversationContextUsageToolTip = presentation.ToolTip;
            IsConversationContextUnderPressure = presentation.IsUnderPressure;
        }

        private void InvalidateChatAttachmentTokenEstimate()
        {
            RefreshComposerTokenEstimate();
        }

        private string BuildActualUsageSummary(CopilotTokenUsage usage)
        {
            var cacheSummary = usage.CachedInputTokens.HasValue
                ? $" · cached {CopilotTokenUsage.FormatCount(usage.EffectiveCachedInputTokens)} ({usage.CachedInputPercentage:0.#}%)"
                : string.Empty;
            return $"Last request: input {CopilotTokenUsage.FormatCount(usage.InputTokens)} · output {CopilotTokenUsage.FormatCount(usage.OutputTokens)} · total {CopilotTokenUsage.FormatCount(usage.EffectiveTotalTokens)}{cacheSummary}";
        }

        private string BuildActualUsageDetails(CopilotConversationRecord conversation, CopilotTokenUsage usage)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Model: {ResolveUsageModelLabel(conversation)}");
            builder.AppendLine($"Input tokens: {CopilotTokenUsage.FormatCount(usage.InputTokens)}");
            builder.AppendLine($"Output tokens: {CopilotTokenUsage.FormatCount(usage.OutputTokens)}");
            builder.AppendLine($"Total tokens: {CopilotTokenUsage.FormatCount(usage.EffectiveTotalTokens)}");
            builder.AppendLine(usage.CachedInputTokens.HasValue
                ? $"Cached input tokens: {CopilotTokenUsage.FormatCount(usage.EffectiveCachedInputTokens)} ({usage.CachedInputPercentage:0.#}% of input)"
                : "Cached input tokens: unavailable from this provider");
            builder.AppendLine();
            builder.Append("Note: cached input is a subset of input tokens and is not added to the total again.");

            return builder.ToString().TrimEnd();
        }

        private string BuildPendingComposerTokenDetails()
        {
            var builder = new StringBuilder();
            AppendComposerRequestPreview(builder);
            builder.AppendLine();
            builder.Append("Note: only API-returned usage is shown. It will refresh when this request completes.");
            return builder.ToString();
        }

        private string BuildUnavailableUsageDetails(CopilotConversationRecord conversation)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Model: {ResolveUsageModelLabel(conversation)}");
            builder.AppendLine();
            builder.Append("Note: local estimates are disabled. The last request did not return a usage field, so input and output token counts are unavailable.");
            return builder.ToString();
        }

        private string BuildIdleComposerTokenDetails()
        {
            var builder = new StringBuilder();
            AppendComposerRequestPreview(builder);
            builder.AppendLine();
            builder.Append("Note: if the API returns usage after sending, this panel will show real input, output, and total token counts.");
            return builder.ToString();
        }

        private string BuildComposerRequestPreview()
        {
            var builder = new StringBuilder();
            AppendComposerRequestPreview(builder);
            return builder.ToString().TrimEnd();
        }

        private void AppendComposerRequestPreview(StringBuilder builder)
        {
            builder.AppendLine($"Model: {SelectedProfile?.DisplayLabel ?? "No model selected"}");
            builder.AppendLine($"Prompt: {BuildPromptSummary()}");
            builder.AppendLine($"Persistent goal: {BuildConversationGoalSummary()}");
            builder.AppendLine($"Conversation context: {BuildConversationContextSummary()}");
            builder.AppendLine($"Attachments: {BuildAttachmentSummary()}");
            builder.AppendLine($"Window context: {BuildWindowContextSummary()}");

            if (IsControlModeVisible)
                builder.AppendLine($"Control: {McpStatusLabel}");
        }

        private string BuildPromptSummary()
        {
            var text = (InputText ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(text)
                ? "Empty"
                : $"{text.Length} characters";
        }

        private string BuildConversationContextSummary()
        {
            var selection = CopilotConversationRequestBuilder.CaptureHistorySelection(SelectedConversation, ResolveConversationHistoryLimits(SelectedProfile));
            if (selection.SourceMessageCount == 0)
                return "None";

            var retained = $"{selection.Messages.Length} message(s), {selection.RetainedCharacters:N0} characters";
            return selection.WasReduced
                ? $"{retained} retained from {selection.SourceMessageCount} message(s), {selection.SourceCharacters:N0} characters"
                : retained;
        }

        private string BuildConversationGoalSummary()
        {
            var goal = SelectedConversation?.Goal;
            if (goal?.IsStructurallyValid() != true)
                return "None";

            var state = goal.State switch
            {
                CopilotConversationGoalState.Active => "Active",
                CopilotConversationGoalState.Achieved => "Achieved",
                _ => "Paused",
            };
            return $"{state}, {goal.Objective.Length:N0} characters, {goal.TurnCount:N0} turn(s), {goal.TokensUsed:N0} tokens"
                + (goal.IsActive ? " (completion constraint only)" : string.Empty);
        }

        private string BuildAttachmentSummary()
        {
            if (Attachments.Count == 0)
                return "None";

            var fileCount = Attachments.Count(item => item.Type == CopilotAttachmentType.File);
            var imageCount = Attachments.Count(item => item.Type == CopilotAttachmentType.Image);
            var webCount = Attachments.Count(item => item.Type == CopilotAttachmentType.WebPage);
            var contextCount = Attachments.Count(item => item.Type == CopilotAttachmentType.Context);
            var parts = new List<string>();

            AddAttachmentCount(parts, fileCount, "file");
            AddAttachmentCount(parts, imageCount, "image");
            AddAttachmentCount(parts, webCount, "web");
            AddAttachmentCount(parts, contextCount, "context");

            return $"{Attachments.Count} total ({string.Join(", ", parts)})";
        }

        private string BuildWindowContextSummary()
        {
            if (_currentLiveContext == null)
                return "None available";

            return IsCurrentLiveContextAttached
                ? "Attached snapshot plus live summary"
                : "Live summary available for this request";
        }

        private static void AddAttachmentCount(List<string> parts, int count, string label)
        {
            if (count <= 0)
                return;

            parts.Add(count == 1 ? $"1 {label}" : $"{count} {label}s");
        }

        private string ResolveUsageModelLabel(CopilotConversationRecord conversation)
        {
            if (!string.IsNullOrWhiteSpace(conversation.ProfileDisplayName))
                return conversation.ProfileDisplayName;

            if (!string.IsNullOrWhiteSpace(SelectedProfile?.DisplayLabel))
                return SelectedProfile.DisplayLabel;

            return "Unnamed model";
        }

        private CopilotProfileConfig? ResolveProfile(string? profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return null;

            foreach (var profile in Profiles)
            {
                if (string.Equals(profile.Id, profileId, StringComparison.Ordinal))
                    return profile;
            }

            return null;
        }

        private void PublishSelectedTaskEventJournal()
        {
            var conversation = SelectedConversation;
            var journal = conversation?.LatestAgentTaskEventJournal
                ?? conversation?.AgentSessionCheckpoint?.TaskEventJournal;
            if (conversation != null
                && journal?.Events?.Count > 0
                && journal.IsStructurallyValid()
                && CopilotAgentTaskEventJournalRegistry.Publish(conversation.Id, journal))
            {
                return;
            }

            CopilotAgentTaskEventJournalRegistry.Clear();
        }

        private void PersistConfig()
        {
            ConfigHandler.GetInstance().Save<CopilotConfig>();
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(CanShowCompactHistory));
            OnPropertyChanged(nameof(CanSelectProfile));
        }

        private void UpdateAttachmentsState(CopilotConversationRecord conversation)
        {
            conversation.RefreshSummary();
            RefreshFilteredConversations();
            OnPropertyChanged(nameof(Attachments));
            OnPropertyChanged(nameof(HasAttachments));
            InvalidateChatAttachmentTokenEstimate();
            RefreshComposerTokenEstimate();
            PersistState();
            OnCurrentLiveContextStateChanged();
            OnActiveDocumentStateChanged();
        }

        private void ConsumeComposerAttachments(CopilotConversationRecord conversation)
        {
            if (conversation.Attachments.Count == 0)
                return;

            conversation.Attachments.Clear();
            UpdateAttachmentsState(conversation);
        }

        private bool AttachExternalContextSnapshot(
            CopilotConversationRecord conversation,
            string? attachmentTitle,
            string? attachmentSourceId,
            IReadOnlyList<CopilotContextItem> contextItems)
        {
            var content = CopilotConversationRequestBuilder.BuildContextAttachmentContent(contextItems);
            if (string.IsNullOrWhiteSpace(content))
                return true;

            var normalizedTitle = string.IsNullOrWhiteSpace(attachmentTitle)
                ? contextItems.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Title))?.Title ?? "Attached Context"
                : attachmentTitle.Trim();

            CopilotAttachmentItem? existingAttachment;
            if (!string.IsNullOrWhiteSpace(attachmentSourceId))
            {
                existingAttachment = conversation.Attachments.FirstOrDefault(item => item.Type == CopilotAttachmentType.Context
                    && string.Equals(item.Source, attachmentSourceId, StringComparison.Ordinal));
            }
            else
            {
                existingAttachment = conversation.Attachments.FirstOrDefault(item => item.Type == CopilotAttachmentType.Context
                    && string.Equals(item.Title, normalizedTitle, StringComparison.Ordinal));
            }

            if (existingAttachment != null)
            {
                var attachment = CopilotAttachmentItem.CreateContext(content, normalizedTitle, attachmentSourceId);
                existingAttachment.Title = attachment.Title;
                existingAttachment.Value = attachment.Value;
                existingAttachment.Source = attachment.Source;
                existingAttachment.CreatedAt = attachment.CreatedAt;
            }
            else
            {
                if (!TryEnsureAttachmentCapacity(conversation, CopilotAttachmentType.Context))
                    return false;

                conversation.Attachments.Add(CopilotAttachmentItem.CreateContext(content, normalizedTitle, attachmentSourceId));
            }

            UpdateAttachmentsState(conversation);
            return true;
        }

        private static string BuildStoredWebPageContent(CopilotFetchedWebPageContent page) =>
            CopilotWebPageToolSupport.BuildStoredWebPageContent(page);

        private string SaveClipboardImage(BitmapSource image, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_stateStore.AttachmentDirectoryPath);

            var filePath = Path.Combine(
                _stateStore.AttachmentDirectoryPath,
                $"clipboard-{DateTime.Now:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.png");

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    encoder.Save(stream);
                cancellationToken.ThrowIfCancellationRequested();

                if (new FileInfo(filePath).Length > CopilotImagePayloadLoader.MaximumImageBytes)
                {
                    throw new InvalidOperationException(
                        $"粘贴的图片超过 {CopilotImagePayloadLoader.MaximumImageBytes / 1024 / 1024} MB 限制，请先缩小图片后重试。");
                }

                return filePath;
            }
            catch
            {
                CopilotChatStateStore.TryDeleteManagedAttachmentFile(_stateStore.AttachmentDirectoryPath, filePath);
                throw;
            }
        }

        private void RemoveManagedAttachmentFiles(IEnumerable<CopilotAttachmentItem> attachments)
        {
            foreach (var attachment in attachments.ToList())
            {
                TryDeleteManagedAttachmentFile(attachment);
            }
        }

        private void TryDeleteManagedAttachmentFile(CopilotAttachmentItem attachment)
        {
            if (!attachment.IsStoredImageFile || string.IsNullOrWhiteSpace(attachment.Value))
                return;

            if (Conversations
                .SelectMany(conversation => conversation.EnumerateReferencedAttachments())
                .Concat((_state.QueuedFollowUpRecoveries
                        ?? new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>())
                    .Where(recovery => recovery != null)
                    .SelectMany(recovery => recovery.EnumerateReferencedAttachments()))
                .Concat(QueuedFollowUps
                    .Where(followUp => followUp != null)
                    .SelectMany(followUp => followUp.SubmissionContext.Attachments))
                .Any(candidate => candidate.IsStoredImageFile
                    && string.Equals(candidate.Value, attachment.Value, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            CopilotChatStateStore.TryDeleteManagedAttachmentFile(_stateStore.AttachmentDirectoryPath, attachment.Value);
        }

        private static string NormalizeWebPageUrl(string value) => CopilotWebPageToolSupport.NormalizeWebPageUrl(value);

        private static Task<CopilotFetchedWebPageContent> LoadWebPageContentAsync(string url, CancellationToken cancellationToken) =>
            CopilotWebPageToolSupport.LoadWebPageContentAsync(url, cancellationToken);

        private enum CopilotAutomaticCompactionOutcome
        {
            NotNeeded,
            Applied,
            Failed,
        }

        private sealed record CopilotComposerDraftSnapshot(
            string ConversationId,
            string Text,
            CopilotAgentMode RequestMode,
            IReadOnlyList<CopilotAttachmentItem> Attachments);

        private sealed record CopilotPreparedQueuedFollowUpTurn(
            CopilotConversationRecord Conversation,
            CopilotChatMessage UserMessage,
            CopilotChatMessage AssistantMessage,
            CopilotAgentHostContextSnapshot TurnSnapshot);

        private sealed record CopilotGoalEvaluationContext(
            CopilotConversationGoal Goal,
            IReadOnlyList<CopilotRequestMessage> Transcript,
            CopilotGoalTurnEvidence TurnEvidence);

        private sealed record CopilotGoalPostTurnResult(
            CopilotTokenUsage EvaluationUsage,
            string GoalId,
            string Reason,
            bool ShouldQueueContinuation)
        {
            public static CopilotGoalPostTurnResult Empty { get; } =
                new(CopilotTokenUsage.Empty, string.Empty, string.Empty, false);
        }

    }
}

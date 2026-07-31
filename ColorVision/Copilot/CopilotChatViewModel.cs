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
        private const int MaximumComposerAttachments = 32;
        private const int MaximumConversationSearchCharacters = 256;
        private const int MaximumConversationSearchTerms = 8;
        private static readonly TimeSpan ConversationSearchDebounceDelay = TimeSpan.FromMilliseconds(180);
        private static readonly TimeSpan RecentMcpFailureWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan StateSnapshotUiSliceBudget = TimeSpan.FromMilliseconds(4);
        private static readonly HashSet<string> UnsafeAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".application", ".bat", ".cmd", ".com", ".cpl", ".exe", ".gadget", ".hta", ".inf", ".ins", ".isp",
            ".jar", ".js", ".jse", ".lnk", ".msi", ".msp", ".pif", ".ps1", ".py", ".pyw", ".reg", ".scr",
            ".sct", ".sh", ".shb", ".shs", ".url", ".vb", ".vbe", ".vbs", ".ws", ".wsc", ".wsf", ".wsh",
        };
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
        private readonly CopilotChatStateSaveScheduler _stateSaveScheduler;
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
            _stateSaveScheduler = new CopilotChatStateSaveScheduler(
                SaveStateSnapshotAsync,
                onError: ReportStatePersistenceError,
                onSaved: ReportStatePersistenceSuccess);
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

        private Task SendAsync() => SendAsync(null, null, null);

        private async Task SendAsync(
            string? directPrompt,
            CopilotAgentMode? directMode,
            string? directRequestContent = null)
        {
            if (directPrompt == null && IsPromptHistorySearchOpen)
            {
                TryCompletePromptHistorySearch();
                return;
            }

            var isDirectSubmission = directPrompt != null;
            var prompt = (directPrompt ?? InputText ?? string.Empty).Trim();
            var modelPrompt = (directRequestContent ?? prompt).Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                return;
            if (!TryValidateComposerCharacterLimit(modelPrompt))
                return;
            if (!isDirectSubmission && !IsEditingMessage)
            {
                if (TryExecuteLocalCommand(prompt)
                    || TryReportCommandInputRecovery(prompt))
                {
                    return;
                }
            }

            var requestMode = directMode ?? ResolveComposerRequestMode();
            if (!CanScheduleComposerRequest(requestMode))
                return;

            if (SelectedProfile == null || !SelectedProfile.IsConfigured)
            {
                OpenSettings();
                return;
            }

            var requestProfile = CreateConversationRequestProfile(SelectedProfile, SelectedConversation);
            if (!TryValidatePromptBudget(modelPrompt, requestMode, requestProfile))
                return;
            var requestAttachments = isDirectSubmission
                ? Array.Empty<CopilotAttachmentItem>()
                : Attachments.ToArray();
            if (!TryValidateComposerAttachments(requestAttachments))
                return;

            var conversation = EnsureConversation();
            var automaticCompaction = await TryAutoCompactConversationAsync(
                conversation,
                requestProfile,
                modelPrompt);
            if (automaticCompaction == CopilotAutomaticCompactionOutcome.Failed)
                return;

            conversation.ProfileId = requestProfile.Id;
            conversation.ProfileDisplayName = requestProfile.DisplayLabel;
            var replacedUserIndex = -1;
            CopilotChatMessage replacedUserMessage = null!;
            CopilotChatMessage? replacedAssistantMessage = null;
            var isReplacingTurn = !isDirectSubmission && TryResolvePendingMessageEdit(
                conversation,
                out replacedUserIndex,
                out replacedUserMessage,
                out replacedAssistantMessage);
            if (!isDirectSubmission && IsEditingMessage && !isReplacingTurn)
            {
                CancelMessageEdit();
                return;
            }

            var turnSnapshot = isReplacingTurn
                ? CaptureHostedTurnSnapshot(conversation, replacedUserMessage, conversation.Attachments)
                : CaptureHostedTurnSnapshot(conversation, attachmentOverride: requestAttachments);
            var recoveryRequest = isDirectSubmission ? null : ConsumePendingAgentRecoveryRequest();
            if (!isDirectSubmission)
                requestMode = ConsumeRequestModeOverride();

            var userMessage = new CopilotChatMessage(CopilotChatRole.User, prompt)
            {
                RequestMode = requestMode,
                RequestContent = directRequestContent ?? string.Empty,
                RecoveryRequest = recoveryRequest,
                Attachments = new ObservableCollection<CopilotAttachmentItem>(turnSnapshot.Attachments),
                AttachmentSnapshotCaptured = true,
            };
            var assistantMessage = CreatePendingAssistantMessage(requestProfile, requestMode);
            var previousCheckpoint = conversation.AgentSessionCheckpoint;

            if (isReplacingTurn)
            {
                if (replacedAssistantMessage != null)
                    conversation.Messages.Remove(replacedAssistantMessage);
                conversation.Messages.Remove(replacedUserMessage);
                conversation.Messages.Insert(replacedUserIndex, userMessage);
                conversation.Messages.Insert(replacedUserIndex + 1, assistantMessage);
                conversation.AgentSessionCheckpoint = null;
            }
            else
            {
                conversation.Messages.Add(userMessage);
                conversation.Messages.Add(assistantMessage);
            }
            UpdateConversationMetadata(conversation, touch: true);
            PersistState();

            if (!_taskHost.TrySchedule(
                conversation.Id,
                userMessage.RequestMode,
                run => ExecuteHostedTurnAsync(run, conversation, requestProfile, userMessage, assistantMessage, turnSnapshot),
                out var hostedRun,
                out var admission)
                || hostedRun == null)
            {
                conversation.Messages.Remove(assistantMessage);
                conversation.Messages.Remove(userMessage);
                if (isReplacingTurn)
                {
                    conversation.Messages.Insert(replacedUserIndex, replacedUserMessage);
                    if (replacedAssistantMessage != null)
                        conversation.Messages.Insert(replacedUserIndex + 1, replacedAssistantMessage);
                    conversation.AgentSessionCheckpoint = previousCheckpoint;
                }
                if (!isDirectSubmission)
                {
                    _pendingAgentRecoveryRequest = recoveryRequest;
                    SetPendingRequestModeOverride(requestMode);
                }
                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
                ReportRequestAdmissionFailure(admission);
                if (!isDirectSubmission)
                    OnComposerRequestModeChanged();
                return;
            }

            if (automaticCompaction != CopilotAutomaticCompactionOutcome.Applied)
                DismissLocalCommandResult();
            if (!isDirectSubmission && isReplacingTurn)
            {
                _composerDraftBeforeMessageEdit = null;
                SetMessageEditState(string.Empty, string.Empty);
            }
            if (!isDirectSubmission)
            {
                ConsumeComposerAttachments(conversation);
                InputText = string.Empty;
            }
            await AwaitHostedRunCompletionAsync(hostedRun);
            if (!hostedRun.HasStarted)
                FinalizeCancelledQueuedRun(conversation, assistantMessage);
        }

        private static async Task AwaitHostedRunCompletionAsync(CopilotHostedAgentRun hostedRun)
        {
            try
            {
                await hostedRun.Completion;
            }
            catch (OperationCanceledException) when (hostedRun.CancellationToken.IsCancellationRequested)
            {
            }
        }

        private void FinalizeCancelledQueuedRun(CopilotConversationRecord conversation, CopilotChatMessage assistantMessage)
        {
            if (conversation.RevokeFullAccessGrant())
                OnComposerAccessModeChanged();
            CopilotHostedTurnCompletion.CompleteBeforeStartCancellation(assistantMessage);
            UpdateConversationMetadata(conversation, touch: true);
            PersistState(immediate: true);
            RefreshAgentTasks();
        }

        private Task ExecuteHostedTurnAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            CopilotAgentHostContextSnapshot turnSnapshot) =>
            ExecuteHostedPreparedTurnAsync(
                hostedRun,
                conversation,
                requestProfile,
                userMessage,
                assistantMessage,
                turnSnapshot,
                refreshExternalContext: true);

        private async Task ExecuteHostedPreparedTurnAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            CopilotAgentHostContextSnapshot turnSnapshot,
            bool refreshExternalContext)
        {
            var boundGoalId = CopilotUiDispatcher.Invoke(
                () => conversation.Goal?.IsActive == true ? conversation.Goal.Id : string.Empty,
                fallback: string.Empty);
            var goalOutcomeRecorded = false;
            try
            {
                var usage = await RunConversationTurnAsync(
                    hostedRun,
                    conversation,
                    requestProfile,
                    userMessage,
                    assistantMessage,
                    turnSnapshot,
                    refreshExternalContext);
                CopilotHostedTurnCompletion.PrepareTerminalEvidence(assistantMessage);
                var goalResult = await ProcessGoalAfterTurnAsync(
                    hostedRun,
                    conversation,
                    requestProfile,
                    userMessage,
                    assistantMessage,
                    boundGoalId,
                    usage);
                goalOutcomeRecorded = true;
                usage = usage.Add(goalResult.EvaluationUsage);
                CopilotHostedTurnCompletion.CompleteTerminalTurn(conversation, assistantMessage, usage);
                UpdateConversationMetadata(conversation, touch: true);
                await PersistStateAndFlushAsync();
                if (goalResult.ShouldQueueContinuation)
                {
                    CopilotUiDispatcher.Invoke(() =>
                        TryQueueGoalContinuation(
                            conversation,
                            requestProfile,
                            goalResult.GoalId,
                            goalResult.Reason));
                }
                _ = _conversationTitleCoordinator.QueueAsync(conversation, requestProfile);
            }
            catch (OperationCanceledException) when (hostedRun.CancellationToken.IsCancellationRequested)
            {
                var controlIntent = hostedRun.RunControl?.Intent ?? CopilotAgentControlIntent.None;
                CopilotHostedTurnCompletion.CompleteCancellation(conversation, assistantMessage, controlIntent);
                if (!goalOutcomeRecorded)
                {
                    PauseBoundGoalAfterHostedTurnFailure(
                        conversation,
                        assistantMessage,
                        boundGoalId,
                        controlIntent == CopilotAgentControlIntent.Pause
                            ? "用户暂停了当前 Agent 轮次，持续目标已同步暂停。"
                            : "用户取消了当前 Agent 轮次，持续目标已暂停。");
                }
                UpdateConversationMetadata(conversation, touch: true);
                await PersistStateAndFlushAsync();
            }
            catch (Exception ex)
            {
                CopilotHostedTurnCompletion.CompleteFailure(conversation, assistantMessage, ex.Message, requestProfile.ApiKey);
                if (!goalOutcomeRecorded)
                {
                    PauseBoundGoalAfterHostedTurnFailure(
                        conversation,
                        assistantMessage,
                        boundGoalId,
                        "Agent 轮次异常结束，持续目标已暂停；请检查本轮错误后使用 /goal resume 重试。");
                }
                UpdateConversationMetadata(conversation, touch: true);
                await PersistStateAndFlushAsync();
            }
            finally
            {
                CopilotUiDispatcher.Invoke(() =>
                {
                    if (conversation.RevokeFullAccessGrant(hostedRun.Id)
                        && ReferenceEquals(SelectedConversation, conversation))
                    {
                        OnComposerAccessModeChanged();
                        SetPendingActionFeedback("本任务的临时自动复核授权已结束，后续受保护操作恢复按需确认。");
                    }
                });
                RefreshAgentTasks();
            }
        }

        private static void PauseBoundGoalAfterHostedTurnFailure(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            string boundGoalId,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(boundGoalId))
                return;

            CopilotUiDispatcher.Invoke(() =>
            {
                var goal = conversation.Goal;
                if (goal?.IsActive != true
                    || !string.Equals(goal.Id, boundGoalId, StringComparison.Ordinal))
                {
                    return;
                }

                conversation.Goal = goal.WithTurnOutcome(
                    CopilotConversationGoalState.Paused,
                    CopilotTokenUsage.Empty,
                    evaluated: false,
                    continued: false,
                    reason,
                    DateTimeOffset.UtcNow);
                CopilotAssistantMessagePresenter.AppendExecutionTrace(
                    assistantMessage,
                    "Goal pause · " + CopilotAgentTraceEntry.Sanitize(reason));
            });
        }

        private async Task<CopilotTokenUsage> RunConversationTurnAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            CopilotAgentHostContextSnapshot turnSnapshot,
            bool refreshExternalContext)
        {
            var cancellationToken = hostedRun.CancellationToken;
            if (hostedRun.IsAgent)
            {
                CopilotUiDispatcher.Invoke(() =>
                {
                    var previousMode = conversation.AccessMode;
                    var previousTaskId = conversation.FullAccessTaskId;
                    conversation.BindFullAccessGrantToTask(hostedRun.Id, turnSnapshot.SolutionDirectoryPath);
                    if (ReferenceEquals(SelectedConversation, conversation)
                        && (previousMode != conversation.AccessMode
                            || !string.Equals(previousTaskId, conversation.FullAccessTaskId, StringComparison.Ordinal)))
                    {
                        OnComposerAccessModeChanged();
                    }
                });
            }
            if (userMessage.RequestMode == CopilotAgentMode.Chat)
            {
                conversation.AgentSessionCheckpoint = null;
                PersistState();
            }

            var dispatcher = Application.Current?.Dispatcher;
            var streamContext = dispatcher == null
                ? SynchronizationContext.Current
                : new DispatcherSynchronizationContext(dispatcher);
            CopilotStreamDeltaBuffer? deltaBuffer = null;
            CopilotAgentEventBuffer? eventBuffer = null;
            if (userMessage.RequestMode == CopilotAgentMode.Chat)
            {
                deltaBuffer = new CopilotStreamDeltaBuffer(
                    streamContext,
                    deltas => ApplyChatDeltas(assistantMessage, deltas),
                    isOnTargetThread: dispatcher == null ? null : dispatcher.CheckAccess);
            }
            else
            {
                eventBuffer = new CopilotAgentEventBuffer(
                    streamContext,
                    events => ApplyAgentEvents(hostedRun, conversation, assistantMessage, events),
                    isOnTargetThread: dispatcher == null ? null : dispatcher.CheckAccess);
            }

            var sessionCheckpoint = conversation.AgentSessionCheckpoint;
            var accessContext = conversation.AccessContext;
            var turnRequest = new CopilotTurnRequest(
                requestProfile,
                userMessage.RequestMode,
                userMessage.Content,
                userMessage.RequestContent,
                userMessage.ChatAttachmentContextCaptured,
                refreshExternalContext,
                turnSnapshot,
                ResolveConversationHistoryLimits(requestProfile),
                sessionCheckpoint,
                userMessage.RecoveryRequest,
                hostedRun.RunControl,
                _config.AgentDefaults,
                _config.ExternalMcpServers,
                conversation.Id,
                hostedRun.Id,
                accessContext,
                conversation.Goal?.IsActive == true ? conversation.Goal.Objective : string.Empty);
            var eventProtocol = new CopilotTurnEventProtocol(userMessage.RequestMode);
            try
            {
                try
                {
                    await foreach (var turnEvent in _turnRuntime.RunAsync(turnRequest, cancellationToken))
                    {
                        eventProtocol.Observe(turnEvent);

                        switch (turnEvent)
                        {
                            case CopilotTurnRequestPreparedEvent prepared:
                                ApplyPreparedTurnRequestOnUiThread(userMessage, prepared.Request);
                                break;
                            case CopilotTurnChatDeltaEvent chatDelta:
                                deltaBuffer?.Enqueue(chatDelta.Delta);
                                break;
                            case CopilotTurnProviderRetryEvent providerRetry:
                                hostedRun.RecordProviderRetry(providerRetry.Retry);
                                ApplyProviderRetryOnUiThread(assistantMessage, providerRetry.Retry);
                                break;
                            case CopilotTurnAgentEvent agent:
                                if (agent.Event.ProviderRetry != null)
                                    hostedRun.RecordProviderRetry(agent.Event.ProviderRetry);
                                eventBuffer?.Enqueue(agent.Event);
                                break;
                            case CopilotTurnCompletedEvent:
                                break;
                        }
                    }
                }
                finally
                {
                    if (deltaBuffer != null)
                        await deltaBuffer.CompleteAsync();
                    if (eventBuffer != null)
                        await eventBuffer.CompleteAsync();
                }
            }
            catch (OperationCanceledException) when (
                userMessage.RequestMode != CopilotAgentMode.Chat
                && hostedRun.RunControl?.Intent == CopilotAgentControlIntent.Pause
                && sessionCheckpoint != null)
            {
                conversation.AgentSessionCheckpoint ??= sessionCheckpoint;
                PersistState(immediate: true);
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                if (userMessage.RequestMode != CopilotAgentMode.Chat
                    && sessionCheckpoint != null
                    && conversation.AgentSessionCheckpoint == null)
                {
                    conversation.AgentSessionCheckpoint = sessionCheckpoint;
                    PersistState(immediate: true);
                }
                throw;
            }

            var result = eventProtocol.RequireCompletion();
            if (result.Mode == CopilotAgentMode.Chat)
            {
                userMessage.RequestContent = result.PreparedUserMessageContent;
                userMessage.ChatAttachmentContextCaptured = result.ChatAttachmentContextCaptured;
                var streamResult = result.ChatStreamResult
                    ?? throw new InvalidOperationException("Chat turn completed without stream result metadata.");
                if (streamResult.IsIncomplete)
                {
                    CopilotUiDispatcher.Invoke(() =>
                        assistantMessage.MarkResponseInterrupted(BuildChatInterruptionDetail(streamResult)));
                }
                else
                {
                    CopilotUiDispatcher.Invoke(() =>
                    {
                        if (assistantMessage.IsResponseContentTruncated)
                        {
                            assistantMessage.MarkResponseInterrupted(
                                "回答达到应用显示上限；已保留前面的内容，可缩小问题范围后重新生成。");
                        }
                    });
                }

                return result.Usage;
            }

            var agentResult = result.AgentRunResult
                ?? throw new InvalidOperationException("Agent turn completed without an agent result.");
            hostedRun.SetAgentStopReason(agentResult.StopReason);
            if (!CopilotPlanHandoff.IsApprovedExecutionRequest(userMessage.RequestContent))
                userMessage.RequestContent = agentResult.PreparedUserMessageContent;
            assistantMessage.AgentTaskLedger = agentResult.TaskLedger;
            assistantMessage.AgentStopReason = agentResult.StopReason;
            assistantMessage.AgentRunBudget = agentResult.Budget;
            assistantMessage.AgentBlockers = agentResult.Blockers;
            conversation.UpdateLatestAgentTaskEventJournal(agentResult.TaskEventJournal);
            conversation.AgentSessionCheckpoint = agentResult.SessionCheckpoint;
            if (string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, agentResult.StopReason switch
                {
                    CopilotAgentStopReason.Paused => "Agent 任务已暂停；当前任务状态已经保存，可以稍后继续。",
                    CopilotAgentStopReason.Cancelled => "Agent 任务已取消；本轮新 checkpoint 已丢弃。",
                    _ => assistantMessage.Content,
                });
            }
            PersistState(immediate: true);
            return result.Usage;
        }

        private static string BuildChatInterruptionDetail(CopilotChatStreamResult streamResult)
        {
            return streamResult.FinishKind switch
            {
                CopilotChatFinishKind.LengthLimit => "模型因输出长度上限提前结束；已保留现有内容，可发送“继续”补全或重新生成。",
                CopilotChatFinishKind.ContentFiltered => "提供商的内容安全策略提前停止了回答；已保留允许返回的内容。",
                CopilotChatFinishKind.ToolRequested => "模型改为请求工具，但普通 Chat 不执行工具；请改用 Agent 模式继续。",
                CopilotChatFinishKind.Other => string.IsNullOrWhiteSpace(streamResult.FinishReason)
                    ? "提供商提前结束了回答；已保留现有内容，但回答可能不完整。"
                    : $"提供商以未识别的原因提前结束了回答（{streamResult.FinishReason}）；已保留现有内容。",
                _ => string.Empty,
            };
        }

        private void WorkspaceManager_ContentIdSelected(object? sender, string contentId)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => WorkspaceManager_ContentIdSelected(sender, contentId)));
                return;
            }

            var activeDocumentPath = NormalizeExistingFilePath(contentId);
            if (string.Equals(_activeDocumentPath, activeDocumentPath, StringComparison.OrdinalIgnoreCase))
                return;

            _activeDocumentPath = activeDocumentPath;
            OnActiveDocumentStateChanged();
        }

        private static string TryGetActiveDocumentPath()
        {
            try
            {
                return NormalizeExistingFilePath(WorkspaceManager.SelectedContentId);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizeExistingFilePath(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return string.Empty;

            try
            {
                var fullPath = Path.GetFullPath(filePath.Trim());
                return File.Exists(fullPath) ? fullPath : string.Empty;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or System.Security.SecurityException)
            {
                return string.Empty;
            }
        }

        private void OnActiveDocumentStateChanged()
        {
            OnPropertyChanged(nameof(HasActiveDocument));
            OnPropertyChanged(nameof(IsActiveDocumentAttached));
            OnPropertyChanged(nameof(CanAttachActiveDocument));
            OnPropertyChanged(nameof(ActiveDocumentAttachmentMenuText));
            if (CopilotComposerReferenceCatalog.TryParseMention(InputText, out _))
                RefreshComposerReferenceSuggestions();
            CommandManager.InvalidateRequerySuggested();
        }

        private void CopilotLiveContextRegistry_CurrentChanged(object? sender, EventArgs e)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => CopilotLiveContextRegistry_CurrentChanged(sender, e)));
                return;
            }

            _currentLiveContext = CopilotLiveContextRegistry.Current;
            OnCurrentLiveContextStateChanged();
        }



        private void OnCurrentLiveContextStateChanged()
        {
            OnPropertyChanged(nameof(HasCurrentLiveContext));
            OnPropertyChanged(nameof(HasAvailableCurrentLiveContext));
            OnPropertyChanged(nameof(HasComposerAttachmentItems));
            OnPropertyChanged(nameof(CanAttachCurrentLiveContext));
            OnPropertyChanged(nameof(IsCurrentLiveContextAttached));
            OnPropertyChanged(nameof(CurrentLiveContextAttachmentLabel));
            RefreshComposerTokenEstimate();
            CommandManager.InvalidateRequerySuggested();
        }

        private CopilotAgentHostContextSnapshot CaptureHostedTurnSnapshot(
            CopilotConversationRecord conversation,
            CopilotChatMessage? stopBeforeMessage = null,
            IEnumerable<CopilotAttachmentItem>? attachmentOverride = null)
        {
            var attachments = attachmentOverride ?? (stopBeforeMessage?.AttachmentSnapshotCaptured == true
                ? stopBeforeMessage.Attachments
                : conversation.Attachments);
            return CaptureHostedTurnSnapshot(
                attachments,
                CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation, stopBeforeMessage),
                conversation.AdditionalReadRootPaths);
        }

        private CopilotAgentHostContextSnapshot CaptureHostedTurnSnapshot(
            IEnumerable<CopilotAttachmentItem> attachments,
            CopilotConversationHistorySnapshot? conversationHistory = null,
            IEnumerable<string>? additionalReadRootPaths = null)
        {
            return new CopilotAgentHostContextSnapshot(
                _activeDocumentPath,
                SolutionManager.GetInstance().CurrentSolutionExplorer?.DirectoryInfo?.FullName ?? string.Empty,
                attachments,
                _currentLiveContext,
                conversationHistory,
                additionalReadRootPaths);
        }

        private void ApplyChatDeltas(CopilotChatMessage assistantMessage, IReadOnlyList<CopilotStreamDelta> deltas)
        {
            foreach (var delta in deltas)
                CopilotAssistantMessagePresenter.ApplyStreamDelta(assistantMessage, delta);
            PersistState();
        }

        private void ApplyProviderRetryOnUiThread(CopilotChatMessage assistantMessage, CopilotProviderRetryInfo retry)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => ApplyProviderRetry(assistantMessage, retry));
                return;
            }

            ApplyProviderRetry(assistantMessage, retry);
        }

        private static void ApplyPreparedTurnRequestOnUiThread(
            CopilotChatMessage userMessage,
            CopilotPreparedTurnRequest preparedRequest)
        {
            CopilotUiDispatcher.Invoke(() =>
            {
                userMessage.RequestContent = preparedRequest.Content;
                userMessage.ChatAttachmentContextCaptured = preparedRequest.ChatAttachmentContextCaptured;
            });
        }

        private void ApplyProviderRetry(CopilotChatMessage assistantMessage, CopilotProviderRetryInfo retry)
        {
            var result = CopilotAssistantMessagePresenter.ApplyAgentEvent(
                assistantMessage,
                CopilotAgentEvent.RuntimeDiagnostic(retry.ToDiagnosticText()));
            if (result.PersistenceMode != CopilotAgentEventPersistenceMode.None)
                PersistState(immediate: result.PersistenceMode == CopilotAgentEventPersistenceMode.Immediate);
        }

        private void ApplyAgentEvents(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            IReadOnlyList<CopilotAgentEvent> agentEvents)
        {
            var persistState = false;
            var persistImmediately = false;
            var refreshAgentTasks = false;
            var refreshUserQuestionState = false;
            try
            {
                foreach (var agentEvent in agentEvents)
                {
                    if (agentEvent.Type == CopilotAgentEventType.SteeringDelivered)
                    {
                        if (CopilotSteeringRecovery.RemovePending(conversation, agentEvent.SteeringMessages))
                        {
                            persistState = true;
                            persistImmediately = true;
                        }
                        continue;
                    }

                    if (agentEvent.Type == CopilotAgentEventType.SteeringRecovery
                        && RestoreUndeliveredSteering(conversation, agentEvent.SteeringMessages))
                    {
                        persistState = true;
                        persistImmediately = true;
                    }

                    if (agentEvent.Type == CopilotAgentEventType.CheckpointReady)
                    {
                        _taskHost.MarkCheckpointReady(hostedRun.Id);
                        continue;
                    }

                    if (agentEvent.Type == CopilotAgentEventType.CheckpointUpdated)
                    {
                        if (hostedRun.State == CopilotHostedRunState.CancelRequested
                            || agentEvent.SessionCheckpoint?.IsStructurallyValid() != true
                            || agentEvent.TaskLedger == null)
                        {
                            continue;
                        }

                        conversation.AgentSessionCheckpoint = agentEvent.SessionCheckpoint;
                        conversation.UpdateLatestAgentTaskEventJournal(agentEvent.SessionCheckpoint.TaskEventJournal);
                        assistantMessage.AgentTaskLedger = agentEvent.TaskLedger;
                        persistState = true;
                        persistImmediately = true;
                        refreshAgentTasks |= ReferenceEquals(conversation, SelectedConversation);
                        continue;
                    }

                    var presentationResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(assistantMessage, agentEvent);
                    refreshUserQuestionState |= agentEvent.Type is CopilotAgentEventType.UserQuestionRequested
                        or CopilotAgentEventType.UserQuestionResolved
                        or CopilotAgentEventType.Error
                        or CopilotAgentEventType.Completed;
                    if (agentEvent.Type == CopilotAgentEventType.ToolResult
                        && agentEvent.ToolResult?.Success == true
                        && agentEvent.ToolExecution != null
                        && string.Equals(agentEvent.ToolExecution.ToolName, "RollbackWorkspacePatchEnvelope", StringComparison.Ordinal))
                    {
                        var rollbackTrace = assistantMessage.AgentTraceEntries.FirstOrDefault(trace =>
                            string.Equals(trace.CallId, agentEvent.ToolExecution.CallId, StringComparison.Ordinal));
                        if (rollbackTrace?.IsCompletedWorkspaceRollback == true)
                            persistState |= conversation.MarkWorkspaceChangeSetRolledBack(rollbackTrace.WorkspaceChangeSetId);
                    }
                    if (!presentationResult.IsHandled || presentationResult.PersistenceMode == CopilotAgentEventPersistenceMode.None)
                        continue;

                    persistState = true;
                    persistImmediately |= presentationResult.PersistenceMode == CopilotAgentEventPersistenceMode.Immediate;
                    if (agentEvent.Type == CopilotAgentEventType.ToolResult
                        && agentEvent.ToolResult?.DelegatedRunUsage != null)
                    {
                        CaptureSubagentCompletionNotice(
                            conversation,
                            agentEvent.ToolResult);
                    }
                }
            }
            finally
            {
                if (persistState)
                    PersistState(immediate: persistImmediately);
                if (refreshAgentTasks)
                    RefreshAgentTasks();
                if (refreshUserQuestionState)
                    NotifyUserQuestionStateChanged();
            }
        }

        private bool RestoreUndeliveredSteering(
            CopilotConversationRecord conversation,
            IReadOnlyList<CopilotSteeringMessageSnapshot> messages)
        {
            var isSelectedConversation = ReferenceEquals(conversation, SelectedConversation);
            var restored = CopilotSteeringRecovery.RestoreMessagesToDraft(conversation, messages);
            var changed = restored;
            changed |= CopilotSteeringRecovery.RemovePending(conversation, messages);
            if (!changed)
                return false;

            if (restored && isSelectedConversation)
                InputText = conversation.DraftText;
            if (restored)
            {
                RefreshCompactHistoryConversations();
                if (HasConversationSearchQuery)
                    RefreshFilteredConversations();
            }
            return true;
        }

        private async Task<CopilotGoalPostTurnResult> ProcessGoalAfterTurnAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            string boundGoalId,
            CopilotTokenUsage turnUsage)
        {
            if (!hostedRun.IsAgent || string.IsNullOrWhiteSpace(boundGoalId))
                return CopilotGoalPostTurnResult.Empty;

            var context = CopilotUiDispatcher.Invoke(
                () =>
                {
                    var goal = conversation.Goal;
                    if (goal?.IsActive != true
                        || !string.Equals(goal.Id, boundGoalId, StringComparison.Ordinal))
                    {
                        return null;
                    }

                    return new CopilotGoalEvaluationContext(
                        goal,
                        CopilotConversationRequestBuilder
                            .CaptureHistorySnapshot(conversation)
                            .VisibleMessages,
                        CopilotGoalTurnEvidence.Capture(assistantMessage));
                },
                fallback: null as CopilotGoalEvaluationContext);
            if (context == null)
                return CopilotGoalPostTurnResult.Empty;

            CopilotGoalEvaluationResult? evaluation = null;
            if (context.TurnEvidence.StopReason == CopilotAgentStopReason.Completed
                && !context.TurnEvidence.WasResponseInterrupted
                && userMessage.RequestMode is CopilotAgentMode.Auto or CopilotAgentMode.Code)
            {
                evaluation = await _goalCompletionEvaluator.EvaluateAsync(
                    requestProfile,
                    context.Goal,
                    context.Transcript,
                    context.TurnEvidence,
                    hostedRun.CancellationToken).ConfigureAwait(false);
            }

            var evaluationUsage = evaluation?.Usage ?? CopilotTokenUsage.Empty;
            var decision = CopilotGoalContinuationPolicy.Evaluate(
                context.Goal,
                userMessage.RequestMode,
                context.TurnEvidence.StopReason,
                context.TurnEvidence.WasResponseInterrupted,
                turnUsage.Add(evaluationUsage),
                evaluation,
                DateTimeOffset.UtcNow);
            var applied = CopilotUiDispatcher.Invoke(
                () =>
                {
                    if (conversation.Goal?.IsActive != true
                        || !string.Equals(conversation.Goal.Id, context.Goal.Id, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    conversation.Goal = decision.Goal;
                    CopilotAssistantMessagePresenter.AppendExecutionTrace(
                        assistantMessage,
                        "Goal "
                        + decision.Action.ToString().ToLowerInvariant()
                        + " · "
                        + CopilotAgentTraceEntry.Sanitize(decision.Reason));
                    return true;
                },
                fallback: false);
            if (!applied)
                return new CopilotGoalPostTurnResult(evaluationUsage, string.Empty, string.Empty, false);

            return new CopilotGoalPostTurnResult(
                evaluationUsage,
                decision.Goal.Id,
                decision.Reason,
                decision.Action == CopilotGoalTurnAction.QueueContinuation);
        }

        private bool TryQueueGoalContinuation(
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            string goalId,
            string reason)
        {
            var goal = conversation.Goal;
            if (goal?.IsActive != true
                || !string.Equals(goal.Id, goalId, StringComparison.Ordinal))
            {
                return false;
            }

            if (_queuedFollowUpsByRunId.Values.Any(item =>
                string.Equals(item.ConversationId, conversation.Id, StringComparison.Ordinal)))
            {
                return true;
            }

            var prompt =
                "继续处理当前持续目标。独立完成评估认为目标尚未达成："
                + CopilotConversationGoal.NormalizeReason(reason)
                + Environment.NewLine
                + "根据现有证据选择下一项最有价值的工作并验证结果；不要把持续目标当作工具、写入、审批复用或扩大范围的授权。";
            var requestProfileSnapshot = requestProfile.Clone();
            var submissionContext = CaptureHostedTurnSnapshot(
                conversation,
                attachmentOverride: conversation.Attachments);
            var itemReady = new TaskCompletionSource<CopilotQueuedFollowUp>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_taskHost.TryScheduleFollowUp(
                conversation.Id,
                CopilotAgentMode.Auto,
                async run =>
                {
                    var queuedItem = await itemReady.Task.ConfigureAwait(false);
                    await ExecuteQueuedFollowUpAsync(run, queuedItem).ConfigureAwait(false);
                },
                out var queuedRun,
                out var admission)
                || queuedRun == null)
            {
                var pauseReason = "无法排入下一轮持续目标任务："
                    + GetRequestAdmissionText(admission)
                    + "。目标已暂停，避免静默丢失续作。";
                conversation.Goal = goal.WithState(
                    CopilotConversationGoalState.Paused,
                    DateTimeOffset.UtcNow,
                    pauseReason);
                PersistState(immediate: true);
                return false;
            }

            var queuedFollowUp = new CopilotQueuedFollowUp(
                queuedRun.Id,
                conversation.Id,
                conversation.Title,
                prompt,
                CopilotAgentMode.Auto,
                requestProfileSnapshot,
                submissionContext,
                goalId);
            _queuedFollowUpsByRunId.Add(queuedRun.Id, queuedFollowUp);
            QueuedFollowUps.Add(queuedFollowUp);
            AddQueuedFollowUpRecovery(queuedFollowUp);
            itemReady.SetResult(queuedFollowUp);
            RefreshQueuedFollowUpPositions();
            PersistState(immediate: true);
            return true;
        }

        private void StartNewChat()
        {
            if (!CanSwitchConversation)
                return;
            if (IsEditingMessage)
                CancelMessageEdit();
            _pendingAgentRecoveryRequest = null;
            ClearPendingRequestModeOverride();

            if (CopilotConversationService.IsReusableEmpty(SelectedConversation))
                return;

            var conversation = ResolveNewConversationTarget();
            if (!ReferenceEquals(conversation, SelectedConversation))
            {
                SelectConversation(conversation, persist: false);
                PersistState();
            }
        }

        private void ClearConversationContext(CopilotLocalCommand command, string previousTitle)
        {
            if (IsBusy || !CanSwitchConversation)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再清空上下文。");
                return;
            }

            var normalizedTitle = previousTitle.Trim();
            if (normalizedTitle.Length > 0
                && (SelectedConversation == null
                    || !TryApplyConversationTitle(SelectedConversation, normalizedTitle)))
            {
                ShowLocalCommandResult(
                    command,
                    $"旧会话名称不能为空且不能超过 {CopilotConversationRecord.MaximumTitleCharacters:N0} 个字符。");
                return;
            }

            DismissLocalCommandResult();
            StartNewChat();
        }

        private void ResumeConversation(CopilotLocalCommand command, string query)
        {
            if (!CanSwitchConversation)
            {
                ShowLocalCommandResult(command, "当前状态不能切换会话；请先结束消息编辑或等待当前普通对话完成。");
                return;
            }

            var normalizedQuery = NormalizeConversationSearchText(query.Trim());
            var exactMatch = CopilotConversationService.FindUniqueResumeTarget(
                CopilotConversationArchiveService.GetActive(Conversations),
                normalizedQuery);
            if (exactMatch != null)
            {
                ConversationSearchText = string.Empty;
                DismissLocalCommandResult();
                SelectConversation(exactMatch, persist: true, preferredProfileId: exactMatch.ProfileId);
                return;
            }

            ConversationSearchText = normalizedQuery;
            RefreshFilteredConversations();
            DismissLocalCommandResult();
            ConversationSearchRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ArchiveCurrentConversation(CopilotLocalCommand command)
        {
            var conversation = SelectedConversation;
            if (conversation == null || conversation.IsArchived)
            {
                ShowLocalCommandResult(command, "当前没有可归档的活动会话。");
                return;
            }
            if (IsBusy || !CanSwitchConversation || HasExclusiveLocalOperation)
            {
                ShowLocalCommandResult(command, "当前会话仍有请求或本地操作正在执行，请完成或停止后再归档。");
                return;
            }
            var activeBackgroundCommands =
                CopilotBackgroundShellCommandRegistry.Shared.GetSnapshots(
                        conversation.Id)
                    .Count(snapshot => snapshot.IsActive);
            if (activeBackgroundCommands > 0)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前会话还有 {activeBackgroundCommands:N0} 条后台命令在运行；请先使用 /ps 查看并停止，再归档会话。进程树未改变。");
                return;
            }
            var retentionBlocker = GetConversationRetentionBlocker(conversation);
            if (retentionBlocker != CopilotConversationRetentionBlocker.None)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前会话{CopilotConversationRetentionPolicy.Describe(retentionBlocker)}；请先处理该状态，避免把待办隐藏。");
                return;
            }

            var archivedTitle = conversation.Title;
            AcknowledgeCompletionNotices(conversation.Id);
            conversation.IsArchived = true;
            conversation.Touch();
            conversation.RefreshSummary();
            var activeConversations = CopilotConversationArchiveService.GetActive(Conversations);
            var replacement = activeConversations.Count > 0
                ? activeConversations[0]
                : CreateConversation();
            SelectConversation(replacement, persist: false, preferredProfileId: replacement.ProfileId);
            RefreshCompactHistoryConversations();
            RefreshFilteredConversations();
            RefreshConversationBranchFamily();
            PersistState(immediate: true);
            ShowLocalCommandResult(
                command,
                $"已归档“{archivedTitle}”。内容仍保留，但已从常用会话列表和 /resume 中隐藏。\n\n"
                + "使用 /archived 查看，或 /unarchive <会话 ID 或唯一完整标题> 恢复。");
        }

        private void UnarchiveConversation(CopilotLocalCommand command, string query)
        {
            if (IsBusy || !CanSwitchConversation)
            {
                ShowLocalCommandResult(command, "当前状态不能恢复归档会话；请先结束消息编辑或等待当前请求完成。");
                return;
            }

            var normalizedQuery = (query ?? string.Empty).Trim();
            var conversation = CopilotConversationArchiveService.FindUniqueArchived(
                Conversations,
                normalizedQuery);
            if (conversation == null)
            {
                ShowLocalCommandResult(
                    command,
                    CopilotConversationArchiveService.FormatArchived(Conversations, normalizedQuery));
                return;
            }

            conversation.IsArchived = false;
            conversation.Touch();
            conversation.RefreshSummary();
            CopilotConversationService.MoveToPreferredIndex(Conversations, conversation);
            RefreshCompactHistoryConversations();
            RefreshFilteredConversations();
            SelectConversation(conversation, persist: false, preferredProfileId: conversation.ProfileId);
            PersistState(immediate: true);
            ShowLocalCommandResult(command, $"已恢复“{conversation.Title}”，会话内容和草稿均保持不变。");
        }

        private CopilotConversationRetentionBlocker GetConversationRetentionBlocker(
            CopilotConversationRecord conversation)
        {
            var conversationId = conversation.Id;
            return CopilotConversationRetentionPolicy.Evaluate(
                conversation,
                hasScheduledRun: _taskHost.FindRunByConversationId(conversationId) != null,
                hasPendingApproval: CopilotMcpConfirmationStore.Instance
                    .GetPendingActionsForConversation(conversationId)
                    .Count > 0,
                hasQueuedFollowUp: QueuedFollowUps.Any(item => string.Equals(
                    item.ConversationId,
                    conversationId,
                    StringComparison.Ordinal)),
                isEditingMessage: string.Equals(
                    _editingConversationId,
                    conversationId,
                    StringComparison.Ordinal));
        }

        private void RenameCurrentConversation(CopilotLocalCommand command, string requestedTitle)
        {
            var conversation = SelectedConversation;
            if (!CanRenameConversation(conversation))
            {
                ShowLocalCommandResult(command, "当前没有可重命名的会话。");
                return;
            }

            if (string.IsNullOrWhiteSpace(requestedTitle))
            {
                DismissLocalCommandResult();
                RenameConversation(conversation);
                return;
            }

            if (!TryApplyConversationTitle(conversation!, requestedTitle))
            {
                ShowLocalCommandResult(
                    command,
                    $"会话名称不能为空且不能超过 {CopilotConversationRecord.MaximumTitleCharacters:N0} 个字符。");
                return;
            }

            DismissLocalCommandResult();
        }

        private void CopyAssistantResponse(CopilotLocalCommand command, string requestedOrdinal)
        {
            if (!CopilotConversationService.TryParseAssistantResponseOrdinal(requestedOrdinal, out var ordinal))
            {
                ShowLocalCommandResult(command, "序号必须是大于 0 的整数，例如 /copy 或 /copy 2。");
                return;
            }

            var message = CopilotConversationService.FindNthLatestCompletedAssistantResponse(
                SelectedConversation,
                ordinal);
            if (message == null)
            {
                ShowLocalCommandResult(
                    command,
                    ordinal == 1
                        ? "当前会话还没有可复制的已完成回答。"
                        : $"当前会话没有倒数第 {ordinal:N0} 条可复制的已完成回答。");
                return;
            }

            var text = BuildMessageClipboardText(message);
            if (!TrySetClipboardText(text, out var errorMessage))
            {
                ShowLocalCommandResult(command, "复制失败：" + errorMessage);
                return;
            }

            ShowLocalCommandResult(
                command,
                ordinal == 1
                    ? $"已复制最近一条已完成回答（{text.Length:N0} 个字符）。"
                    : $"已复制倒数第 {ordinal:N0} 条已完成回答（{text.Length:N0} 个字符）。");
        }

        private void RetryLatestResponse(
            CopilotLocalCommand command,
            string arguments)
        {
            if (!CopilotResponseRetryCommand.TryParse(
                    arguments,
                    out var refreshExternalContext))
            {
                ShowLocalCommandResult(
                    command,
                    "参数只支持 refresh，例如 /retry 或 /retry refresh。");
                return;
            }

            var message = SelectedConversation?.Messages.LastOrDefault();
            if (message == null)
            {
                ShowLocalCommandResult(command, "当前会话还没有可重试的请求。");
                return;
            }
            if (SelectedProfile?.IsConfigured != true)
            {
                ShowLocalCommandResult(
                    command,
                    "当前模型 Profile 尚未完成配置；请先使用 /settings models。");
                return;
            }
            if (!CanRegenerateMessage(message))
            {
                if (TryResolveLatestTurn(
                        message,
                        out var conversation,
                        out _,
                        out var assistantMessage)
                    && assistantMessage != null
                    && CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                        conversation,
                        assistantMessage,
                        CreateConversationRequestProfile(SelectedProfile, conversation),
                        CopilotCapabilityCatalog.Shared.GetSnapshot()))
                {
                    ShowLocalCommandResult(
                        command,
                        "最后一轮保留了可安全继续的 Agent checkpoint；请优先使用 /tasks 继续或明确放弃恢复项，避免重新执行已完成的工具操作。");
                    return;
                }

                ShowLocalCommandResult(
                    command,
                    "最后一轮当前不能重试；请先结束运行或消息编辑，并确认它仍是当前会话的最后一轮。");
                return;
            }

            DismissLocalCommandResult();
            RunUiOperation(
                () => RetryMessageAsync(message, refreshExternalContext),
                refreshExternalContext
                    ? "刷新附件与网页后重新生成"
                    : message.RequestMode == CopilotAgentMode.Chat
                        ? "重新生成回复"
                        : "重新运行 Agent");
        }

        private void SelectModelProfile(CopilotLocalCommand command, string query)
        {
            if (!CanSelectProfile)
            {
                ShowLocalCommandResult(
                    command,
                    IsBusy
                        ? "当前有请求正在执行，请完成或停止后再切换模型 Profile。"
                        : "当前没有可选择的模型 Profile，请先在 Copilot 设置中添加并配置模型。");
                return;
            }

            var normalizedQuery = query.Trim();
            if (normalizedQuery.Length == 0)
            {
                DismissLocalCommandResult();
                ProfileSelectionRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            var profile = CopilotConversationService.FindUniqueProfileTarget(Profiles, normalizedQuery);
            if (profile == null)
            {
                ShowLocalCommandResult(
                    command,
                    $"未找到唯一匹配“{normalizedQuery}”的 Profile 名或模型 ID，请从模型列表中选择。");
                ProfileSelectionRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            SelectedProfile = profile;
            ShowLocalCommandResult(
                command,
                $"当前会话后续请求将使用：{profile.DisplayLabel}"
                + Environment.NewLine
                + $"模型：{(string.IsNullOrWhiteSpace(profile.Model) ? "未设置" : profile.Model)}"
                + Environment.NewLine
                + $"协议：{profile.ProviderLabel}"
                + Environment.NewLine
                + $"推理：{profile.ReasoningLabel}"
                + Environment.NewLine
                + $"状态：{profile.ConfigurationStatusText}");
        }

        private void SelectReasoningMode(CopilotLocalCommand command, string query)
        {
            if (!CanSelectProfile)
            {
                ShowLocalCommandResult(
                    command,
                    IsBusy
                        ? "当前有请求正在执行，请完成或停止后再调整推理强度。"
                        : "当前没有可选择的模型 Profile，请先在 Copilot 设置中添加并配置模型。");
                return;
            }

            var profile = SelectedProfile;
            if (profile == null)
            {
                ShowLocalCommandResult(command, "当前没有选中的模型 Profile。");
                return;
            }
            if (!HasConfigurableReasoning)
            {
                ShowLocalCommandResult(
                    command,
                    $"{profile.DisplayLabel} 未声明可配置的推理强度，将继续使用 Provider 默认值。");
                return;
            }

            var normalizedQuery = query.Trim();
            if (normalizedQuery.Length == 0)
            {
                DismissLocalCommandResult();
                ReasoningSelectionRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            var option = CopilotReasoningCapabilities.FindCommandOption(profile, normalizedQuery);
            if (option == null)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前 Profile 不支持推理级别“{normalizedQuery}”。"
                    + Environment.NewLine
                    + $"可用级别：{CopilotReasoningCapabilities.GetCommandOptionSummary(profile)}");
                return;
            }

            var previousMode = CopilotReasoningCapabilities.GetEffectiveMode(profile);
            SetSelectedProfileReasoningMode(option.Mode);
            var changeLabel = previousMode == option.Mode ? "保持" : "已设置";
            ShowLocalCommandResult(
                command,
                $"{profile.DisplayLabel} · 推理强度{changeLabel}为“{option.Label}”。"
                + Environment.NewLine
                + option.Description
                + Environment.NewLine
                + "该设置保存到当前模型 Profile，并用于后续请求。");
        }

        private void SelectResponsePersonality(CopilotLocalCommand command, string query)
        {
            var conversation = EnsureConversation();
            var normalizedQuery = query.Trim();
            if (normalizedQuery.Length == 0)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前会话风格：{CopilotResponsePersonalitySelection.GetDisplayName(conversation.ResponsePersonality)}"
                    + Environment.NewLine
                    + "可用风格：friendly、pragmatic、none。");
                return;
            }
            if (!CopilotResponsePersonalitySelection.TryParse(normalizedQuery, out var personality))
            {
                ShowLocalCommandResult(
                    command,
                    $"不支持会话风格“{normalizedQuery}”。"
                    + Environment.NewLine
                    + "可用风格：friendly、pragmatic、none。");
                return;
            }

            var previousPersonality = conversation.ResponsePersonality;
            conversation.ResponsePersonality = personality;
            conversation.Touch();
            PersistState(immediate: true);
            var changeLabel = previousPersonality == personality ? "保持" : "已设置";
            var checkpointNote = conversation.AgentSessionCheckpoint == null || previousPersonality == personality
                ? string.Empty
                : Environment.NewLine + "已有 Agent checkpoint 会保留；继续任务时将按新风格重新规划，不会直接复用旧提示身份。";
            ShowLocalCommandResult(
                command,
                $"当前会话风格{changeLabel}为“{CopilotResponsePersonalitySelection.GetDisplayName(personality)}”（{CopilotResponsePersonalitySelection.GetCommandToken(personality)}）。"
                + Environment.NewLine
                + "它只影响后续回答的默认表达，不改变任务范围、权限、安全规则、证据要求或用户明确指定的格式。"
                + checkpointNote);
        }

        private CopilotConversationRecord ResolveNewConversationTarget()
        {
            var profile = SelectedProfile ?? ResolveProfile(_state.ActiveProfileId) ?? _config.GetPreferredDefaultProfile();
            return CopilotConversationService.ResolveNewTarget(Conversations, SelectedConversation, profile);
        }

        private bool CanContinueAgentTasks(CopilotChatMessage? message)
        {
            if (IsEditingMessage || !CanScheduleComposerRequest(CopilotAgentMode.Auto) || message == null || message.IsUser || !message.HasRecoverableAgentTasks)
                return false;
            if (SelectedConversation?.AgentSessionCheckpoint == null || SelectedProfile?.IsConfigured != true)
                return false;

            var latestAssistant = SelectedConversation.Messages.LastOrDefault(candidate => !candidate.IsUser);
            if (!ReferenceEquals(latestAssistant, message))
                return false;

            return CopilotAgentRecoveryPolicy.Evaluate(
                message,
                SelectedConversation.AgentSessionCheckpoint,
                CreateConversationRequestProfile(SelectedProfile, SelectedConversation),
                CopilotCapabilityCatalog.Shared.GetSnapshot(),
                CopilotToolExecutor.GetSharedHookSurfaceSnapshot()).IsAvailable;
        }

        private void ContinueAgentTasks(CopilotChatMessage? message)
        {
            TryContinueAgentTasks(message);
        }

        private bool TryContinueAgentTasks(CopilotChatMessage? message)
        {
            if (!CanContinueAgentTasks(message))
                return false;

            var conversation = SelectedConversation!;
            var profile = SelectedProfile!;
            var decision = CopilotAgentRecoveryPolicy.Evaluate(
                message,
                conversation.AgentSessionCheckpoint,
                CreateConversationRequestProfile(profile, conversation),
                CopilotCapabilityCatalog.Shared.GetSnapshot(),
                CopilotToolExecutor.GetSharedHookSurfaceSnapshot());
            if (!decision.IsAvailable)
                return false;

            _pendingAgentRecoveryRequest = decision.Request;
            SetPendingRequestModeOverride(CopilotAgentMode.Auto);
            InputText = decision.UserMessage;
            RunUiOperation(SendAsync, "继续 Agent 任务");
            return true;
        }

        private bool CanExecuteApprovedPlan(CopilotChatMessage? message)
        {
            return CanUseCompletedPlan(message, CopilotAgentMode.Auto)
                && CopilotPlanHandoff.TryCreateExecutionRequest(message, out _);
        }

        private void ExecuteApprovedPlan(CopilotChatMessage? message)
        {
            if (!CanExecuteApprovedPlan(message)
                || !CopilotPlanHandoff.TryCreateExecutionRequest(message, out var request))
            {
                return;
            }

            RunUiOperation(
                () => SendAsync(request.VisiblePrompt, CopilotAgentMode.Auto, request.ModelPrompt),
                "执行批准的计划");
        }

        private bool CanContinuePlanning(CopilotChatMessage? message)
        {
            return CanUseCompletedPlan(message, CopilotAgentMode.Plan);
        }

        private void ContinuePlanning(CopilotChatMessage? message)
        {
            if (!CanContinuePlanning(message))
                return;

            SetPendingRequestModeOverride(CopilotAgentMode.Plan);
            if (IsInputEmpty)
                InputText = CopilotPlanHandoff.ContinuePlanningPrompt;
        }

        private bool CanUseCompletedPlan(CopilotChatMessage? message, CopilotAgentMode nextMode)
        {
            if (IsEditingMessage
                || SelectedProfile?.IsConfigured != true
                || !CanScheduleComposerRequest(nextMode)
                || message?.HasCompletedPlan != true
                || SelectedConversation == null)
            {
                return false;
            }

            var latestAssistant = SelectedConversation.Messages.LastOrDefault(candidate => !candidate.IsUser);
            return ReferenceEquals(latestAssistant, message);
        }

        private bool CanRequestWorkspaceRollback(CopilotAgentTraceEntry? trace)
        {
            return trace?.CanRequestWorkspaceRollback == true
                && !IsBusy
                && !IsEditingMessage
                && SelectedConversation?.Messages.Any(message => message.AgentTraceEntries.Contains(trace)) == true
                && !HasActiveWorkspaceRollback(trace.WorkspaceChangeSetId);
        }

        private void RequestWorkspaceRollback(CopilotAgentTraceEntry? trace)
        {
            if (trace?.CanRequestWorkspaceRollback != true)
            {
                LocalCommandResultTitle = "无法撤销文件修改";
                LocalCommandResultText = "这次修改的安全回滚记录已失效、已被使用，或与当前会话及工作区状态不再匹配。";
                return;
            }

            RunUiOperation(
                () => RequestWorkspaceRollbackAsync(trace),
                "撤销文件修改");
        }

        private void RollbackWorkspaceFromCommand(
            CopilotLocalCommand command,
            string requestedOrdinal)
        {
            var conversation = SelectedConversation;
            if (conversation == null || IsBusy || IsEditingMessage)
            {
                ShowLocalCommandResult(command, "当前状态不能撤销文件修改；请先结束正在运行的请求或消息编辑。");
                return;
            }

            if (string.IsNullOrWhiteSpace(requestedOrdinal))
            {
                ShowLocalCommandResult(command, CopilotWorkspaceRollbackPointService.Format(conversation));
                return;
            }
            if (!CopilotWorkspaceRollbackPointService.TryResolve(
                    conversation,
                    requestedOrdinal,
                    out var point))
            {
                ShowLocalCommandResult(
                    command,
                    "回滚序号必须对应一组仍有效的精确文件修改，例如 /rollback 1。输入 /rollback 可查看可用回滚点。");
                return;
            }
            if (!CanRequestWorkspaceRollback(point.Trace))
            {
                ShowLocalCommandResult(
                    command,
                    "这组文件修改正在回滚，或其安全回滚记录刚刚失效；未创建重复请求。");
                return;
            }

            RequestWorkspaceRollback(point.Trace);
        }

        private async Task RequestWorkspaceRollbackAsync(CopilotAgentTraceEntry trace)
        {
            var conversation = SelectedConversation;
            var assistantMessage = conversation?.Messages.FirstOrDefault(message =>
                message.AgentTraceEntries.Contains(trace));
            if (conversation == null || assistantMessage == null)
            {
                LocalCommandResultTitle = "无法撤销文件修改";
                LocalCommandResultText = "这条修改记录不属于当前会话，未创建回滚请求。";
                return;
            }

            var workspacePath = CaptureHostedTurnSnapshot(conversation.Attachments).SolutionDirectoryPath;
            var result = await _turnRuntime.RequestWorkspaceRollbackAsync(
                new CopilotWorkspaceRollbackActionRequest(
                    conversation.Id,
                    workspacePath,
                    trace.WorkspaceChangeSetId),
                agentEvent => ApplyDirectWorkspaceRollbackEvent(
                    conversation,
                    assistantMessage,
                    trace.WorkspaceChangeSetId,
                    agentEvent),
                CancellationToken.None);
            if (!result.Success || result.Action == null)
            {
                LocalCommandResultTitle = "无法撤销文件修改";
                LocalCommandResultText = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "安全回滚请求未能创建。"
                    : result.ErrorMessage;
                return;
            }

            SetPendingActionFeedback("已创建精确绑定的工作区回滚审批；无需再次调用模型。");
            await ApprovePendingActionAsync(result.Action);
        }

        private void ApplyDirectWorkspaceRollbackEvent(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            string changeSetId,
            CopilotAgentEvent agentEvent)
        {
            CopilotUiDispatcher.Invoke(() =>
            {
                var presentationResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(
                    assistantMessage,
                    agentEvent);
                var rolledBack = agentEvent.Type == CopilotAgentEventType.ToolResult
                    && agentEvent.ToolResult?.Success == true
                    && string.Equals(
                        agentEvent.ToolExecution?.ToolName,
                        "RollbackWorkspacePatchEnvelope",
                        StringComparison.Ordinal)
                    && conversation.MarkWorkspaceChangeSetRolledBack(changeSetId);
                if (rolledBack
                    || presentationResult.PersistenceMode != CopilotAgentEventPersistenceMode.None)
                {
                    PersistState(immediate: rolledBack);
                }
                CommandManager.InvalidateRequerySuggested();
            });
        }

        private bool HasActiveWorkspaceRollback(string changeSetId)
        {
            if (string.IsNullOrWhiteSpace(changeSetId) || SelectedConversation == null)
                return false;

            return SelectedConversation.Messages
                .SelectMany(message => message.AgentTraceEntries)
                .Any(entry =>
                    string.Equals(
                        entry.ToolName,
                        "RollbackWorkspacePatchEnvelope",
                        StringComparison.Ordinal)
                    && string.Equals(
                        entry.WorkspaceChangeSetId,
                        changeSetId,
                        StringComparison.Ordinal)
                    && entry.State is CopilotToolExecutionState.Pending
                        or CopilotToolExecutionState.Running
                        or CopilotToolExecutionState.AwaitingApproval);
        }

        private static bool CanOpenWorkspaceChangeFile(CopilotWorkspaceChangeFile? file)
        {
            return file != null && CopilotLocalFileLinkNavigator.TryResolve(file.FilePath, out _);
        }

        private void OpenWorkspaceChangeFile(CopilotWorkspaceChangeFile? file)
        {
            var errorMessage = string.Empty;
            if (file != null
                && CopilotLocalFileLinkNavigator.TryResolve(file.FilePath, out var target)
                && CopilotLocalFileLinkNavigator.TryOpen(target, out errorMessage))
            {
                return;
            }

            LocalCommandResultTitle = "无法打开修改文件";
            LocalCommandResultText = string.IsNullOrWhiteSpace(errorMessage)
                ? "文件已不存在或不在当前工作区内。"
                : CopilotUserFacingErrorFormatter.Sanitize(errorMessage);
        }

        private void OpenAgentTask(CopilotAgentTaskSummary? task)
        {
            if (task == null || !CanSwitchConversation || !Conversations.Contains(task.Conversation))
                return;

            SelectConversation(task.Conversation, persist: true, preferredProfileId: task.Conversation.ProfileId);
        }

        private void ToggleAgentTaskPanel()
        {
            if (!HasAgentTasks)
                return;

            _state.ToggleAgentTaskPanelExpanded();
            OnPropertyChanged(nameof(IsAgentTaskPanelExpanded));
            OnPropertyChanged(nameof(IsAgentTaskListVisible));
            OnPropertyChanged(nameof(AgentTaskPanelToggleGlyph));
            OnPropertyChanged(nameof(AgentTaskPanelToolTip));
            PersistState();
        }

        private void ChangeMessageTimestampVisibility(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotMessageTimestampPreference.TryResolve(
                    arguments,
                    ShowMessageTimestamps,
                    out var show))
            {
                ShowLocalCommandResult(command, CopilotMessageTimestampPreference.Usage);
                return;
            }

            if (_state.SetShowMessageTimestamps(show))
            {
                OnPropertyChanged(nameof(ShowMessageTimestamps));
                PersistState(immediate: true);
            }

            ShowLocalCommandResult(
                command,
                $"消息时间戳已{(show ? "显示" : "隐藏")}。\n\n"
                + "该偏好只改变本地界面，不修改聊天内容，也不调用模型或工具。");
        }

        private void ChangePromptSuggestionPreference(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotPromptSuggestionPreference.TryResolve(
                    arguments,
                    PromptHistoryCompletionsEnabled,
                    out var enabled))
            {
                ShowLocalCommandResult(command, CopilotPromptSuggestionPreference.Usage);
                return;
            }

            if (_state.SetEnablePromptHistoryCompletions(enabled))
            {
                OnPropertyChanged(nameof(PromptHistoryCompletionsEnabled));
                NotifyPromptHistoryPrefixCompletionChanged();
                PersistState(immediate: true);
            }

            ShowLocalCommandResult(
                command,
                $"本地历史提示补全已{(enabled ? "开启" : "关闭")}。\n\n"
                + "该偏好只控制当前设备上的输入提示；不会调用模型，不会修改或删除历史消息。");
        }

        private void ChangeCompactMessageLayout(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotCompactMessageLayout.TryResolvePreference(
                    arguments,
                    UseCompactMessageLayout,
                    out var useCompactLayout))
            {
                ShowLocalCommandResult(command, CopilotCompactMessageLayout.Usage);
                return;
            }

            if (_state.SetUseCompactMessageLayout(useCompactLayout))
            {
                OnPropertyChanged(nameof(UseCompactMessageLayout));
                OnPropertyChanged(nameof(MessageListPadding));
                OnPropertyChanged(nameof(MessageItemMargin));
                OnPropertyChanged(nameof(UserMessagePadding));
                OnPropertyChanged(nameof(AssistantActionsMargin));
                PersistState(immediate: true);
            }

            ShowLocalCommandResult(
                command,
                $"消息布局已切换为{(useCompactLayout ? "紧凑" : "标准")}间距。\n\n"
                + "该偏好只改变本地消息密度；不会压缩会话上下文，也不调用模型或工具。");
        }

        private void ChangeMultilineComposerPreference(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotMultilineComposerPreference.TryResolve(
                    arguments,
                    UseMultilineComposer,
                    out var enabled))
            {
                ShowLocalCommandResult(command, CopilotMultilineComposerPreference.Usage);
                return;
            }

            if (_state.SetUseMultilineComposer(enabled))
            {
                OnPropertyChanged(nameof(UseMultilineComposer));
                OnPropertyChanged(nameof(ComposerInputToolTip));
                OnPropertyChanged(nameof(InputPlaceholder));
                OnPropertyChanged(nameof(SteerActionToolTip));
                OnPropertyChanged(nameof(QueueFollowUpToolTip));
                OnPropertyChanged(nameof(FollowUpQueueHintText));
                PersistState(immediate: true);
            }

            ShowLocalCommandResult(
                command,
                enabled
                    ? "多行输入模式已开启：Enter 插入换行，Shift+Enter 发送；Ctrl+Enter 空闲时发送、Agent 运行中立即接管。\n\n该偏好只改变当前设备的输入按键，不修改消息、模型或权限。"
                    : "多行输入模式已关闭：Enter 发送，Shift+Enter 插入换行；Ctrl+Enter 空闲时发送、Agent 运行中立即接管。\n\n该偏好只改变当前设备的输入按键，不修改消息、模型或权限。");
        }

        private void ChangeFollowUpBehavior(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotFollowUpPreference.TryResolve(
                    arguments,
                    DefaultFollowUpBehavior,
                    out var behavior))
            {
                ShowLocalCommandResult(command, CopilotFollowUpPreference.Usage);
                return;
            }

            if (_state.SetDefaultFollowUpBehavior(behavior))
            {
                OnPropertyChanged(nameof(DefaultFollowUpBehavior));
                OnPropertyChanged(nameof(InputPlaceholder));
                OnPropertyChanged(nameof(SteerActionToolTip));
                OnPropertyChanged(nameof(QueueFollowUpToolTip));
                OnPropertyChanged(nameof(FollowUpQueueHintText));
                PersistState(immediate: true);
            }

            var primaryAction = behavior == CopilotFollowUpBehavior.Queue
                ? "排到当前任务完成后的下一轮"
                : "加入当前 Agent 运行并调整方向";
            var alternateAction = behavior == CopilotFollowUpBehavior.Queue
                ? "调整当前 Agent 运行"
                : "排到当前任务完成后的下一轮";
            ShowLocalCommandResult(
                command,
                $"运行期间的默认后续行为已设为：{ComposerSubmitShortcutLabel} {primaryAction}；Tab {alternateAction}。\n\n"
                + "该偏好只影响普通后续消息；澄清问题答案和本地 / 命令仍按各自语义执行。");
        }

        private bool CanResumeAgentTask(CopilotAgentTaskSummary? task)
        {
            if (task?.CanResume != true
                || !Conversations.Contains(task.Conversation)
                || !CanScheduleConversationRequest(task.Conversation.Id, CopilotAgentMode.Auto))
                return false;

            var profile = ResolveProfile(task.Conversation.ProfileId);
            return profile?.IsConfigured == true && CopilotAgentRecoveryPolicy.Evaluate(
                task.Message,
                task.Conversation.AgentSessionCheckpoint,
                CreateConversationRequestProfile(profile, task.Conversation),
                CopilotCapabilityCatalog.Shared.GetSnapshot(),
                CopilotToolExecutor.GetSharedHookSurfaceSnapshot()).IsAvailable;
        }

        private void ResumeAgentTask(CopilotAgentTaskSummary? task)
        {
            TryResumeAgentTask(task);
        }

        private bool TryResumeAgentTask(CopilotAgentTaskSummary? task)
        {
            if (!CanResumeAgentTask(task) || task == null)
                return false;

            SelectConversation(task.Conversation, persist: true, preferredProfileId: task.Conversation.ProfileId);
            if (!ReferenceEquals(SelectedConversation, task.Conversation))
                return false;

            return TryContinueAgentTasks(task.Message);
        }

        private void DismissAgentTask(CopilotAgentTaskSummary? task)
        {
            TryDismissAgentTask(task);
        }

        private bool TryDismissAgentTask(CopilotAgentTaskSummary? task)
        {
            if (task == null || IsBusy || !Conversations.Contains(task.Conversation))
                return false;

            if (MessageBox.Show(
                Application.Current.GetActiveWindow(),
                task.DismissConfirmationText,
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return false;
            }

            if (!CopilotAgentTaskIndex.Dismiss(task))
                return false;
            if (ReferenceEquals(task.Conversation, SelectedConversation))
                PublishSelectedTaskEventJournal();
            PersistState();
            RefreshAgentTasks();
            return true;
        }

        private CopilotAgentRecoveryRequest? ConsumePendingAgentRecoveryRequest()
        {
            var recovery = _pendingAgentRecoveryRequest;
            _pendingAgentRecoveryRequest = null;
            return recovery;
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
                _stateSaveScheduler.RequestSave();
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
                _stateSaveScheduler.RequestSave();
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
            var attachmentSnapshot = attachments.Where(attachment => attachment != null).ToArray();
            if (attachmentSnapshot.Length > MaximumComposerAttachments)
            {
                LocalCommandResultTitle = "附件过多";
                LocalCommandResultText = $"当前请求包含 {attachmentSnapshot.Length:N0} 个附件，最多支持 {MaximumComposerAttachments:N0} 个。请移除多余附件后重试。";
                return false;
            }

            var imageCount = attachmentSnapshot.Count(attachment => attachment.Type == CopilotAttachmentType.Image);
            if (imageCount > CopilotImagePayloadLoader.MaximumImages)
            {
                LocalCommandResultTitle = "图片过多";
                LocalCommandResultText = $"当前请求包含 {imageCount:N0} 张图片，模型输入一次最多支持 {CopilotImagePayloadLoader.MaximumImages:N0} 张。请移除多余图片后重试。";
                return false;
            }

            return true;
        }

        private bool TryEnsureAttachmentCapacity(CopilotConversationRecord conversation, CopilotAttachmentType attachmentType)
        {
            if (attachmentType == CopilotAttachmentType.Image
                && conversation.Attachments.Count(attachment => attachment.Type == CopilotAttachmentType.Image) >= CopilotImagePayloadLoader.MaximumImages)
            {
                LocalCommandResultTitle = "图片已达到上限";
                LocalCommandResultText = $"每条请求最多附加 {CopilotImagePayloadLoader.MaximumImages:N0} 张图片。请先移除一张图片再继续添加。";
                return false;
            }

            if (conversation.Attachments.Count >= MaximumComposerAttachments)
            {
                LocalCommandResultTitle = "附件已达到上限";
                LocalCommandResultText = $"每条请求最多附加 {MaximumComposerAttachments:N0} 个文件、图片、网页或上下文。请先移除一个附件再继续添加。";
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
            LocalCommandResultText = $"本次已添加 {addedCount:N0} 个文件。每条请求最多支持 {MaximumComposerAttachments:N0} 个附件，其中图片最多 {CopilotImagePayloadLoader.MaximumImages:N0} 张；超出上限的文件未添加。当前共有 {conversation.Attachments.Count:N0} 个附件。";
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

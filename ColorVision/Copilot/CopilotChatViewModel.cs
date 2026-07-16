#pragma warning disable CA1001,CA1822,CA1859,CA1861,CA1870,CS4014
using ColorVision.Solution;
using ColorVision.Solution.Workspace;
using ColorVision.Copilot.Mcp;
using ColorVision.Common.MVVM;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
    public class CopilotChatViewModel : ViewModelBase, IDisposable
    {
        private const int CompactHistoryLimit = 4;
        private const int CompactSummaryOutputTokens = 4096;
        private static readonly TimeSpan RecentMcpFailureWindow = TimeSpan.FromMinutes(15);
        private const string CompactSystemPrompt = "You compact an existing conversation for seamless continuation. Preserve the user's active goal, constraints, decisions, verified facts, relevant files, commands and results, unfinished work, blockers, and safe next steps. Remove greetings, repetition, obsolete exploration, and verbose tool traces. Never invent facts or treat historical actions as current authorization. Return only a concise Markdown continuation summary.";

        private readonly CopilotChatService _chatService;
        private readonly CopilotConversationRequestBuilder _conversationRequestBuilder;
        private readonly CopilotAgentContextBuilder _agentContextBuilder;
        private readonly CopilotMicrosoftAgentFrameworkRuntime _agentRuntime;
        private readonly CopilotAgentTaskHost _taskHost;
        private readonly CopilotLocalGitDiffService _localGitDiffService;
        private readonly CopilotContextRegistry _contextRegistry;
        private readonly CopilotConfig _config;
        private readonly ICopilotChatStateStore _stateStore;
        private readonly CopilotChatStateSaveScheduler _stateSaveScheduler;
        private readonly ObservableCollection<CopilotChatMessage> _emptyMessages = new();
        private readonly ObservableCollection<CopilotAttachmentItem> _emptyAttachments = new();
        private readonly ObservableCollection<ConfirmableAction> _pendingActions = new();
        private readonly DispatcherTimer _pendingActionExpiryTimer;
        private CancellationTokenSource? _pendingActionFeedbackCts;
        private CancellationTokenSource? _compactConversationCts;
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
        private string _statePersistenceNoticeText = string.Empty;
        private string _statePersistenceNoticeToolTip = string.Empty;
        private string _localCommandResultTitle = string.Empty;
        private string _localCommandResultText = string.Empty;
        private bool _hasPendingMcpActions;
        private bool _hasRecentMcpFailures;
        private bool _isInspectingGitDiff;
        private bool _isCompactingConversation;
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
            _conversationRequestBuilder = new CopilotConversationRequestBuilder();
            _agentContextBuilder = new CopilotAgentContextBuilder();
            var toolRegistry = CopilotToolRegistry.CreateDefault();
            var toolExecutor = new CopilotToolExecutor();
            _agentRuntime = new CopilotMicrosoftAgentFrameworkRuntime(toolRegistry, _agentContextBuilder, toolExecutor);
            _taskHost = CopilotAgentTaskHost.Shared;
            _localGitDiffService = new CopilotLocalGitDiffService();
            _contextRegistry = CopilotContextRegistry.CreateDefault();
            _config = CopilotConfig.Instance;
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _stateSaveScheduler = new CopilotChatStateSaveScheduler(
                SaveStateSnapshotAsync,
                onError: ReportStatePersistenceError,
                onSaved: ReportStatePersistenceSuccess);
            _currentLiveContext = CopilotLiveContextRegistry.Current;

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
            NewChatCommand = new RelayCommand(_ => StartNewChat());
            SelectConversationCommand = new RelayCommand<CopilotConversationRecord>(
                conversation => SelectConversation(conversation, persist: true),
                conversation => CanSwitchConversation && conversation != null);
            CancelCommand = new RelayCommand(_ => CancelActiveRun(), _ => IsBusy);
            PrimaryActionCommand = new RelayCommand(_ => ExecutePrimaryAction());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings(), _ => !IsBusy);
            AddFileAttachmentCommand = new RelayCommand(_ => AddFileAttachment(), _ => !IsBusy);
            AddContextAttachmentCommand = new RelayCommand(_ => AddContextAttachment(), _ => !IsBusy);
            AddWebPageAttachmentCommand = new RelayCommand(_ => RunUiOperation(AddWebPageAttachmentAsync, "附加网页"), _ => !IsBusy);
            PasteImageAttachmentCommand = new RelayCommand(_ => PasteImageAttachment(), _ => !IsBusy);
            AttachCurrentLiveContextCommand = new RelayCommand(_ => AttachCurrentLiveContext(), _ => HasCurrentLiveContext);
            CopyMessageCommand = new RelayCommand<CopilotChatMessage>(CopyMessage, message => message != null);
            RetryMessageCommand = new RelayCommand<CopilotChatMessage>(message => RunUiOperation(() => RetryMessageAsync(message, refreshWebContext: false), "重新生成回复"), CanRegenerateMessage);
            RefreshMessageCommand = new RelayCommand<CopilotChatMessage>(message => RunUiOperation(() => RetryMessageAsync(message, refreshWebContext: true), "刷新网页后重新生成"), CanRegenerateMessage);
            ContinueAgentTasksCommand = new RelayCommand<CopilotChatMessage>(ContinueAgentTasks, CanContinueAgentTasks);
            OpenAgentTaskCommand = new RelayCommand<CopilotAgentTaskSummary>(OpenAgentTask, task => task != null && CanSwitchConversation);
            ResumeAgentTaskCommand = new RelayCommand<CopilotAgentTaskSummary>(ResumeAgentTask, CanResumeAgentTask);
            DismissAgentTaskCommand = new RelayCommand<CopilotAgentTaskSummary>(DismissAgentTask, task => task != null && !IsBusy);
            OpenAgentRunNoticeCommand = new RelayCommand(_ => OpenAgentRunNotice(), _ => HasAgentRunNotice);
            SteerCommand = new RelayCommand(_ => TrySteerCurrentRun(), _ => CanSteerCurrentRun);
            RemoveAttachmentCommand = new RelayCommand<CopilotAttachmentItem>(RemoveAttachment, attachment => !IsBusy && attachment != null);
            RenameConversationCommand = new RelayCommand<CopilotConversationRecord>(RenameConversation, conversation => !IsBusy && conversation != null);
            DeleteConversationCommand = new RelayCommand<CopilotConversationRecord>(DeleteConversation, conversation => !IsBusy && conversation != null);
            TogglePinConversationCommand = new RelayCommand<CopilotConversationRecord>(TogglePinConversation, conversation => !IsBusy && conversation != null);
            CopyPendingActionIdCommand = new RelayCommand<ConfirmableAction>(CopyPendingActionId, action => action != null);
            CopyPendingActionPayloadCommand = new RelayCommand<ConfirmableAction>(CopyPendingActionPayload, action => action != null);
            ApprovePendingActionCommand = new RelayCommand<ConfirmableAction>(action => RunUiOperation(
                () => ApprovePendingActionAsync(action),
                "执行已批准操作",
                message => SetPendingActionFeedback("执行失败：" + message)), action => action?.IsPending == true);
            RejectPendingActionCommand = new RelayCommand<ConfirmableAction>(RejectPendingAction, action => action?.IsPending == true);
            DismissLocalCommandResultCommand = new RelayCommand(_ => DismissLocalCommandResult(), _ => HasLocalCommandResult);
            CompleteLocalCommandCommand = new RelayCommand(command => TryCompleteLocalCommand(command as CopilotLocalCommand), _ => HasLocalCommandSuggestions);

            _pendingActionExpiryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5),
            };
            _pendingActionExpiryTimer.Tick += (_, _) => RefreshPendingActions();
            _pendingActionExpiryTimer.Start();

            RefreshPendingActions();
            RefreshComposerTokenEstimate();
            RefreshCompactHistoryConversations();
            RefreshAgentTasks();
            IsBusy = _taskHost.IsActive;
            NotifyHostedRunStateChanged();
        }

        public ObservableCollection<CopilotConversationRecord> Conversations => _state.Conversations;

        public ObservableCollection<CopilotConversationRecord> CompactHistoryConversations { get; } = new();

        public ObservableCollection<CopilotAgentTaskSummary> AgentTasks { get; } = new();

        public bool HasAgentTasks => AgentTasks.Count > 0;

        public string AgentTaskCountLabel => AgentTasks.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);

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
                    return "Action review";

                return count == 1
                    ? "Review 1 protected action"
                    : $"Review {count} protected actions";
            }
        }

        public string PendingActionPanelSummary
        {
            get
            {
                if (_pendingActions.Count == 0)
                    return "No protected actions are waiting for approval.";

                var nextDeadline = _pendingActions
                    .OrderBy(action => action.ExpiresAt)
                    .FirstOrDefault()?.ReviewDeadlineLabel ?? string.Empty;

                var actionBehavior = _pendingActions.Any(action => action.ResumesAgentOnApproval)
                    ? "Protected Agent Framework calls resume in the same session after approval."
                    : _pendingActions.Any(action => action.ExecuteOnApproval)
                        ? "In-app template actions apply to the editor immediately after approval; you still decide when to save."
                        : "External MCP actions still require confirm_action after approval.";
                return string.IsNullOrWhiteSpace(nextDeadline) ? actionBehavior : $"{actionBehavior} Next {nextDeadline}.";
            }
        }

        public string PendingActionPanelToolTip
        {
            get
            {
                if (_pendingActions.Count == 0)
                    return PendingActionPanelSummary;

                return string.Join(Environment.NewLine, _pendingActions.Select(action =>
                    $"{action.Title} | tool={action.ToolName} | risk={action.RiskLevel} | deadline={action.ReviewDeadlineLabel}"));
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

        public ICommand SelectConversationCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand PrimaryActionCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand AddFileAttachmentCommand { get; }

        public ICommand AddContextAttachmentCommand { get; }

        public ICommand AddWebPageAttachmentCommand { get; }

        public ICommand PasteImageAttachmentCommand { get; }

        public ICommand AttachCurrentLiveContextCommand { get; }

        public string AttachmentMenuToolTip => IsBusy
            ? "Attachments are locked while the assistant is responding."
            : "Attachments apply to the next message only: paste an image, add a web page, add a file, or add context text.";

        public ICommand CopyMessageCommand { get; }

        public ICommand RetryMessageCommand { get; }

        public ICommand RefreshMessageCommand { get; }

        public ICommand ContinueAgentTasksCommand { get; }

        public ICommand OpenAgentTaskCommand { get; }

        public ICommand ResumeAgentTaskCommand { get; }

        public ICommand DismissAgentTaskCommand { get; }

        public ICommand OpenAgentRunNoticeCommand { get; }

        public ICommand SteerCommand { get; }

        public ICommand RemoveAttachmentCommand { get; }

        public ICommand RenameConversationCommand { get; }

        public ICommand DeleteConversationCommand { get; }

        public ICommand TogglePinConversationCommand { get; }

        public ICommand CopyPendingActionIdCommand { get; }

        public ICommand CopyPendingActionPayloadCommand { get; }

        public ICommand ApprovePendingActionCommand { get; }

        public ICommand RejectPendingActionCommand { get; }

        public ICommand DismissLocalCommandResultCommand { get; }

        public ICommand CompleteLocalCommandCommand { get; }

        public bool IsConversationEmpty => Messages.Count == 0;

        public bool HasAttachments => Attachments.Count > 0;

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

        public string CurrentLiveContextTitle => _currentLiveContext?.Title ?? string.Empty;

        public string CurrentLiveContextSummary => _currentLiveContext?.Summary ?? string.Empty;

        public bool CanAttachCurrentLiveContext => _currentLiveContext != null;

        public bool IsCurrentLiveContextAttached => _currentLiveContext != null
            && SelectedConversation?.Attachments.Any(item => item.Type == CopilotAttachmentType.Context
                && string.Equals(item.Source, _currentLiveContext.SourceId, StringComparison.Ordinal)) == true;

        public string CurrentLiveContextStatusText => IsCurrentLiveContextAttached ? "Attached" : "Available";

        public string CurrentLiveContextToolTip
        {
            get
            {
                if (_currentLiveContext == null)
                    return string.Empty;

                var builder = new StringBuilder();
                builder.AppendLine(Properties.Resources.CopilotCurrentWindowContext);

                if (!string.IsNullOrWhiteSpace(_currentLiveContext.Title))
                    builder.AppendLine(_currentLiveContext.Title.Trim());

                if (!string.IsNullOrWhiteSpace(_currentLiveContext.Summary))
                {
                    builder.AppendLine();
                    builder.AppendLine(_currentLiveContext.Summary.Trim());
                }

                builder.AppendLine();
                builder.Append(IsCurrentLiveContextAttached ? "Already attached to this conversation." : "Ready to attach to this question.");
                return builder.ToString();
            }
        }

        public string CurrentLiveContextActionText => IsCurrentLiveContextAttached ? Properties.Resources.CopilotUpdateSnapshot : Properties.Resources.CopilotAttachToQuestion;

        public string EmptyStateText => _config.IsConfigured
            ? Properties.Resources.CopilotSelectHistoryOrNew
            : Properties.Resources.CopilotConfigureModelFirst;

        public string PrimaryActionGlyph => _isCompactingConversation ? "■" : IsViewingQueuedRun ? "×" : IsViewingActiveRun ? (CanPauseAgentRun ? "Ⅱ" : "■") : "↑";

        public string PrimaryActionToolTip
        {
            get
            {
                if (_isCompactingConversation)
                    return "停止上下文压缩";
                if (IsViewingQueuedRun)
                    return "取消这个排队任务";
                if (IsViewingActiveRun)
                    return CanPauseAgentRun ? "暂停并保存当前 Agent 任务" : Properties.Resources.CopilotStopGeneration;
                if (IsBusy)
                {
                    var admission = EvaluateComposerRequestAdmission(ResolveComposerRequestMode());
                    return admission.Reason switch
                    {
                        CopilotRequestAdmissionReason.Allowed => $"加入 Agent 队列（当前等待 {_taskHost.QueuedCount}/{_taskHost.MaxQueuedRuns}）",
                        CopilotRequestAdmissionReason.ActiveChatIsExclusive => "另一个普通对话正在生成；完成后才能发送新请求",
                        CopilotRequestAdmissionReason.ChatCannotQueue => "普通对话不能排队；请等待当前 Agent 任务结束",
                        CopilotRequestAdmissionReason.ConversationAlreadyScheduled => "此会话已有任务正在运行或排队",
                        CopilotRequestAdmissionReason.HostShutdown => "Copilot 正在关闭，不能再发送请求",
                        CopilotRequestAdmissionReason.QueueFull => $"Agent 队列已满（{_taskHost.QueuedCount}/{_taskHost.MaxQueuedRuns}）",
                        _ => "当前没有可接收请求的会话",
                    };
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

        public string InputText
        {
            get => _inputText;
            set
            {
                if (SetProperty(ref _inputText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(IsInputEmpty));
                    OnPropertyChanged(nameof(LocalCommandSuggestions));
                    OnPropertyChanged(nameof(HasLocalCommandSuggestions));
                    OnPropertyChanged(nameof(CanSteerCurrentRun));
                    RefreshComposerTokenEstimate();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
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

        public string InputPlaceholder => IsConversationEmpty ? "随心输入 · 输入 / 或 $ 查看命令与 Skill" : "要求后续变更 · 输入 / 或 $ 查看命令与 Skill";

        public bool IsInputEmpty => string.IsNullOrWhiteSpace(InputText);

        public IReadOnlyList<CopilotLocalCommand> LocalCommandSuggestions
        {
            get
            {
                var input = (InputText ?? string.Empty).Trim();
                if (input.Length == 0 || input[0] is not '/' and not '$')
                    return Array.Empty<CopilotLocalCommand>();
                if (input.StartsWith('/') && CopilotLocalCommandCatalog.FindExact(input) != null)
                    return Array.Empty<CopilotLocalCommand>();
                if (ResolveComposerRequestMode() == CopilotAgentMode.Chat)
                    return CopilotLocalCommandCatalog.Suggest(input);

                var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
                var searchRoots = CopilotAgentRequestFactory.BuildSearchRootPaths(turnSnapshot, Array.Empty<string>());
                var skills = CopilotAgentSkillCatalog.DiscoverCached(
                    searchRoots,
                    _config.AgentDefaults.CreateSkillOverrideSnapshot());
                return CopilotLocalCommandCatalog.Suggest(input, skills);
            }
        }

        public bool HasLocalCommandSuggestions => LocalCommandSuggestions.Count > 0;

        public bool TryCompleteLocalCommand(CopilotLocalCommand? command = null)
        {
            var suggestions = LocalCommandSuggestions;
            command ??= suggestions.Count > 0 ? suggestions[0] : null;
            if (command == null)
                return false;

            InputText = command.Name + (command.AcceptsArguments ? " " : string.Empty);
            return true;
        }

        private CopilotHostedAgentRun? ActiveHostedRun => _taskHost.ActiveRun;

        private CopilotHostedAgentRun? SelectedHostedRun => _taskHost.FindRunByConversationId(SelectedConversation?.Id);

        private bool IsAgentRequestActive => ActiveHostedRun?.IsAgent == true;

        private bool IsViewingActiveRun => string.Equals(ActiveHostedRun?.ConversationId, SelectedConversation?.Id, StringComparison.Ordinal);

        private bool IsViewingQueuedRun => SelectedHostedRun?.State == CopilotHostedRunState.Queued;

        public bool CanSteerCurrentRun => IsBusy && IsAgentRequestActive && IsViewingActiveRun && !IsInputEmpty;

        public bool CanCancelAgentRun => IsViewingActiveRun
            && IsAgentRequestActive
            && SelectedHostedRun?.CanRequestCancel == true;

        public bool CanPauseAgentRun => IsViewingActiveRun && ActiveHostedRun?.CanRequestPause == true;

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSwitchConversation));
                OnPropertyChanged(nameof(CanSelectProfile));
                OnPropertyChanged(nameof(PrimaryActionGlyph));
                OnPropertyChanged(nameof(PrimaryActionToolTip));
                OnPropertyChanged(nameof(AttachmentMenuToolTip));
                OnPropertyChanged(nameof(CanSteerCurrentRun));
                OnPropertyChanged(nameof(CanCancelAgentRun));
                OnPropertyChanged(nameof(CanPauseAgentRun));
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

                return CopilotMcpServer.Instance.IsRunning ? "完全访问" : "控制停止";
            }
        }

        public string McpStatusToolTip
        {
            get
            {
                var server = CopilotMcpServer.Instance;
                var pendingCount = CopilotMcpConfirmationStore.Instance.PendingCount;
                var status = string.IsNullOrWhiteSpace(server.LastStatusMessage)
                    ? "No MCP status message is available."
                    : CopilotMcpAuditLogger.RedactText(server.LastStatusMessage);

                var entries = CopilotMcpAuditLogger.GetRecentEntries(8);
                var failureCount = entries.Count(entry => !entry.Success);
                var builder = new StringBuilder();
                builder.AppendLine($"Endpoint: {_config.McpEndpoint}");
                builder.AppendLine($"Service: {BuildMcpStatusSummary()}");
                builder.AppendLine($"Pending actions: {pendingCount}");
                builder.AppendLine($"Recent calls: {entries.Count}; failures: {failureCount}");

                var lastEntry = entries.Count > 0 ? entries[^1] : null;
                if (lastEntry != null)
                    builder.AppendLine($"Last call: {FormatMcpAuditEntryForTooltip(lastEntry)}");

                var lastError = CopilotMcpAuditLogger.GetLastError();
                if (lastError != null)
                    builder.AppendLine($"Last error: {FormatMcpAuditEntryForTooltip(lastError)}");

                builder.Append(status);
                return builder.ToString();
            }
        }

        private bool TryExecuteLocalCommand(string prompt)
        {
            var invocation = CopilotLocalCommandCatalog.Parse(prompt);
            if (invocation == null)
                return false;

            InputText = string.Empty;
            var command = invocation.Command;
            switch (command.Kind)
            {
                case CopilotLocalCommandKind.Status:
                    ShowLocalCommandResult(command, BuildStatusDiagnosticsReport());
                    break;
                case CopilotLocalCommandKind.Context:
                    ShowLocalCommandResult(command, BuildContextDiagnosticsReport());
                    break;
                case CopilotLocalCommandKind.Skills:
                    ShowLocalCommandResult(command, BuildAgentSkillDiagnosticsReport());
                    break;
                case CopilotLocalCommandKind.Mcp:
                    ShowLocalCommandResult(command, McpStatusToolTip);
                    break;
                case CopilotLocalCommandKind.Diff:
                    RunUiOperation(() => ShowGitDiffAsync(command, invocation.Arguments), "读取 Git 变更");
                    break;
                case CopilotLocalCommandKind.Compact:
                    RunUiOperation(() => CompactConversationAsync(command, invocation.Arguments), "压缩上下文");
                    break;
                case CopilotLocalCommandKind.Review:
                    StartWorkspaceReview(command, invocation.Arguments);
                    break;
                case CopilotLocalCommandKind.NewConversation:
                    DismissLocalCommandResult();
                    StartNewChat();
                    break;
                default:
                    return false;
            }
            return true;
        }

        private string BuildStatusDiagnosticsReport()
        {
            var profile = SelectedProfile;
            var defaults = _config.AgentDefaults;
            var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
            var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
            var skillUsage = CopilotAgentSkillUsageStore.Shared.GetSnapshot();
            var activeRun = ActiveHostedRun;
            return CopilotStatusDiagnostics.Format(new CopilotStatusDiagnosticSnapshot
            {
                ApplicationVersion = typeof(CopilotChatViewModel).Assembly.GetName().Version?.ToString(3) ?? "unknown",
                ProfileLabel = profile?.DisplayLabel ?? string.Empty,
                ProfileDetails = profile?.SecondaryLabel ?? string.Empty,
                ProfileConfigured = profile?.IsConfigured == true,
                ReasoningLabel = profile?.ReasoningLabel ?? "默认",
                Mode = ResolveComposerRequestMode(),
                AgentState = activeRun?.State.ToString() ?? "Idle",
                QueuedAgentRuns = _taskHost.QueuedCount,
                MaximumQueuedAgentRuns = _taskHost.MaxQueuedRuns,
                WorkspacePath = turnSnapshot.SolutionDirectoryPath,
                ActiveDocumentPath = turnSnapshot.ActiveDocumentPath,
                PreferredShell = defaults.PreferredShell,
                ContextWindowTokens = defaults.ContextWindowTokens,
                RequestTokenBudget = defaults.RequestTokenBudget,
                MaximumToolCalls = defaults.MaxToolCalls,
                MaximumAgentPasses = defaults.MaxAgentPasses,
                TimeoutSeconds = defaults.TimeoutSeconds,
                RegisteredCapabilities = capabilitySnapshot.Capabilities.Count,
                ApprovalCapabilities = capabilitySnapshot.Capabilities.Count(capability => capability.ApprovalMode != CopilotToolApprovalMode.Never),
                TrackedSkills = skillUsage.Entries.Count,
                ExplicitOnlySkills = skillUsage.HistoricalExplicitOnlySkills.Count,
                McpListenerEnabled = _config.McpEnabled,
                McpListenerRunning = CopilotMcpServer.Instance.IsRunning,
                EnabledExternalMcpServers = _config.ExternalMcpServers.Count(server => server?.Enabled == true),
                PendingApprovals = CopilotMcpConfirmationStore.Instance.PendingCount,
            });
        }

        private void StartWorkspaceReview(CopilotLocalCommand command, string focusInstructions)
        {
            if (IsBusy)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再开始审查。");
                return;
            }

            var prompt = new StringBuilder("Review the current uncommitted workspace changes. Do not modify files or apply fixes.");
            if (!string.IsNullOrWhiteSpace(focusInstructions))
                prompt.Append(" Focus: ").Append(focusInstructions.Trim());

            DismissLocalCommandResult();
            SetPendingRequestModeOverride(CopilotAgentMode.Review);
            InputText = prompt.ToString();
            RunUiOperation(SendAsync, "开始工作区审查");
        }

        private async Task ShowGitDiffAsync(CopilotLocalCommand command, string scope)
        {
            if (_isInspectingGitDiff)
            {
                ShowLocalCommandResult(command, "Git 变更快照正在生成，请稍候。");
                return;
            }

            _isInspectingGitDiff = true;
            ShowLocalCommandResult(command, "正在读取本地 Git 变更…不会调用模型，也不会修改文件。");
            try
            {
                var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
                var searchRoots = CopilotAgentRequestFactory.BuildSearchRootPaths(turnSnapshot, Array.Empty<string>());
                var result = await _localGitDiffService.ExecuteAsync(searchRoots, scope, CancellationToken.None);
                ShowLocalCommandResult(command, result.Report);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ShowLocalCommandResult(command, "Git 变更快照失败：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
            finally
            {
                _isInspectingGitDiff = false;
            }
        }

        private async Task CompactConversationAsync(CopilotLocalCommand command, string focusInstructions)
        {
            var conversation = SelectedConversation;
            var profile = SelectedProfile;
            if (IsBusy || _isCompactingConversation)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再压缩上下文。");
                return;
            }
            if (conversation == null || profile?.IsConfigured != true)
            {
                ShowLocalCommandResult(command, "请先选择并配置可用模型。");
                return;
            }
            if (CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                conversation,
                profile,
                CopilotCapabilityCatalog.Shared.GetSnapshot()))
            {
                ShowLocalCommandResult(
                    command,
                    "当前会话还有可安全继续的 Agent 任务。请先使用“继续任务”完成它，或在任务列表中明确放弃它，再压缩上下文；本次压缩未开始，checkpoint 已保留。");
                return;
            }

            var sourceMessages = conversation.Messages
                .Where(message => !string.IsNullOrWhiteSpace(message.ModelContent))
                .ToArray();
            var newMessageCount = CopilotConversationCompactionContext.CountMessagesAfterBoundary(conversation);
            if (sourceMessages.Length < 2 || newMessageCount < 2)
            {
                var reason = conversation.Compaction == null
                    ? "至少需要一轮完整对话后才能压缩。"
                    : "上次压缩后还没有足够的新对话，不需要重复压缩。";
                ShowLocalCommandResult(command, reason);
                return;
            }

            var boundaryMessage = sourceMessages[^1];
            var compactProfile = profile.Clone();
            compactProfile.UseSystemPromptOverride(CompactSystemPrompt);
            compactProfile.MaxTokens = Math.Min(compactProfile.MaxTokens, CompactSummaryOutputTokens);
            compactProfile.Temperature = 0.1;

            var rawHistory = CopilotConversationCompactionContext.Build(conversation, stopBeforeMessage: null, useModelContent: true);
            var request = rawHistory
                .Append(new CopilotRequestMessage("user", BuildCompactRequest(focusInstructions)))
                .ToArray();
            var selectedRequest = CopilotConversationHistoryWindow.Select(request, ResolveConversationHistoryLimits(compactProfile)).ToArray();
            var compactedInput = selectedRequest.Take(Math.Max(0, selectedRequest.Length - 1)).ToArray();
            var sourceCharacters = compactedInput.Sum(message => message.Content.Length);

            using var cancellation = new CancellationTokenSource();
            _compactConversationCts = cancellation;
            _isCompactingConversation = true;
            IsBusy = true;
            ShowLocalCommandResult(command, "正在压缩当前对话…完整聊天记录会继续保留在本地。");
            try
            {
                var reply = await _chatService.CompleteReplyAsync(compactProfile, selectedRequest, cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                var summary = NormalizeCompactSummary(reply.Content);
                if (summary.Length == 0)
                    throw new InvalidOperationException("模型没有返回可用的压缩摘要。");
                if (!Conversations.Contains(conversation) || !conversation.Messages.Contains(boundaryMessage))
                    throw new InvalidOperationException("压缩期间会话已发生变化，结果未应用。");

                conversation.Compaction = new CopilotConversationCompaction
                {
                    Summary = summary,
                    ThroughMessageId = boundaryMessage.Id,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    SourceMessageCount = compactedInput.Length,
                    SourceCharacters = sourceCharacters,
                };
                conversation.AgentSessionCheckpoint = null;
                UpdateConversationMetadata(conversation, touch: true);
                PersistState();

                var retainedAfterBoundary = CopilotConversationCompactionContext.CountMessagesAfterBoundary(conversation);
                ShowLocalCommandResult(
                    command,
                    $"已压缩 {compactedInput.Length:N0} 条有效上下文、{sourceCharacters:N0} 个字符。\n"
                    + $"后续请求将使用 {summary.Length:N0} 字符摘要，并保留边界后的 {retainedAfterBoundary:N0} 条新消息；界面中的完整对话未删除。"
                    + (string.IsNullOrWhiteSpace(focusInstructions) ? string.Empty : "\n聚焦要求：" + focusInstructions.Trim()));
                RefreshComposerTokenEstimate();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                ShowLocalCommandResult(command, "上下文压缩已取消，原有对话和压缩状态均未改变。");
            }
            catch (Exception ex)
            {
                ShowLocalCommandResult(command, "压缩失败：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
            finally
            {
                if (ReferenceEquals(_compactConversationCts, cancellation))
                    _compactConversationCts = null;
                _isCompactingConversation = false;
                IsBusy = _taskHost.IsActive;
            }
        }

        private static string BuildCompactRequest(string focusInstructions)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Create a continuation summary for all conversation context above.");
            builder.AppendLine("Keep the active goal, user constraints and preferences, decisions, verified state, important paths and identifiers, completed work and evidence, remaining work, blockers, and the next concrete action.");
            builder.AppendLine("Omit greetings, repetition, superseded alternatives, and low-value detail. Return only the summary.");
            if (!string.IsNullOrWhiteSpace(focusInstructions))
                builder.Append("Additional focus from the user: ").Append(focusInstructions.Trim());
            return builder.ToString().Trim();
        }

        private static string NormalizeCompactSummary(string summary)
        {
            var normalized = (summary ?? string.Empty).Trim();
            return normalized.Length <= CopilotConversationCompaction.MaximumSummaryCharacters
                ? normalized
                : normalized[..CopilotConversationCompaction.MaximumSummaryCharacters].TrimEnd();
        }

        private string BuildAgentSkillDiagnosticsReport()
        {
            var agentDefaults = _config.AgentDefaults;
            return CopilotAgentSkillDiagnostics.FormatReport(
                CopilotAgentSkillUsageStore.Shared.GetSnapshot(),
                CopilotAgentSkills.ResolveMetadataCharacterBudget(agentDefaults.ContextWindowTokens),
                agentDefaults.CreateSkillOverrideSnapshot());
        }

        private void ShowLocalCommandResult(CopilotLocalCommand command, string report)
        {
            LocalCommandResultTitle = $"{command.Name} · 本地快照";
            LocalCommandResultText = report;
        }

        private string BuildContextDiagnosticsReport()
        {
            var mode = ResolveComposerRequestMode();
            var agentContextEnabled = mode != CopilotAgentMode.Chat;
            var history = CopilotConversationRequestBuilder.CaptureHistorySelection(SelectedConversation, ResolveConversationHistoryLimits(SelectedProfile));
            var projectInstructions = Array.Empty<CopilotProjectInstructionDocument>();
            CopilotAgentSkillUsageSnapshot? skillUsage = null;
            if (agentContextEnabled)
            {
                var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
                var searchRoots = CopilotAgentRequestFactory.BuildSearchRootPaths(turnSnapshot, Array.Empty<string>());
                projectInstructions = CopilotAgentProjectInstructions.Discover(searchRoots, turnSnapshot.ActiveDocumentPath).ToArray();
                skillUsage = CopilotAgentSkillUsageStore.Shared.GetSnapshot();
            }

            var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
            var agentExtensionSnapshot = CopilotAgentExtensionBridge.Shared.GetSnapshot();
            var agentDefaults = _config.AgentDefaults;
            var historyLimits = ResolveConversationHistoryLimits(SelectedProfile);
            var compaction = SelectedConversation?.Compaction;
            return CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
            {
                ProfileLabel = SelectedProfile?.DisplayLabel ?? string.Empty,
                Mode = mode,
                SystemPromptCharacters = SelectedProfile?.EffectiveSystemPrompt.Length ?? 0,
                SourceHistoryMessages = history.SourceMessageCount,
                RetainedHistoryMessages = history.Messages.Length,
                SourceHistoryCharacters = history.SourceCharacters,
                RetainedHistoryCharacters = history.RetainedCharacters,
                HistoryMaximumMessages = historyLimits.MaximumMessages,
                HistoryMaximumCharacters = historyLimits.MaximumCharacters,
                HistoryMaximumContentCharacters = historyLimits.MaximumContentCharacters,
                HistoryContextWindowTokens = agentDefaults.ContextWindowTokens,
                CompactedSourceMessages = compaction?.SourceMessageCount ?? 0,
                CompactionSummaryCharacters = compaction?.Summary.Length ?? 0,
                AttachmentCount = Attachments.Count,
                FileAttachmentCount = Attachments.Count(item => item.Type == CopilotAttachmentType.File),
                ImageAttachmentCount = Attachments.Count(item => item.Type == CopilotAttachmentType.Image),
                WebAttachmentCount = Attachments.Count(item => item.Type == CopilotAttachmentType.WebPage),
                HasLiveWindowContext = HasCurrentLiveContext,
                AgentContextEnabled = agentContextEnabled,
                ProjectInstructionDocuments = projectInstructions.Length,
                ProjectInstructionPromptCharacters = CopilotAgentProjectInstructions.BuildPromptBlock(projectInstructions).Length,
                ProjectInstructions = projectInstructions,
                RecordedSkillRuns = skillUsage?.RecordedRuns ?? 0,
                TrackedSkills = skillUsage?.Entries.Count ?? 0,
                HistoricalExplicitOnlySkills = skillUsage?.HistoricalExplicitOnlySkills.Count ?? 0,
                ManualSkillOverrides = agentDefaults.SkillOverrides.Count,
                SkillMetadataCharacterBudget = CopilotAgentSkills.ResolveMetadataCharacterBudget(
                    agentDefaults.ContextWindowTokens),
                AgentContextWindowTokens = agentDefaults.ContextWindowTokens,
                AgentRequestTokenBudget = agentDefaults.RequestTokenBudget,
                AgentMaxToolCalls = agentDefaults.MaxToolCalls,
                AgentMaxPasses = agentDefaults.MaxAgentPasses,
                AgentTimeoutSeconds = agentDefaults.TimeoutSeconds,
                RegisteredCapabilities = capabilitySnapshot.Capabilities.Count,
                EnabledExternalMcpServers = _config.ExternalMcpServers.Count(server => server?.Enabled == true),
                AgentExtensions = agentExtensionSnapshot.Sources,
                AgentExtensionIssues = agentExtensionSnapshot.Issues,
            });
        }

        private void DismissLocalCommandResult()
        {
            LocalCommandResultTitle = string.Empty;
            LocalCommandResultText = string.Empty;
        }

        private void RunUiOperation(Func<Task> operation, string operationName, Action<string>? onError = null)
        {
            CopilotUiTaskObserver.Run(
                operation,
                operationName,
                onError ?? (message =>
                {
                    LocalCommandResultTitle = operationName + " · 失败";
                    LocalCommandResultText = message;
                }));
        }

        private async Task SendAsync()
        {
            var prompt = (InputText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                return;
            if (TryExecuteLocalCommand(prompt))
                return;

            var requestMode = ResolveComposerRequestMode();
            if (!CanScheduleComposerRequest(requestMode))
                return;

            if (SelectedProfile == null || !SelectedProfile.IsConfigured)
            {
                OpenSettings();
                return;
            }

            var requestProfile = SelectedProfile.Clone();
            var conversation = EnsureConversation();
            conversation.ProfileId = requestProfile.Id;
            conversation.ProfileDisplayName = requestProfile.DisplayLabel;
            var turnSnapshot = CaptureHostedTurnSnapshot(conversation);
            var recoveryRequest = ConsumePendingAgentRecoveryRequest();
            requestMode = ConsumeRequestModeOverride();

            var userMessage = new CopilotChatMessage(CopilotChatRole.User, prompt)
            {
                RequestMode = requestMode,
                RecoveryRequest = recoveryRequest,
                Attachments = new ObservableCollection<CopilotAttachmentItem>(turnSnapshot.Attachments),
                AttachmentSnapshotCaptured = true,
            };
            var assistantMessage = CreatePendingAssistantMessage(requestProfile);

            Messages.Add(userMessage);
            Messages.Add(assistantMessage);
            UpdateConversationMetadata(conversation, touch: true);
            PersistState();

            if (!_taskHost.TrySchedule(
                conversation.Id,
                userMessage.RequestMode,
                run => ExecuteHostedTurnAsync(run, conversation, requestProfile, userMessage, assistantMessage, turnSnapshot),
                out var hostedRun)
                || hostedRun == null)
            {
                conversation.Messages.Remove(assistantMessage);
                conversation.Messages.Remove(userMessage);
                _pendingAgentRecoveryRequest = recoveryRequest;
                _pendingRequestModeOverride = requestMode == CopilotAgentMode.Auto ? null : requestMode;
                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
                OnComposerRequestModeChanged();
                return;
            }

            DismissLocalCommandResult();
            ConsumeComposerAttachments(conversation);
            InputText = string.Empty;
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
            CopilotHostedTurnCompletion.CompleteQueuedCancellation(assistantMessage);
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
            try
            {
                var usage = await RunConversationTurnAsync(hostedRun, conversation, requestProfile, userMessage, assistantMessage, turnSnapshot, refreshExternalContext);
                CopilotHostedTurnCompletion.CompleteSuccessfully(conversation, assistantMessage, usage);
                UpdateConversationMetadata(conversation, touch: true);
                await PersistStateAndFlushAsync();
                QueueConversationTitleGeneration(conversation, requestProfile);
            }
            catch (OperationCanceledException)
            {
                var controlIntent = hostedRun.RunControl?.Intent ?? CopilotAgentControlIntent.None;
                CopilotHostedTurnCompletion.CompleteCancellation(conversation, assistantMessage, controlIntent);
                UpdateConversationMetadata(conversation, touch: true);
                await PersistStateAndFlushAsync();
            }
            catch (Exception ex)
            {
                CopilotHostedTurnCompletion.CompleteFailure(conversation, assistantMessage, ex.Message, requestProfile.ApiKey);
                UpdateConversationMetadata(conversation, touch: true);
                await PersistStateAndFlushAsync();
            }
            finally
            {
                RefreshAgentTasks();
            }
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

            if (userMessage.RequestMode == CopilotAgentMode.Chat)
            {
                conversation.AgentSessionCheckpoint = null;
                PersistState();
                return await RunChatTurnAsync(requestProfile, userMessage, assistantMessage, turnSnapshot, refreshExternalContext, cancellationToken);
            }

            return await RunAgentTurnAsync(hostedRun, conversation, requestProfile, userMessage, assistantMessage, turnSnapshot, refreshExternalContext, cancellationToken);
        }

        private async Task<CopilotTokenUsage> RunChatTurnAsync(
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            CopilotAgentHostContextSnapshot turnSnapshot,
            bool refreshExternalContext,
            CancellationToken cancellationToken)
        {
            var prompt = (userMessage.Content ?? string.Empty).Trim();
            if (refreshExternalContext || string.IsNullOrWhiteSpace(userMessage.RequestContent))
                userMessage.RequestContent = await _conversationRequestBuilder.BuildUserRequestContentAsync(prompt, turnSnapshot.LiveContext, cancellationToken);

            var history = CopilotConversationRequestBuilder.BuildChatHistory(
                turnSnapshot.ConversationHistory,
                userMessage.RequestContent,
                turnSnapshot.Attachments,
                ResolveConversationHistoryLimits(requestProfile),
                includeAttachmentContext: true);
            return await StreamChatReplyAsync(requestProfile, history, assistantMessage, cancellationToken);
        }

        private async Task<CopilotTokenUsage> StreamChatReplyAsync(
            CopilotProfileConfig requestProfile,
            IReadOnlyList<CopilotRequestMessage> history,
            CopilotChatMessage assistantMessage,
            CancellationToken cancellationToken)
        {
            var dispatcher = Application.Current?.Dispatcher;
            var streamContext = dispatcher == null
                ? SynchronizationContext.Current
                : new DispatcherSynchronizationContext(dispatcher);
            var deltaBuffer = new CopilotStreamDeltaBuffer(
                streamContext,
                deltas => ApplyChatDeltas(assistantMessage, deltas),
                isOnTargetThread: dispatcher == null ? null : dispatcher.CheckAccess);
            try
            {
                return await _chatService.StreamReplyAsync(
                    requestProfile,
                    history,
                    deltaBuffer.Enqueue,
                    retry => ApplyProviderRetryOnUiThread(assistantMessage, retry),
                    cancellationToken);
            }
            finally
            {
                await deltaBuffer.CompleteAsync();
            }
        }

        private async Task<CopilotTokenUsage> RunAgentTurnAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            CopilotAgentHostContextSnapshot turnSnapshot,
            bool refreshExternalContext,
            CancellationToken cancellationToken)
        {
            if (!refreshExternalContext && !string.IsNullOrWhiteSpace(userMessage.RequestContent))
            {
                assistantMessage.MarkThinkingStarted();
                assistantMessage.IsExecutionInProgress = true;
                assistantMessage.IsExecutionExpanded = true;

                var history = CopilotConversationRequestBuilder.BuildVisibleHistory(turnSnapshot.ConversationHistory, ResolveConversationHistoryLimits(requestProfile)).ToList();
                history.Add(new CopilotRequestMessage("user", userMessage.RequestContent.Trim()));

                return await StreamChatReplyAsync(requestProfile, history, assistantMessage, cancellationToken);
            }

            var requestPlan = CopilotAgentRequestFactory.Prepare(userMessage.Content, userMessage.RequestMode, turnSnapshot);
            IReadOnlyList<CopilotContextItem> contextItems = await _contextRegistry.CaptureAsync(
                requestPlan.ContextRequest,
                cancellationToken);

            contextItems = MergeCurrentLiveContextSummary(contextItems, turnSnapshot.LiveContext);
            var sessionCheckpoint = conversation.AgentSessionCheckpoint;
            var copilotConfig = CopilotConfig.Instance;
            var agentRequest = CopilotAgentRequestFactory.Create(requestPlan, new CopilotAgentRequestBuildInput
            {
                Profile = requestProfile,
                History = CopilotConversationRequestBuilder.BuildVisibleHistory(turnSnapshot.ConversationHistory, ResolveConversationHistoryLimits(requestProfile)),
                ContextItems = contextItems,
                SessionCheckpoint = sessionCheckpoint,
                Recovery = userMessage.RecoveryRequest,
                RunControl = hostedRun.RunControl,
                AgentDefaults = copilotConfig.AgentDefaults,
                ExternalMcpServers = copilotConfig.ExternalMcpServers,
            });

            CopilotAgentRunResult result;
            var dispatcher = Application.Current?.Dispatcher;
            var streamContext = dispatcher == null
                ? SynchronizationContext.Current
                : new DispatcherSynchronizationContext(dispatcher);
            var eventBuffer = new CopilotAgentEventBuffer(
                streamContext,
                events => ApplyAgentEvents(hostedRun, conversation, assistantMessage, events),
                isOnTargetThread: dispatcher == null ? null : dispatcher.CheckAccess);
            try
            {
                try
                {
                    result = await _agentRuntime.RunAsync(agentRequest, eventBuffer.Enqueue, cancellationToken);
                }
                finally
                {
                    await eventBuffer.CompleteAsync();
                }
            }
            catch (OperationCanceledException) when (hostedRun.RunControl?.Intent == CopilotAgentControlIntent.Pause && sessionCheckpoint != null)
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
                if (sessionCheckpoint != null && conversation.AgentSessionCheckpoint == null)
                {
                    conversation.AgentSessionCheckpoint = sessionCheckpoint;
                    PersistState(immediate: true);
                }
                throw;
            }

            userMessage.RequestContent = result.PreparedUserMessageContent;
            assistantMessage.AgentTaskLedger = result.TaskLedger;
            assistantMessage.AgentStopReason = result.StopReason;
            assistantMessage.AgentBlockers = result.Blockers;
            conversation.AgentSessionCheckpoint = result.SessionCheckpoint;
            if (string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, result.StopReason switch
                {
                    CopilotAgentStopReason.Paused => "Agent 任务已暂停；当前任务状态已经保存，可以稍后继续。",
                    CopilotAgentStopReason.Cancelled => "Agent 任务已取消；本轮新 checkpoint 已丢弃。",
                    _ => assistantMessage.Content,
                });
            }
            PersistState(immediate: true);
            return result.Usage;
        }

        private void WorkspaceManager_ContentIdSelected(object? sender, string contentId)
        {
            _activeDocumentPath = contentId ?? string.Empty;
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

        private void TaskHost_Changed(object? sender, CopilotAgentTaskHostChangedEventArgs e)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => TaskHost_Changed(sender, e)));
                return;
            }

            IsBusy = _taskHost.IsActive;
            if (e.Kind == CopilotAgentTaskHostChangeKind.ControlRequested
                && e.Run.HasStarted
                && e.Run.State == CopilotHostedRunState.CancelRequested
                && e.Run.RunControl?.Intent == CopilotAgentControlIntent.Cancel)
            {
                var conversation = Conversations.FirstOrDefault(item => string.Equals(item.Id, e.Run.ConversationId, StringComparison.Ordinal));
                if (conversation?.AgentSessionCheckpoint != null)
                {
                    conversation.AgentSessionCheckpoint = null;
                    PersistState(immediate: true);
                }
            }
            if (e.Kind == CopilotAgentTaskHostChangeKind.Completed)
                RefreshAgentTasks();
            NotifyHostedRunStateChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        private void NotifyHostedRunStateChanged()
        {
            RefreshConversationRunStatuses();
            OnPropertyChanged(nameof(CanSwitchConversation));
            OnPropertyChanged(nameof(CanSteerCurrentRun));
            OnPropertyChanged(nameof(CanCancelAgentRun));
            OnPropertyChanged(nameof(CanPauseAgentRun));
            OnPropertyChanged(nameof(PrimaryActionGlyph));
            OnPropertyChanged(nameof(PrimaryActionToolTip));
            RefreshAgentRunNotice();
        }

        private void RefreshConversationRunStatuses()
        {
            var activeRun = ActiveHostedRun;
            CopilotAgentRunStatusSynchronizer.Refresh(
                Conversations,
                activeRun?.IsAgent == true ? activeRun.ConversationId : null,
                activeRun?.IsAgent == true ? activeRun.State : null,
                _taskHost.QueuedRuns.Where(run => run.IsAgent).Select(run => run.ConversationId).ToArray());
        }

        private void RefreshAgentRunNotice()
        {
            var selectedRun = SelectedHostedRun;
            if (selectedRun?.State == CopilotHostedRunState.Queued)
            {
                var position = _taskHost.GetQueuePosition(selectedRun.Id);
                _agentRunNoticeConversationId = selectedRun.ConversationId;
                AgentRunNoticeText = position > 0
                    ? $"Agent 已排队 · 前面 {position} 个任务"
                    : "Agent 已排队";
                return;
            }

            var run = ActiveHostedRun;
            if (run?.IsAgent != true || string.Equals(run.ConversationId, SelectedConversation?.Id, StringComparison.Ordinal))
            {
                ClearAgentRunNotice();
                return;
            }

            var conversation = Conversations.FirstOrDefault(item => string.Equals(item.Id, run.ConversationId, StringComparison.Ordinal));
            if (conversation == null)
            {
                ClearAgentRunNotice();
                return;
            }

            _agentRunNoticeConversationId = conversation.Id;
            var status = run.State switch
            {
                CopilotHostedRunState.PauseRequested => "正在暂停",
                CopilotHostedRunState.CancelRequested => "正在取消",
                _ => "正在运行",
            };
            AgentRunNoticeText = $"{conversation.Title} · {status}";
        }

        private void OpenAgentRunNotice()
        {
            var conversation = Conversations.FirstOrDefault(item => string.Equals(item.Id, _agentRunNoticeConversationId, StringComparison.Ordinal));
            if (conversation != null && CanSwitchConversation)
                SelectConversation(conversation, persist: true, preferredProfileId: conversation.ProfileId);

            RefreshAgentRunNotice();
        }

        private void ClearAgentRunNotice()
        {
            _agentRunNoticeConversationId = string.Empty;
            AgentRunNoticeText = string.Empty;
            CommandManager.InvalidateRequerySuggested();
        }

        private void InitializeStateRecoveryNotice()
        {
            if (_stateStore is not CopilotChatStateStore stateStore)
                return;

            StateRecoveryNoticeText = stateStore.LastLoadStatus.Source switch
            {
                _ when stateStore.IsManagedAttachmentCleanupProtected => "此前的会话状态无法完整恢复；托管附件已保护，自动清理暂停。",
                CopilotChatStateLoadSource.Temporary => "已从写入中断前的临时快照恢复会话。",
                CopilotChatStateLoadSource.Backup => "主会话状态不可用，已从可信备份恢复。",
                CopilotChatStateLoadSource.Unrecoverable => "会话状态无法读取，已打开空会话；可恢复的托管附件不会被自动删除。",
                _ => string.Empty,
            };
            StateRecoveryNoticeToolTip = string.IsNullOrWhiteSpace(StateRecoveryNoticeText)
                ? string.Empty
                : $"{StateRecoveryNoticeText}{Environment.NewLine}{Environment.NewLine}状态目录：{stateStore.StateDirectoryPath}";
        }

        private void ConfirmationStore_ActionsChanged(object? sender, EventArgs e)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => ConfirmationStore_ActionsChanged(sender, e)));
                return;
            }

            RefreshPendingActions();
        }

        private void RefreshMcpStatus()
        {
            _hasPendingMcpActions = CopilotMcpConfirmationStore.Instance.PendingCount > 0;
            _hasRecentMcpFailures = CopilotMcpAuditLogger.GetRecentEntries(20)
                .Any(entry => !entry.Success && DateTimeOffset.UtcNow - entry.TimestampUtc <= RecentMcpFailureWindow);

            OnPropertyChanged(nameof(IsMcpEnabled));
            OnPropertyChanged(nameof(IsMcpRunning));
            OnPropertyChanged(nameof(IsControlModeVisible));
            OnPropertyChanged(nameof(HasPendingMcpActions));
            OnPropertyChanged(nameof(HasRecentMcpFailures));
            OnPropertyChanged(nameof(McpStatusLabel));
            OnPropertyChanged(nameof(McpStatusToolTip));
            OnPropertyChanged(nameof(PrimaryActionToolTip));
        }

        private string BuildMcpStatusSummary()
        {
            if (!_config.McpEnabled)
                return "Disabled";

            if (CopilotMcpServer.Instance.IsRunning)
                return "Running";

            return "Stopped";
        }

        private static string FormatMcpAuditEntryForTooltip(CopilotMcpAuditEntry entry)
        {
            var result = entry.Success ? "OK" : "failed";
            var message = string.IsNullOrWhiteSpace(entry.ErrorMessage)
                ? string.Empty
                : " - " + CopilotMcpAuditLogger.RedactText(entry.ErrorMessage);
            var caller = string.IsNullOrWhiteSpace(entry.CallerSource)
                ? string.Empty
                : $" caller={entry.CallerSource}";

            return $"{entry.TimestampUtc.ToLocalTime():HH:mm:ss} {entry.ToolName} {result} {entry.DurationMs}ms{caller}{message}";
        }

        private void RefreshPendingActions()
        {
            _pendingActions.Clear();
            foreach (var action in CopilotMcpConfirmationStore.Instance.GetPendingActions())
                _pendingActions.Add(action);

            OnPropertyChanged(nameof(HasPendingActions));
            OnPropertyChanged(nameof(HasPendingActionPanel));
            OnPropertyChanged(nameof(PendingActionPanelTitle));
            OnPropertyChanged(nameof(PendingActionPanelSummary));
            OnPropertyChanged(nameof(PendingActionPanelToolTip));
            RefreshMcpStatus();
            CommandManager.InvalidateRequerySuggested();
        }

        private void CopyPendingActionId(ConfirmableAction? action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.ActionId))
                return;

            try
            {
                Clipboard.SetText(action.ActionId);
                SetPendingActionFeedback($"Copied action_id {action.ActionId}.");
            }
            catch (Exception ex)
            {
                SetPendingActionFeedback($"Copy failed: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}");
            }
        }

        private void CopyPendingActionPayload(ConfirmableAction? action)
        {
            if (action == null)
                return;

            try
            {
                Clipboard.SetText(action.ConfirmActionPayloadJson);
                SetPendingActionFeedback($"Copied confirm_action payload for {action.ActionId}.");
            }
            catch (Exception ex)
            {
                SetPendingActionFeedback($"Copy failed: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}");
            }
        }

        private async Task ApprovePendingActionAsync(ConfirmableAction? action)
        {
            if (action == null)
                return;

            var result = MessageBox.Show(
                Application.Current.GetActiveWindow(),
                BuildPendingActionApprovalPrompt(action),
                "Approve Copilot action",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (result != MessageBoxResult.Yes)
            {
                SetPendingActionFeedback($"Approval cancelled for {action.ActionId}.");
                return;
            }

            if (action.ResumesAgentOnApproval)
            {
                var approved = CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out var message);
                SetPendingActionFeedback(approved
                    ? $"{action.ActionId}: {message} The agent will resume in the same session."
                    : $"{action.ActionId}: {message}");
            }
            else if (action.ExecuteOnApproval)
            {
                var executionResult = await CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(action.ActionId, CancellationToken.None);
                SetPendingActionFeedback(executionResult.Success
                    ? $"{action.ActionId}: approved and executed."
                    : $"{action.ActionId}: {executionResult.Text}");
            }
            else
            {
                CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out var message);
                SetPendingActionFeedback($"{action.ActionId}: {message}");
            }
            RefreshPendingActions();
        }

        private void ConfirmationStore_ActionStatusChanged(object? sender, ConfirmableActionChangedEventArgs e)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => ConfirmationStore_ActionStatusChanged(sender, e)));
                return;
            }

            var action = e.Action;
            if (string.IsNullOrWhiteSpace(action.AgentCallId))
                return;

            var changed = false;
            foreach (var message in Conversations.SelectMany(conversation => conversation.Messages))
            {
                var trace = message.AgentTraceEntries.FirstOrDefault(entry =>
                    string.Equals(entry.CallId, action.AgentCallId, StringComparison.Ordinal)
                    || (!string.IsNullOrWhiteSpace(entry.ApprovalActionId)
                        && string.Equals(entry.ApprovalActionId, action.ActionId, StringComparison.OrdinalIgnoreCase)));
                if (trace == null)
                    continue;

                switch (action.Status)
                {
                    case ConfirmableActionStatus.Pending:
                    case ConfirmableActionStatus.Approved:
                        trace.State = CopilotToolExecutionState.AwaitingApproval;
                        break;
                    case ConfirmableActionStatus.Executing:
                        trace.State = CopilotToolExecutionState.Running;
                        message.MarkThinkingStarted();
                        message.IsExecutionInProgress = true;
                        break;
                    case ConfirmableActionStatus.Rejected:
                        trace.State = CopilotToolExecutionState.Denied;
                        trace.CompletedAtUtc = DateTimeOffset.UtcNow;
                        trace.ErrorMessage = "The user rejected this approval request.";
                        message.IsExecutionInProgress = false;
                        message.MarkThinkingCompleted();
                        break;
                    case ConfirmableActionStatus.Expired:
                        trace.State = CopilotToolExecutionState.TimedOut;
                        trace.CompletedAtUtc = DateTimeOffset.UtcNow;
                        trace.ErrorMessage = "The approval request expired before a decision was recorded.";
                        message.IsExecutionInProgress = false;
                        message.MarkThinkingCompleted();
                        break;
                    case ConfirmableActionStatus.Cancelled:
                        trace.State = CopilotToolExecutionState.Cancelled;
                        trace.CompletedAtUtc = action.CompletedAt ?? DateTimeOffset.UtcNow;
                        trace.ErrorMessage = CopilotAgentTraceEntry.Sanitize(action.ExecutionResultText);
                        message.IsExecutionInProgress = false;
                        message.MarkThinkingCompleted();
                        break;
                    case ConfirmableActionStatus.Executed:
                        if (action.ResumesAgentOnApproval)
                            break;
                        trace.State = action.ExecutionSucceeded == true
                            ? CopilotToolExecutionState.Completed
                            : CopilotToolExecutionState.Failed;
                        trace.CompletedAtUtc = action.CompletedAt ?? DateTimeOffset.UtcNow;
                        trace.ResultSummary = action.ExecutionSucceeded == true
                            ? CopilotAgentTraceEntry.Sanitize(action.ExecutionResultText)
                            : trace.ResultSummary;
                        trace.ErrorMessage = action.ExecutionSucceeded == false
                            ? CopilotAgentTraceEntry.Sanitize(action.ExecutionResultText)
                            : string.Empty;
                        message.IsExecutionInProgress = false;
                        message.MarkThinkingCompleted();
                        break;
                }

                trace.ApprovalActionId = action.ActionId;
                if (trace.CompletedAtUtc != null && trace.StartedAtUtc != default)
                    trace.DurationMs = Math.Max(trace.DurationMs, (long)Math.Max(0, (trace.CompletedAtUtc.Value - trace.StartedAtUtc).TotalMilliseconds));
                message.RebuildExecutionContentFromAgentTrace();
                changed = true;
            }

            if (changed)
                PersistState();
        }

        private static string BuildPendingActionApprovalPrompt(ConfirmableAction action)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Approve this Copilot action?");
            builder.AppendLine();
            builder.AppendLine(action.Title);
            builder.AppendLine($"Tool: {action.ToolName}");
            builder.AppendLine($"Risk: {action.RiskLevel}");
            builder.AppendLine($"Expires: {action.ExpiresAtLabel}");

            if (!string.IsNullOrWhiteSpace(action.ArgumentsSummary))
                builder.AppendLine($"Params: {action.ArgumentsSummary}");

            builder.AppendLine();
            builder.AppendLine("Only approve if the requested operation matches your intent.");
            builder.Append(action.ExecuteOnApproval
                ? "This in-app action will execute immediately after approval."
                : "The requesting MCP client must still call confirm_action after approval.");
            return builder.ToString();
        }

        private void RejectPendingAction(ConfirmableAction? action)
        {
            if (action == null)
                return;

            CopilotMcpConfirmationStore.Instance.Reject(action.ActionId, out var message);
            SetPendingActionFeedback($"{action.ActionId}: {message}");
            RefreshPendingActions();
        }

        private void SetPendingActionFeedback(string message)
        {
            _pendingActionFeedbackCts?.Cancel();
            var cts = new CancellationTokenSource();
            _pendingActionFeedbackCts = cts;
            PendingActionFeedbackText = message ?? string.Empty;
            _ = ClearPendingActionFeedbackAsync(cts);
        }

        private async Task ClearPendingActionFeedbackAsync(CancellationTokenSource cts)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
                if (!ReferenceEquals(_pendingActionFeedbackCts, cts))
                    return;

                if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => ClearPendingActionFeedback(cts)));
                    return;
                }

                ClearPendingActionFeedback(cts);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                cts.Dispose();
            }
        }

        private void ClearPendingActionFeedback(CancellationTokenSource cts)
        {
            if (!ReferenceEquals(_pendingActionFeedbackCts, cts))
                return;

            _pendingActionFeedbackCts = null;
            PendingActionFeedbackText = string.Empty;
        }

        private void OnCurrentLiveContextStateChanged()
        {
            OnPropertyChanged(nameof(HasCurrentLiveContext));
            OnPropertyChanged(nameof(CurrentLiveContextTitle));
            OnPropertyChanged(nameof(CurrentLiveContextSummary));
            OnPropertyChanged(nameof(CanAttachCurrentLiveContext));
            OnPropertyChanged(nameof(IsCurrentLiveContextAttached));
            OnPropertyChanged(nameof(CurrentLiveContextStatusText));
            OnPropertyChanged(nameof(CurrentLiveContextToolTip));
            OnPropertyChanged(nameof(CurrentLiveContextActionText));
            RefreshComposerTokenEstimate();
            CommandManager.InvalidateRequerySuggested();
        }

        private static IReadOnlyList<CopilotContextItem> MergeCurrentLiveContextSummary(
            IReadOnlyList<CopilotContextItem> contextItems,
            CopilotLiveContext? liveContext)
        {
            var liveContextItem = BuildCurrentLiveContextSummaryItem(liveContext);
            if (liveContextItem == null)
                return contextItems;

            var merged = new List<CopilotContextItem>((contextItems?.Count ?? 0) + 1)
            {
                liveContextItem,
            };

            if (contextItems != null)
                merged.AddRange(contextItems);

            return merged;
        }

        private static CopilotContextItem? BuildCurrentLiveContextSummaryItem(CopilotLiveContext? liveContext)
        {
            if (liveContext == null)
                return null;

            if (string.IsNullOrWhiteSpace(liveContext.Title) && string.IsNullOrWhiteSpace(liveContext.Summary))
                return null;

            return new CopilotContextItem
            {
                Id = string.IsNullOrWhiteSpace(liveContext.SourceId)
                    ? "live-context"
                    : $"{liveContext.SourceId}:summary",
                Title = liveContext.Title,
                Summary = liveContext.Summary,
            };
        }

        private CopilotAgentHostContextSnapshot CaptureHostedTurnSnapshot(
            CopilotConversationRecord conversation,
            CopilotChatMessage? stopBeforeMessage = null)
        {
            var attachments = stopBeforeMessage?.AttachmentSnapshotCaptured == true
                ? stopBeforeMessage.Attachments
                : conversation.Attachments;
            return CaptureHostedTurnSnapshot(
                attachments,
                CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation, stopBeforeMessage));
        }

        private CopilotAgentHostContextSnapshot CaptureHostedTurnSnapshot(
            IEnumerable<CopilotAttachmentItem> attachments,
            CopilotConversationHistorySnapshot? conversationHistory = null)
        {
            return new CopilotAgentHostContextSnapshot(
                _activeDocumentPath,
                SolutionManager.GetInstance().CurrentSolutionExplorer?.DirectoryInfo?.FullName ?? string.Empty,
                attachments,
                _currentLiveContext,
                conversationHistory);
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
            try
            {
                foreach (var agentEvent in agentEvents)
                {
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
                        assistantMessage.AgentTaskLedger = agentEvent.TaskLedger;
                        persistState = true;
                        persistImmediately = true;
                        refreshAgentTasks |= ReferenceEquals(conversation, SelectedConversation);
                        continue;
                    }

                    var presentationResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(assistantMessage, agentEvent);
                    if (!presentationResult.IsHandled || presentationResult.PersistenceMode == CopilotAgentEventPersistenceMode.None)
                        continue;

                    persistState = true;
                    persistImmediately |= presentationResult.PersistenceMode == CopilotAgentEventPersistenceMode.Immediate;
                }
            }
            finally
            {
                if (persistState)
                    PersistState(immediate: persistImmediately);
                if (refreshAgentTasks)
                    RefreshAgentTasks();
            }
        }

        private void StartNewChat()
        {
            if (IsBusy && !IsAgentRequestActive)
                CancelCurrentReply(discardAgentCheckpoint: true);
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

        private CopilotConversationRecord ResolveNewConversationTarget()
        {
            var profile = SelectedProfile ?? ResolveProfile(_state.ActiveProfileId) ?? _config.GetPreferredDefaultProfile();
            return CopilotConversationService.ResolveNewTarget(Conversations, SelectedConversation, profile);
        }

        private void ExecutePrimaryAction()
        {
            if (_isCompactingConversation)
            {
                _compactConversationCts?.Cancel();
                return;
            }
            if (IsViewingQueuedRun || IsViewingActiveRun)
            {
                CancelCurrentReply(discardAgentCheckpoint: !CanPauseAgentRun);
                return;
            }

            RunUiOperation(SendAsync, "发送请求");
        }

        private void ExecuteSendOrSteer()
        {
            if (IsViewingActiveRun)
            {
                var invocation = CopilotLocalCommandCatalog.Parse(InputText);
                if (invocation?.Command.AvailableWhileAgentRuns == true && TryExecuteLocalCommand(InputText))
                    return;

                TrySteerCurrentRun();
                return;
            }
            if (IsViewingQueuedRun)
                return;

            RunUiOperation(SendAsync, "发送请求");
        }

        private void TrySteerCurrentRun()
        {
            var steeringMessage = (InputText ?? string.Empty).Trim();
            var activeRun = ActiveHostedRun;
            if (!CanSteerCurrentRun || activeRun == null || string.IsNullOrWhiteSpace(steeringMessage))
                return;
            if (!_agentRuntime.TryEnqueueSteeringMessage(steeringMessage))
                return;

            var activeConversation = Conversations.FirstOrDefault(conversation => string.Equals(conversation.Id, activeRun.ConversationId, StringComparison.Ordinal));
            var activeAssistant = activeConversation?.Messages.LastOrDefault(message => !message.IsUser && message.IsThinkingInProgress);
            if (activeAssistant != null)
                CopilotAssistantMessagePresenter.AppendExecutionTrace(activeAssistant, "User steering queued · " + CopilotAgentTraceEntry.Sanitize(steeringMessage));

            InputText = string.Empty;
            PersistState();
        }

        private bool CanContinueAgentTasks(CopilotChatMessage? message)
        {
            if (!CanScheduleComposerRequest(CopilotAgentMode.Auto) || message == null || message.IsUser || !message.HasRecoverableAgentTasks)
                return false;
            if (SelectedConversation?.AgentSessionCheckpoint == null || SelectedProfile?.IsConfigured != true)
                return false;

            var latestAssistant = SelectedConversation.Messages.LastOrDefault(candidate => !candidate.IsUser);
            if (!ReferenceEquals(latestAssistant, message))
                return false;

            return CopilotAgentRecoveryPolicy.Evaluate(
                message,
                SelectedConversation.AgentSessionCheckpoint,
                SelectedProfile,
                CopilotCapabilityCatalog.Shared.GetSnapshot()).IsAvailable;
        }

        private void ContinueAgentTasks(CopilotChatMessage? message)
        {
            if (!CanContinueAgentTasks(message))
                return;

            var decision = CopilotAgentRecoveryPolicy.Evaluate(
                message,
                SelectedConversation?.AgentSessionCheckpoint,
                SelectedProfile,
                CopilotCapabilityCatalog.Shared.GetSnapshot());
            if (!decision.IsAvailable)
                return;

            _pendingAgentRecoveryRequest = decision.Request;
            SetPendingRequestModeOverride(CopilotAgentMode.Auto);
            InputText = decision.UserMessage;
            RunUiOperation(SendAsync, "继续 Agent 任务");
        }

        private void OpenAgentTask(CopilotAgentTaskSummary? task)
        {
            if (task == null || !CanSwitchConversation || !Conversations.Contains(task.Conversation))
                return;

            SelectConversation(task.Conversation, persist: true, preferredProfileId: task.Conversation.ProfileId);
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
                profile,
                CopilotCapabilityCatalog.Shared.GetSnapshot()).IsAvailable;
        }

        private void ResumeAgentTask(CopilotAgentTaskSummary? task)
        {
            if (!CanResumeAgentTask(task) || task == null)
                return;

            SelectConversation(task.Conversation, persist: true, preferredProfileId: task.Conversation.ProfileId);
            ContinueAgentTasks(task.Message);
        }

        private void DismissAgentTask(CopilotAgentTaskSummary? task)
        {
            if (task == null || IsBusy || !Conversations.Contains(task.Conversation))
                return;

            if (MessageBox.Show(
                Application.Current.GetActiveWindow(),
                $"放弃 Agent 任务“{task.Title}”？保存的继续状态会被清除。",
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            if (!CopilotAgentTaskIndex.Dismiss(task))
                return;
            if (ReferenceEquals(task.Conversation, SelectedConversation))
                PublishSelectedTaskEventJournal();
            PersistState();
            RefreshAgentTasks();
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

        private bool CanScheduleComposerRequest(CopilotAgentMode mode)
        {
            return EvaluateComposerRequestAdmission(mode).IsAllowed;
        }

        private bool CanScheduleConversationRequest(string? conversationId, CopilotAgentMode mode)
        {
            return _taskHost.EvaluateRequestAdmission(conversationId, mode).IsAllowed;
        }

        private CopilotRequestAdmissionResult EvaluateComposerRequestAdmission(CopilotAgentMode mode) =>
            _taskHost.EvaluateRequestAdmission(SelectedConversation?.Id, mode);

        private CopilotAgentMode ConsumeRequestModeOverride()
        {
            var mode = ResolveComposerRequestMode();
            _pendingRequestModeOverride = null;
            return mode;
        }

        private void SetPendingRequestModeOverride(CopilotAgentMode mode)
        {
            _pendingRequestModeOverride = mode == CopilotAgentMode.Auto ? null : mode;
            OnComposerRequestModeChanged();
        }

        private void ClearPendingRequestModeOverride()
        {
            if (_pendingRequestModeOverride == null)
                return;

            _pendingRequestModeOverride = null;
            OnComposerRequestModeChanged();
        }

        private void OnComposerRequestModeChanged()
        {
            OnPropertyChanged(nameof(PrimaryActionToolTip));
            RefreshComposerTokenEstimate();
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
            if (string.IsNullOrWhiteSpace(normalizedPrompt))
                return new CopilotPromptQueueResult(false, false);

            if (startNewConversation || SelectedConversation == null)
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
                AttachExternalContextSnapshot(
                    conversation,
                    contextAttachmentTitle,
                    contextAttachmentSourceId,
                    contextAttachmentItems);
            }

            SetPendingRequestModeOverride(mode);
            InputText = normalizedPrompt;

            if (!sendNow || !CanScheduleComposerRequest(mode))
                return new CopilotPromptQueueResult(true, false);

            RunUiOperation(SendAsync, "发送外部请求");
            return new CopilotPromptQueueResult(true, true);
        }

        private void CancelActiveRun()
        {
            if (_isCompactingConversation)
            {
                _compactConversationCts?.Cancel();
                return;
            }
            CancelCurrentReply(discardAgentCheckpoint: true);
        }

        private void CancelCurrentReply(bool discardAgentCheckpoint)
        {
            var selectedRun = SelectedHostedRun;
            if (selectedRun?.State == CopilotHostedRunState.Queued)
            {
                _taskHost.RequestCancel(selectedRun.Id);
                return;
            }

            var activeRun = ActiveHostedRun;
            if (!IsViewingActiveRun || activeRun == null)
                return;

            if (!discardAgentCheckpoint && activeRun.IsAgent && _taskHost.RequestPause(activeRun.Id))
                return;

            _taskHost.RequestCancel(activeRun.Id);
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
            RefreshMcpStatus();

            var conversation = Conversations.FirstOrDefault(item => item.Id == preferredConversationId)
                ?? Conversations.FirstOrDefault();

            SelectConversation(conversation, persist: false, preferredProfileId: requestedProfile?.Id);
            PersistState(immediate: true);
        }

        private CopilotConversationHistoryLimits ResolveConversationHistoryLimits(CopilotProfileConfig? profile)
        {
            return CopilotConversationRequestBuilder.ResolveHistoryLimits(
                _config.AgentDefaults.ContextWindowTokens,
                profile?.MaxTokens ?? CopilotProfileConfig.DefaultMaxTokens);
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsConversationEmpty));
            OnPropertyChanged(nameof(InputPlaceholder));
            RefreshCompactHistoryConversations();
            RefreshAgentTasks();
            RefreshComposerTokenEstimate();
            CommandManager.InvalidateRequerySuggested();
        }

        private void Conversations_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshCompactHistoryConversations();
            RefreshAgentTasks();
            RefreshConversationRunStatuses();
            OnPropertyChanged(nameof(Conversations));
            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshCompactHistoryConversations()
        {
            var history = Conversations
                .Where(CopilotConversationService.IsHistory)
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

        private void RefreshAgentTasks()
        {
            var tasks = CopilotAgentTaskIndex.Build(Conversations);
            AgentTasks.Clear();
            foreach (var task in tasks)
                AgentTasks.Add(task);

            OnPropertyChanged(nameof(HasAgentTasks));
            OnPropertyChanged(nameof(AgentTaskCountLabel));
            CommandManager.InvalidateRequerySuggested();
        }

        private int CountHistoryConversations()
        {
            return Conversations.Count(CopilotConversationService.IsHistory);
        }

        private void Attachments_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateChatAttachmentTokenEstimate();
            RefreshComposerTokenEstimate();
            OnCurrentLiveContextStateChanged();
        }

        private void SelectConversation(CopilotConversationRecord? conversation, bool persist, string? preferredProfileId = null)
        {
            if (ReferenceEquals(_selectedConversation, conversation))
            {
                if (!string.IsNullOrWhiteSpace(preferredProfileId))
                {
                    var preferredProfile = ResolveProfile(preferredProfileId) ?? ResolveProfile(_selectedConversation?.ProfileId);
                    SelectProfile(preferredProfile, syncConversation: true, persist: false);
                }
                return;
            }

            if (_selectedConversation != null)
                _selectedConversation.Attachments.CollectionChanged -= Attachments_CollectionChanged;

            if (_selectedConversation != null)
                _selectedConversation.Messages.CollectionChanged -= Messages_CollectionChanged;

            _selectedConversation = conversation;
            DismissLocalCommandResult();
            if (_selectedConversation != null)
                _selectedConversation.Attachments.CollectionChanged += Attachments_CollectionChanged;

            if (_selectedConversation != null)
                _selectedConversation.Messages.CollectionChanged += Messages_CollectionChanged;

            OnPropertyChanged(nameof(SelectedConversation));
            OnPropertyChanged(nameof(Messages));
            OnPropertyChanged(nameof(Attachments));
            OnPropertyChanged(nameof(HasAttachments));
            OnPropertyChanged(nameof(IsConversationEmpty));
            OnPropertyChanged(nameof(InputPlaceholder));
            NotifyHostedRunStateChanged();
            PublishSelectedTaskEventJournal();

            _state.ActiveConversationId = conversation?.Id ?? string.Empty;

            var profile = ResolveProfile(preferredProfileId)
                ?? ResolveProfile(conversation?.ProfileId)
                ?? ResolveProfile(_state.ActiveProfileId)
                ?? _config.GetPreferredDefaultProfile();

            SelectProfile(profile, syncConversation: false, persist: false);

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

            if (shouldPersist)
                PersistState();
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
            RefreshComposerTokenEstimate();
        }

        private void QueueConversationTitleGeneration(CopilotConversationRecord conversation, CopilotProfileConfig requestProfile)
        {
            if (!ShouldGenerateConversationTitle(conversation))
                return;

            _ = GenerateConversationTitleAsync(conversation, requestProfile.Clone());
        }

        private static bool ShouldGenerateConversationTitle(CopilotConversationRecord conversation)
        {
            if (conversation.HasCustomTitle)
                return false;

            var userMessageCount = conversation.Messages.Count(message => message.Role == CopilotChatRole.User && !string.IsNullOrWhiteSpace(message.Content));
            var assistantMessageCount = conversation.Messages.Count(message => message.Role == CopilotChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Content));
            return userMessageCount == 1 && assistantMessageCount == 1;
        }

        private async Task GenerateConversationTitleAsync(CopilotConversationRecord conversation, CopilotProfileConfig requestProfile)
        {
            var titlePrompt = BuildConversationTitlePrompt(conversation);
            if (string.IsNullOrWhiteSpace(titlePrompt))
                return;

            try
            {
                requestProfile.UseSystemPromptOverride("You are a conversation title generator. Generate a short, natural English title for the given conversation. Return only the title itself, with no explanation.");
                requestProfile.MaxTokens = Math.Min(requestProfile.MaxTokens, 32);
                requestProfile.Temperature = 0.2;

                var titleBuilder = new StringBuilder();
                await _chatService.StreamReplyAsync(
                    requestProfile,
                    new[]
                    {
                        new CopilotRequestMessage("user", titlePrompt),
                    },
                    delta =>
                    {
                        if (delta.HasContent)
                            titleBuilder.Append(delta.Content);
                    },
                    CancellationToken.None);

                var generatedTitle = NormalizeGeneratedTitle(titleBuilder.ToString());
                if (string.IsNullOrWhiteSpace(generatedTitle) || Application.Current == null)
                    return;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!Conversations.Contains(conversation) || conversation.HasCustomTitle)
                        return;

                    conversation.SetGeneratedTitle(generatedTitle);
                    PersistState();
                });
            }
            catch
            {
            }
        }

        private void BringConversationToFront(CopilotConversationRecord conversation)
        {
            CopilotConversationService.MoveToPreferredIndex(Conversations, conversation);
            _state.ActiveConversationId = conversation.Id;
        }

        private void RenameConversation(CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return;

            var window = new CopilotTextInputWindow("Rename Chat", "Enter a new chat name", conversation.Title)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.ResultText))
                return;

            conversation.SetCustomTitle(window.ResultText);
            PersistState();
        }

        private void DeleteConversation(CopilotConversationRecord? conversation)
        {
            if (conversation == null || IsBusy)
                return;

            if (MessageBox.Show(
                Application.Current.GetActiveWindow(),
                $"Delete chat \"{conversation.Title}\"?",
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            RemoveManagedAttachmentFiles(conversation.EnumerateReferencedAttachments());
            if (string.Equals(conversation.Id, _agentRunNoticeConversationId, StringComparison.Ordinal))
                ClearAgentRunNotice();

            var currentIndex = Conversations.IndexOf(conversation);
            Conversations.Remove(conversation);

            if (Conversations.Count == 0)
            {
                var replacement = CreateConversation();
                SelectConversation(replacement, persist: false);
            }
            else
            {
                var nextIndex = Math.Clamp(currentIndex, 0, Conversations.Count - 1);
                SelectConversation(Conversations[nextIndex], persist: false);
            }

            PersistState();
        }

        private void TogglePinConversation(CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return;

            conversation.IsPinned = !conversation.IsPinned;
            CopilotConversationService.MoveToPreferredIndex(Conversations, conversation);
            PersistState();
        }

        private void AddFileAttachment()
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                CheckFileExists = true,
                Filter = "All files|*.*",
            };

            if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true)
                return;

            AddFileAttachments(dialog.FileNames);
        }

        public int AddFileAttachments(IEnumerable<string>? filePaths)
        {
            if (IsBusy || filePaths == null)
                return 0;

            var normalizedPaths = filePaths
                .Where(filePath => !string.IsNullOrWhiteSpace(filePath))
                .Select(TryNormalizeFilePath)
                .Where(filePath => filePath != null && File.Exists(filePath))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalizedPaths.Length == 0)
                return 0;

            var conversation = EnsureConversation();
            var addedCount = 0;
            foreach (var filePath in normalizedPaths)
            {
                if (conversation.Attachments.Any(item => item.Type == CopilotAttachmentType.File && string.Equals(item.Value, filePath, StringComparison.OrdinalIgnoreCase)))
                    continue;

                conversation.Attachments.Add(CopilotAttachmentItem.CreateFile(filePath));
                addedCount++;
            }

            if (addedCount > 0)
                UpdateAttachmentsState(conversation);
            return addedCount;
        }

        private static string? TryNormalizeFilePath(string filePath)
        {
            try
            {
                return Path.GetFullPath(filePath.Trim());
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or System.Security.SecurityException)
            {
                return null;
            }
        }

        private void AddContextAttachment()
        {
            var conversation = EnsureConversation();
            var window = new CopilotTextInputWindow("Attach Context", "Enter the context to attach to this chat", string.Empty, isMultiline: true)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.ResultText))
                return;

            conversation.Attachments.Add(CopilotAttachmentItem.CreateContext(window.ResultText));
            UpdateAttachmentsState(conversation);
        }

        private void AttachCurrentLiveContext()
        {
            var liveContext = _currentLiveContext;
            if (liveContext == null || liveContext.SnapshotItems == null || liveContext.SnapshotItems.Count == 0)
                return;

            var conversation = EnsureConversation();
            AttachExternalContextSnapshot(
                conversation,
                string.IsNullOrWhiteSpace(liveContext.AttachmentTitle) ? liveContext.Title : liveContext.AttachmentTitle,
                liveContext.SourceId,
                liveContext.SnapshotItems);
        }

        private async Task AddWebPageAttachmentAsync()
        {
            var conversation = EnsureConversation();
            var window = new CopilotTextInputWindow("Attach Web Page", "Enter the web page URL to fetch and attach", "https://")
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.ResultText))
                return;

            var url = NormalizeWebPageUrl(window.ResultText);
            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "The web page URL is invalid.",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                var webPage = await LoadWebPageContentAsync(url, CancellationToken.None);
                var attachment = CopilotAttachmentItem.CreateWebPage(url, webPage.Title, BuildStoredWebPageContent(webPage));

                var existingAttachment = conversation.Attachments.FirstOrDefault(item => item.Type == CopilotAttachmentType.WebPage && string.Equals(item.Source, url, StringComparison.OrdinalIgnoreCase));
                if (existingAttachment != null)
                {
                    existingAttachment.Title = attachment.Title;
                    existingAttachment.Value = attachment.Value;
                    existingAttachment.Source = attachment.Source;
                    existingAttachment.CreatedAt = attachment.CreatedAt;
                }
                else
                {
                    conversation.Attachments.Add(attachment);
                }

                UpdateAttachmentsState(conversation);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"Failed to fetch web page: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void PasteImageAttachment()
        {
            if (TryPasteClipboardImageAttachment())
                return;

            MessageBox.Show(
                Application.Current.GetActiveWindow(),
                "The clipboard does not contain an image that can be attached.",
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        public bool TryPasteClipboardImageAttachment()
        {
            if (IsBusy)
                return false;

            try
            {
                if (!Clipboard.ContainsImage())
                    return false;

                var image = Clipboard.GetImage();
                if (image == null)
                    return false;

                var conversation = EnsureConversation();
                var imagePath = SaveClipboardImage(image);
                var title = $"Pasted Image {DateTime.Now:HH:mm:ss}";
                conversation.Attachments.Add(CopilotAttachmentItem.CreateImage(imagePath, title));
                UpdateAttachmentsState(conversation);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"Failed to paste image: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
        }

        private void CopyMessage(CopilotChatMessage? message)
        {
            if (message == null)
                return;

            var text = BuildMessageClipboardText(message);
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"Failed to copy message: {CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private bool CanRegenerateMessage(CopilotChatMessage? message)
        {
            if (IsBusy || message == null || SelectedConversation == null || SelectedProfile == null || !SelectedProfile.IsConfigured)
                return false;

            return TryResolveLatestTurn(message, out var conversation, out _, out var assistantMessage)
                && !CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                    conversation,
                    assistantMessage,
                    SelectedProfile,
                    CopilotCapabilityCatalog.Shared.GetSnapshot());
        }

        private async Task RetryMessageAsync(CopilotChatMessage? message, bool refreshWebContext)
        {
            if (!TryResolveLatestTurn(message, out var conversation, out var userMessage, out var assistantMessage))
                return;

            if (SelectedProfile == null || !SelectedProfile.IsConfigured)
            {
                OpenSettings();
                return;
            }
            if (CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                conversation,
                assistantMessage,
                SelectedProfile,
                CopilotCapabilityCatalog.Shared.GetSnapshot()))
            {
                return;
            }

            var prompt = (userMessage.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            var requestProfile = SelectedProfile.Clone();
            conversation.ProfileId = requestProfile.Id;
            conversation.ProfileDisplayName = requestProfile.DisplayLabel;
            conversation.AgentSessionCheckpoint = null;
            var turnSnapshot = CaptureHostedTurnSnapshot(conversation, userMessage);
            PersistState();

            var hostedRun = _taskHost.Start(
                conversation.Id,
                userMessage.RequestMode,
                run => ExecuteHostedRetryAsync(run, conversation, requestProfile, userMessage, assistantMessage, turnSnapshot, refreshWebContext));
            await AwaitHostedRunCompletionAsync(hostedRun);
        }

        private async Task ExecuteHostedRetryAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage? assistantMessage,
            CopilotAgentHostContextSnapshot turnSnapshot,
            bool refreshWebContext)
        {
            CopilotChatMessage? replacementAssistantMessage = null;
            try
            {
                if (assistantMessage != null)
                    conversation.Messages.Remove(assistantMessage);

                replacementAssistantMessage = CreatePendingAssistantMessage(requestProfile);
                conversation.Messages.Add(replacementAssistantMessage);
            }
            catch (Exception ex)
            {
                if (replacementAssistantMessage == null)
                {
                    replacementAssistantMessage = CreatePendingAssistantMessage(requestProfile);
                    conversation.Messages.Add(replacementAssistantMessage);
                }

                CopilotHostedTurnCompletion.CompleteFailure(conversation, replacementAssistantMessage, ex.Message, requestProfile.ApiKey);
                UpdateConversationMetadata(conversation, touch: true);
                await PersistStateAndFlushAsync();
                RefreshAgentTasks();
                return;
            }

            await ExecuteHostedPreparedTurnAsync(
                hostedRun,
                conversation,
                requestProfile,
                userMessage,
                replacementAssistantMessage,
                turnSnapshot,
                refreshWebContext);
        }

        private bool TryResolveLatestTurn(CopilotChatMessage? message, out CopilotConversationRecord conversation, out CopilotChatMessage userMessage, out CopilotChatMessage? assistantMessage)
        {
            conversation = SelectedConversation!;
            userMessage = null!;
            assistantMessage = null;

            if (message == null || SelectedConversation == null)
                return false;

            var messages = SelectedConversation.Messages;
            var targetIndex = messages.IndexOf(message);
            if (targetIndex < 0)
                return false;

            var userIndex = message.IsUser ? targetIndex : FindPreviousUserMessageIndex(messages, targetIndex - 1);
            if (userIndex < 0)
                return false;

            var resolvedAssistantIndex = userIndex + 1 < messages.Count && !messages[userIndex + 1].IsUser
                ? userIndex + 1
                : -1;

            if (!message.IsUser && resolvedAssistantIndex != targetIndex)
                return false;

            var turnEndIndex = resolvedAssistantIndex >= 0 ? resolvedAssistantIndex : userIndex;
            if (turnEndIndex != messages.Count - 1)
                return false;

            conversation = SelectedConversation;
            userMessage = messages[userIndex];
            assistantMessage = resolvedAssistantIndex >= 0 ? messages[resolvedAssistantIndex] : null;
            return true;
        }

        private static int FindPreviousUserMessageIndex(ObservableCollection<CopilotChatMessage> messages, int startIndex)
        {
            for (var index = startIndex; index >= 0; index--)
            {
                if (messages[index].IsUser)
                    return index;
            }

            return -1;
        }

        private static string BuildMessageClipboardText(CopilotChatMessage message)
        {
            var content = (message.Content ?? string.Empty).Trim();
            var execution = (message.ExecutionContent ?? string.Empty).Trim();
            var reasoning = (message.ReasoningContent ?? string.Empty).Trim();

            if (message.IsUser || string.IsNullOrWhiteSpace(execution) && string.IsNullOrWhiteSpace(reasoning))
                return content;

            var sections = new List<string>();

            if (!string.IsNullOrWhiteSpace(execution))
            {
                sections.Add("Execution:");
                sections.Add(execution);
            }

            if (!string.IsNullOrWhiteSpace(reasoning))
            {
                if (sections.Count > 0)
                    sections.Add(string.Empty);

                sections.Add("Reasoning:");
                sections.Add(reasoning);
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                if (sections.Count > 0)
                    sections.Add(string.Empty);

                sections.Add("Answer:");
                sections.Add(content);
            }

            return string.Join(Environment.NewLine, sections);
        }

        private void RemoveAttachment(CopilotAttachmentItem? attachment)
        {
            if (attachment == null || SelectedConversation == null)
                return;

            if (!SelectedConversation.Attachments.Remove(attachment))
                return;

            TryDeleteManagedAttachmentFile(attachment);

            UpdateAttachmentsState(SelectedConversation);
        }

        private static bool EnsureAssistantHeaders(CopilotConversationRecord conversation, CopilotProfileConfig? profile)
        {
            var assistantHeader = ResolveAssistantHeader(conversation, profile);
            var changed = false;

            foreach (var message in conversation.Messages)
            {
                if (message.IsUser || !string.IsNullOrWhiteSpace(message.AssistantName))
                    continue;

                message.AssistantName = assistantHeader;
                changed = true;
            }

            return changed;
        }

        private static string ResolveAssistantHeader(CopilotProfileConfig profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.Model))
                return profile.Model;

            if (!string.IsNullOrWhiteSpace(profile.DisplayLabel))
                return profile.DisplayLabel;

            return "AI";
        }

        private static CopilotChatMessage CreatePendingAssistantMessage(CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
            {
                AssistantName = ResolveAssistantHeader(profile),
            };
            assistantMessage.MarkThinkingStarted();
            return assistantMessage;
        }

        private static string ResolveAssistantHeader(CopilotConversationRecord conversation, CopilotProfileConfig? profile)
        {
            if (profile != null)
                return ResolveAssistantHeader(profile);

            if (!string.IsNullOrWhiteSpace(conversation.ProfileDisplayName))
                return conversation.ProfileDisplayName;

            if (!string.IsNullOrWhiteSpace(conversation.ProfileId))
                return conversation.ProfileId;

            return "AI";
        }

        private void RefreshComposerTokenEstimate()
        {
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

        private void InvalidateChatAttachmentTokenEstimate()
        {
            RefreshComposerTokenEstimate();
        }

        private string BuildActualUsageSummary(CopilotTokenUsage usage)
        {
            return $"Last request: input {CopilotTokenUsage.FormatCount(usage.InputTokens)} · output {CopilotTokenUsage.FormatCount(usage.OutputTokens)} · total {CopilotTokenUsage.FormatCount(usage.EffectiveTotalTokens)}";
        }

        private string BuildActualUsageDetails(CopilotConversationRecord conversation, CopilotTokenUsage usage)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Model: {ResolveUsageModelLabel(conversation)}");
            builder.AppendLine($"Input tokens: {CopilotTokenUsage.FormatCount(usage.InputTokens)}");
            builder.AppendLine($"Output tokens: {CopilotTokenUsage.FormatCount(usage.OutputTokens)}");
            builder.AppendLine($"Total tokens: {CopilotTokenUsage.FormatCount(usage.EffectiveTotalTokens)}");
            builder.AppendLine();
            builder.Append("Note: this shows the most recent usage returned by the API.");

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

        private static string BuildConversationTitlePrompt(CopilotConversationRecord conversation)
        {
            var firstUserMessage = conversation.Messages.FirstOrDefault(message => message.Role == CopilotChatRole.User && !string.IsNullOrWhiteSpace(message.Content));
            var firstAssistantMessage = conversation.Messages.FirstOrDefault(message => message.Role == CopilotChatRole.Assistant && !string.IsNullOrWhiteSpace(message.Content));
            if (firstUserMessage == null || firstAssistantMessage == null)
                return string.Empty;

            return string.Join(Environment.NewLine, new[]
            {
                "Generate a short English title for the conversation below.",
                "Requirements: 3 to 8 words, return only the title, no explanation, no quotes, no trailing period.",
                $"User: {TruncateForTitlePrompt(firstUserMessage.Content, 180)}",
                $"Assistant: {TruncateForTitlePrompt(firstAssistantMessage.Content, 260)}",
            });
        }

        private static string NormalizeGeneratedTitle(string rawTitle)
        {
            var title = (rawTitle ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            title = title.Trim('"', '\'', '“', '”', '‘', '’', '《', '》', '【', '】', '「', '」');

            if (title.StartsWith("\u6807\u9898", StringComparison.Ordinal))
            {
                var separatorIndex = title.IndexOfAny(new[] { ':', '：', '-', ' ' });
                if (separatorIndex >= 0 && separatorIndex < title.Length - 1)
                    title = title[(separatorIndex + 1)..].Trim();
            }

            if (title.Length > 18)
                title = title[..18].Trim();

            return title;
        }

        private static string TruncateForTitlePrompt(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
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

        private void PersistState(bool immediate = false)
        {
            PublishSelectedTaskEventJournal();
            _stateSaveScheduler.RequestSave(immediate);
            OnPropertyChanged(nameof(HasAttachments));
        }

        private async Task PersistStateAndFlushAsync()
        {
            PersistState(immediate: true);
            try
            {
                await _stateSaveScheduler.FlushAsync();
            }
            catch (Exception)
            {
                // The scheduler has already published the persistence failure. Keep the completed
                // Agent turn usable in memory; a later state change or flush will retry the snapshot.
            }
        }

        private async Task SaveStateSnapshotAsync(CancellationToken cancellationToken)
        {
            var dispatcher = Application.Current?.Dispatcher;
            string serializedState;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                serializedState = _stateStore.Serialize(_state);
            }
            else
            {
                var captureOperation = dispatcher.InvokeAsync(
                    () => _stateStore.Serialize(_state),
                    DispatcherPriority.Background,
                    cancellationToken);
                serializedState = await captureOperation.Task.ConfigureAwait(false);
            }

            await _stateStore.SaveSerializedAsync(serializedState, cancellationToken).ConfigureAwait(false);
        }

        private void Application_Exit(object? sender, ExitEventArgs e)
        {
            _taskHost.Shutdown();
            _stateSaveScheduler.Dispose();
            PublishSelectedTaskEventJournal();
            try
            {
                _stateStore.Save(_state);
            }
            catch (Exception exception)
            {
                ReportStatePersistenceError(exception);
            }
            finally
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) == 1)
                return;

            if (Application.Current != null)
                Application.Current.Exit -= Application_Exit;
            WorkspaceManager.ContentIdSelected -= WorkspaceManager_ContentIdSelected;
            CopilotLiveContextRegistry.CurrentChanged -= CopilotLiveContextRegistry_CurrentChanged;
            CopilotMcpConfirmationStore.Instance.ActionsChanged -= ConfirmationStore_ActionsChanged;
            CopilotMcpConfirmationStore.Instance.ActionStatusChanged -= ConfirmationStore_ActionStatusChanged;
            WeakEventManager<CopilotAgentTaskHost, CopilotAgentTaskHostChangedEventArgs>.RemoveHandler(_taskHost, nameof(CopilotAgentTaskHost.Changed), TaskHost_Changed);

            Conversations.CollectionChanged -= Conversations_CollectionChanged;
            if (_selectedConversation != null)
            {
                _selectedConversation.Attachments.CollectionChanged -= Attachments_CollectionChanged;
                _selectedConversation.Messages.CollectionChanged -= Messages_CollectionChanged;
            }
            if (_selectedProfile != null)
                _selectedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;

            _pendingActionExpiryTimer.Stop();
            _pendingActionFeedbackCts?.Cancel();
            _pendingActionFeedbackCts = null;
            _compactConversationCts?.Cancel();
            _stateSaveScheduler.Dispose();
            GC.SuppressFinalize(this);
        }

        private void ReportStatePersistenceError(Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Copilot state persistence failed: {exception}");
            var safeError = CopilotUserFacingErrorFormatter.Sanitize(exception.Message);
            var stateDirectory = _stateStore is CopilotChatStateStore stateStore
                ? stateStore.StateDirectoryPath
                : string.Empty;
            var tooltip = "当前会话仍保留在内存中；下一次会话变更或显式刷新会再次尝试保存。";
            if (!string.IsNullOrWhiteSpace(safeError))
                tooltip += $"{Environment.NewLine}{Environment.NewLine}错误：{safeError}";
            if (!string.IsNullOrWhiteSpace(stateDirectory))
                tooltip += $"{Environment.NewLine}{Environment.NewLine}状态目录：{stateDirectory}";

            UpdateStatePersistenceNotice("会话保存失败；请暂时不要关闭程序，Copilot 将在下一次变更时重试。", tooltip);
        }

        private void ReportStatePersistenceSuccess() => UpdateStatePersistenceNotice(string.Empty, string.Empty);

        private void UpdateStatePersistenceNotice(string text, string tooltip)
        {
            if (Volatile.Read(ref _disposeState) == 1)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                    dispatcher.BeginInvoke(new Action(() => UpdateStatePersistenceNotice(text, tooltip)));
                return;
            }

            StatePersistenceNoticeText = text;
            StatePersistenceNoticeToolTip = tooltip;
        }

        private void PublishSelectedTaskEventJournal()
        {
            var conversation = SelectedConversation;
            var journal = conversation?.AgentSessionCheckpoint?.TaskEventJournal;
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
            OnPropertyChanged(nameof(Attachments));
            OnPropertyChanged(nameof(HasAttachments));
            InvalidateChatAttachmentTokenEstimate();
            RefreshComposerTokenEstimate();
            PersistState();
            OnCurrentLiveContextStateChanged();
        }

        private void ConsumeComposerAttachments(CopilotConversationRecord conversation)
        {
            if (conversation.Attachments.Count == 0)
                return;

            conversation.Attachments.Clear();
            UpdateAttachmentsState(conversation);
        }

        private void AttachExternalContextSnapshot(
            CopilotConversationRecord conversation,
            string? attachmentTitle,
            string? attachmentSourceId,
            IReadOnlyList<CopilotContextItem> contextItems)
        {
            var content = CopilotConversationRequestBuilder.BuildContextAttachmentContent(contextItems);
            if (string.IsNullOrWhiteSpace(content))
                return;

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

            var attachment = CopilotAttachmentItem.CreateContext(content, normalizedTitle, attachmentSourceId);
            if (existingAttachment != null)
            {
                existingAttachment.Title = attachment.Title;
                existingAttachment.Value = attachment.Value;
                existingAttachment.Source = attachment.Source;
                existingAttachment.CreatedAt = attachment.CreatedAt;
            }
            else
            {
                conversation.Attachments.Add(attachment);
            }

            UpdateAttachmentsState(conversation);
        }

        private static string BuildStoredWebPageContent(CopilotFetchedWebPageContent page) =>
            CopilotWebPageToolSupport.BuildStoredWebPageContent(page);

        private string SaveClipboardImage(BitmapSource image)
        {
            Directory.CreateDirectory(_stateStore.AttachmentDirectoryPath);

            var filePath = Path.Combine(
                _stateStore.AttachmentDirectoryPath,
                $"clipboard-{DateTime.Now:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.png");

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            encoder.Save(stream);

            return filePath;
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

            CopilotChatStateStore.TryDeleteManagedAttachmentFile(_stateStore.AttachmentDirectoryPath, attachment.Value);
        }

        private static string NormalizeWebPageUrl(string value) => CopilotWebPageToolSupport.NormalizeWebPageUrl(value);

        private static Task<CopilotFetchedWebPageContent> LoadWebPageContentAsync(string url, CancellationToken cancellationToken) =>
            CopilotWebPageToolSupport.LoadWebPageContentAsync(url, cancellationToken);

    }
}

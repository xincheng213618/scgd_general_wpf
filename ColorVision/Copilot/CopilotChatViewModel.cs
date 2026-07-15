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
    public class CopilotChatViewModel : ViewModelBase
    {
        private const int AttachmentContentLimit = 12000;
        private const int MaxWebPageInjectionChars = 8000;
        private const int CompactHistoryLimit = 4;
        private const int CompactSummaryOutputTokens = 4096;
        private static readonly TimeSpan RecentMcpFailureWindow = TimeSpan.FromMinutes(15);
        private const string CompactSystemPrompt = "You compact an existing conversation for seamless continuation. Preserve the user's active goal, constraints, decisions, verified facts, relevant files, commands and results, unfinished work, blockers, and safe next steps. Remove greetings, repetition, obsolete exploration, and verbose tool traces. Never invent facts or treat historical actions as current authorization. Return only a concise Markdown continuation summary.";

        private readonly CopilotChatService _chatService;
        private readonly CopilotAgentContextBuilder _agentContextBuilder;
        private readonly ICopilotAgentRuntime _agentRuntime;
        private readonly CopilotAgentTaskHost _taskHost;
        private readonly CopilotContextRegistry _contextRegistry;
        private readonly CopilotConfig _config;
        private readonly ICopilotChatStateStore _stateStore;
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
        private string _localCommandResultTitle = string.Empty;
        private string _localCommandResultText = string.Empty;
        private bool _hasPendingMcpActions;
        private bool _hasRecentMcpFailures;
        private bool _isCompactingConversation;

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
            _agentContextBuilder = new CopilotAgentContextBuilder();
            var toolRegistry = CopilotToolRegistry.CreateDefault();
            var toolExecutor = new CopilotToolExecutor();
            var agentFrameworkRuntime = new CopilotMicrosoftAgentFrameworkRuntime(toolRegistry, _agentContextBuilder, toolExecutor);
            _agentRuntime = new CopilotAgentRuntimeRouter(agentFrameworkRuntime);
            _taskHost = CopilotAgentTaskHost.Shared;
            _contextRegistry = CopilotContextRegistry.CreateDefault();
            _config = CopilotConfig.Instance;
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _currentLiveContext = CopilotLiveContextRegistry.Current;

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
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            OpenMcpSettingsCommand = new RelayCommand(_ => OpenMcpSettings());
            AddFileAttachmentCommand = new RelayCommand(_ => AddFileAttachment(), _ => !IsBusy);
            AddContextAttachmentCommand = new RelayCommand(_ => AddContextAttachment(), _ => !IsBusy);
            AddWebPageAttachmentCommand = new RelayCommand(_ => _ = AddWebPageAttachmentAsync(), _ => !IsBusy);
            PasteImageAttachmentCommand = new RelayCommand(_ => PasteImageAttachment(), _ => !IsBusy);
            AttachCurrentLiveContextCommand = new RelayCommand(_ => AttachCurrentLiveContext(), _ => HasCurrentLiveContext);
            CopyMessageCommand = new RelayCommand<CopilotChatMessage>(CopyMessage, message => message != null);
            RetryMessageCommand = new RelayCommand<CopilotChatMessage>(message => _ = RetryMessageAsync(message, refreshWebContext: false), CanRegenerateMessage);
            RefreshMessageCommand = new RelayCommand<CopilotChatMessage>(message => _ = RetryMessageAsync(message, refreshWebContext: true), CanRegenerateMessage);
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
            ApprovePendingActionCommand = new RelayCommand<ConfirmableAction>(action => _ = ApprovePendingActionAsync(action), action => action?.IsPending == true);
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

        public ICommand OpenMcpSettingsCommand { get; }

        public ICommand AddFileAttachmentCommand { get; }

        public ICommand AddContextAttachmentCommand { get; }

        public ICommand AddWebPageAttachmentCommand { get; }

        public ICommand PasteImageAttachmentCommand { get; }

        public ICommand AttachCurrentLiveContextCommand { get; }

        public string AttachmentMenuToolTip => IsBusy
            ? "Attachments are locked while the assistant is responding."
            : "Attach input: paste an image, add a web page, add a file, or add context text.";

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
                    return CanScheduleComposerRequest(ResolveComposerRequestMode())
                        ? $"加入 Agent 队列（当前等待 {_taskHost.QueuedCount}/{_taskHost.MaxQueuedRuns}）"
                        : $"Agent 队列已满（{_taskHost.QueuedCount}/{_taskHost.MaxQueuedRuns}）";
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

        public string InputPlaceholder => IsConversationEmpty ? "随心输入 · 输入 / 查看本地命令" : "要求后续变更 · 输入 / 查看本地命令";

        public bool IsInputEmpty => string.IsNullOrWhiteSpace(InputText);

        public IReadOnlyList<CopilotLocalCommand> LocalCommandSuggestions => CopilotLocalCommandCatalog.Suggest(InputText);

        public bool HasLocalCommandSuggestions => LocalCommandSuggestions.Count > 0;

        public bool TryCompleteLocalCommand(CopilotLocalCommand? command = null)
        {
            var suggestions = LocalCommandSuggestions;
            command ??= suggestions.Count > 0 ? suggestions[0] : null;
            if (command == null)
                return false;

            InputText = command.Name;
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
            && (SelectedHostedRun?.State is CopilotHostedRunState.Running or CopilotHostedRunState.PauseRequested);

        public bool CanPauseAgentRun => IsViewingActiveRun && IsAgentRequestActive && ActiveHostedRun?.IsCheckpointReady == true;

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
                case CopilotLocalCommandKind.Context:
                    ShowLocalCommandResult(command, BuildContextDiagnosticsReport());
                    break;
                case CopilotLocalCommandKind.Skills:
                    ShowLocalCommandResult(command, BuildAgentSkillDiagnosticsReport());
                    break;
                case CopilotLocalCommandKind.Mcp:
                    ShowLocalCommandResult(command, McpStatusToolTip);
                    break;
                case CopilotLocalCommandKind.Compact:
                    _ = CompactConversationAsync(command, invocation.Arguments);
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
                ShowLocalCommandResult(command, "压缩失败：" + CopilotMcpAuditLogger.RedactText(ex.Message));
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
            var history = CaptureConversationHistorySelection();
            var projectInstructions = Array.Empty<CopilotProjectInstructionDocument>();
            CopilotAgentSkillUsageSnapshot? skillUsage = null;
            if (agentContextEnabled)
            {
                var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
                var searchRoots = BuildSearchRootPaths(turnSnapshot, Array.Empty<string>());
                projectInstructions = CopilotAgentProjectInstructions.Discover(searchRoots, turnSnapshot.ActiveDocumentPath).ToArray();
                skillUsage = CopilotAgentSkillUsageStore.Shared.GetSnapshot();
            }

            var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
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
            });
        }

        private void DismissLocalCommandResult()
        {
            LocalCommandResultTitle = string.Empty;
            LocalCommandResultText = string.Empty;
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
            };
            var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
            {
                AssistantName = ResolveAssistantHeader(requestProfile),
            };
            assistantMessage.MarkThinkingStarted();

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
            InputText = string.Empty;
            await hostedRun.Completion;
            if (!hostedRun.HasStarted)
                FinalizeCancelledQueuedRun(conversation, assistantMessage);
        }

        private void FinalizeCancelledQueuedRun(CopilotConversationRecord conversation, CopilotChatMessage assistantMessage)
        {
            assistantMessage.IsExecutionInProgress = false;
            assistantMessage.IsReasoningInProgress = false;
            assistantMessage.AgentStopReason = CopilotAgentStopReason.Cancelled;
            assistantMessage.MarkThinkingCompleted();
            SetAssistantFallbackContent(assistantMessage, "排队的 Agent 任务已取消，未调用模型或工具。");

            UpdateConversationMetadata(conversation, touch: true);
            PersistState();
            RefreshAgentTasks();
        }

        private async Task ExecuteHostedTurnAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            CopilotHostedTurnSnapshot turnSnapshot)
        {
            try
            {
                var usage = await RunConversationTurnAsync(hostedRun, conversation, requestProfile, userMessage, assistantMessage, turnSnapshot, refreshExternalContext: true);
                FinalizeAssistantMessage(assistantMessage);
                UpdateConversationUsage(conversation, usage);

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
                QueueConversationTitleGeneration(conversation, requestProfile);
            }
            catch (OperationCanceledException)
            {
                assistantMessage.IsExecutionInProgress = false;
                assistantMessage.IsReasoningInProgress = false;
                assistantMessage.MarkThinkingCompleted();

                var controlIntent = hostedRun.RunControl?.Intent ?? CopilotAgentControlIntent.None;
                if (controlIntent == CopilotAgentControlIntent.Cancel)
                {
                    conversation.AgentSessionCheckpoint = null;
                    assistantMessage.AgentStopReason = CopilotAgentStopReason.Cancelled;
                }
                else if (controlIntent == CopilotAgentControlIntent.Pause)
                {
                    assistantMessage.AgentStopReason = CopilotAgentStopReason.Paused;
                }

                if (string.IsNullOrWhiteSpace(assistantMessage.Content))
                {
                    SetAssistantFallbackContent(assistantMessage, controlIntent == CopilotAgentControlIntent.Pause
                        ? "Agent 任务已暂停；可从最近一次可用 checkpoint 继续。"
                        : "The current reply was cancelled.");
                }

                UpdateConversationUsage(conversation, CopilotTokenUsage.Empty);

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            catch (Exception ex)
            {
                assistantMessage.IsExecutionInProgress = false;
                assistantMessage.IsReasoningInProgress = false;
                assistantMessage.MarkThinkingCompleted();
                SetAssistantFallbackContent(assistantMessage, $"Request failed: {ex.Message}");

                UpdateConversationUsage(conversation, CopilotTokenUsage.Empty);

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
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
            CopilotHostedTurnSnapshot turnSnapshot,
            bool refreshExternalContext)
        {
            var cancellationToken = hostedRun.CancellationToken;

            if (userMessage.RequestMode == CopilotAgentMode.Chat)
            {
                conversation.AgentSessionCheckpoint = null;
                PersistState();
                return await RunChatTurnAsync(requestProfile, userMessage, assistantMessage, refreshExternalContext, cancellationToken);
            }

            return await RunAgentTurnAsync(hostedRun, conversation, requestProfile, userMessage, assistantMessage, turnSnapshot, refreshExternalContext, cancellationToken);
        }

        private async Task<CopilotTokenUsage> RunChatTurnAsync(
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            bool refreshExternalContext,
            CancellationToken cancellationToken)
        {
            var prompt = (userMessage.Content ?? string.Empty).Trim();
            if (refreshExternalContext || string.IsNullOrWhiteSpace(userMessage.RequestContent))
                userMessage.RequestContent = await BuildUserRequestContentAsync(prompt, cancellationToken);

            var history = BuildConversationHistory(requestProfile, includeAttachmentContext: true);
            return await _chatService.StreamReplyAsync(
                requestProfile,
                history,
                delta => ApplyAssistantDelta(assistantMessage, delta),
                cancellationToken);
        }

        private async Task<CopilotTokenUsage> RunAgentTurnAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            CopilotHostedTurnSnapshot turnSnapshot,
            bool refreshExternalContext,
            CancellationToken cancellationToken)
        {
            if (!refreshExternalContext && !string.IsNullOrWhiteSpace(userMessage.RequestContent))
            {
                assistantMessage.MarkThinkingStarted();
                assistantMessage.IsExecutionInProgress = true;
                assistantMessage.IsExecutionExpanded = true;

                var history = BuildVisibleConversationHistory(conversation, userMessage, requestProfile);
                history.Add(new CopilotRequestMessage("user", userMessage.RequestContent.Trim()));

                return await _chatService.StreamReplyAsync(
                    requestProfile,
                    history,
                    delta => ApplyAssistantDelta(assistantMessage, delta),
                    cancellationToken);
            }

            var explicitLocalPaths = CopilotLocalFileToolSupport.ExtractExplicitLocalFilePaths(userMessage.Content);
            var explicitLocalDirectoryPaths = explicitLocalPaths
                .Where(IsExistingDirectoryPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var explicitLocalFilePaths = explicitLocalPaths
                .Where(path => !IsExistingDirectoryPath(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var searchRootPaths = BuildSearchRootPaths(turnSnapshot, explicitLocalPaths);
            var writableLocalRootPaths = BuildWritableLocalRootPaths(turnSnapshot);
            var writableLocalFilePaths = BuildWritableLocalFilePaths(turnSnapshot, explicitLocalFilePaths);
            var projectInstructions = userMessage.RequestMode == CopilotAgentMode.Chat
                ? Array.Empty<CopilotProjectInstructionDocument>()
                : CopilotAgentProjectInstructions.Discover(searchRootPaths, turnSnapshot.ActiveDocumentPath);
            IReadOnlyList<CopilotContextItem> contextItems = await _contextRegistry.CaptureAsync(
                new CopilotContextRequest
                {
                    Scope = MapContextScope(userMessage.RequestMode),
                    UserText = (userMessage.Content ?? string.Empty).Trim(),
                    SolutionDirectoryPath = turnSnapshot.SolutionDirectoryPath,
                    ActiveDocumentPath = turnSnapshot.ActiveDocumentPath,
                    SearchRootPaths = searchRootPaths,
                },
                cancellationToken);

            contextItems = MergeCurrentLiveContextSummary(contextItems);
            var sessionCheckpoint = conversation.AgentSessionCheckpoint;
            var copilotConfig = CopilotConfig.Instance;
            var agentDefaults = copilotConfig.AgentDefaults.Clone();

            var agentRequest = new CopilotAgentRequest
            {
                UserText = (userMessage.Content ?? string.Empty).Trim(),
                Profile = requestProfile,
                History = BuildVisibleConversationHistory(conversation, userMessage, requestProfile),
                Attachments = turnSnapshot.Attachments,
                ContextItems = contextItems,
                SearchRootPaths = searchRootPaths,
                ActiveDocumentPath = turnSnapshot.ActiveDocumentPath,
                ProjectInstructions = projectInstructions,
                ReadableLocalFilePaths = explicitLocalFilePaths,
                ReadableLocalDirectoryPaths = explicitLocalDirectoryPaths,
                WritableLocalRootPaths = writableLocalRootPaths,
                WritableLocalFilePaths = writableLocalFilePaths,
                PreferBatchReadLocalFiles = explicitLocalDirectoryPaths.Length > 0 && explicitLocalFilePaths.Length == 0,
                PreferredShell = agentDefaults.PreferredShell,
                Mode = userMessage.RequestMode,
                SessionCheckpoint = sessionCheckpoint,
                Recovery = sessionCheckpoint == null ? null : userMessage.RecoveryRequest,
                RunControl = hostedRun.RunControl,
                RunBudgetDefaults = agentDefaults.CreateRunBudgetDefaults(),
                SkillOverrides = agentDefaults.CreateSkillOverrideSnapshot(),
                ExternalMcpServers = copilotConfig.ExternalMcpServers
                    .Where(server => server?.Enabled == true)
                    .Select(server => server.Clone())
                    .ToArray(),
            };

            CopilotAgentRunResult result;
            try
            {
                result = await _agentRuntime.RunAsync(
                    agentRequest,
                    agentEvent => ApplyAgentEvent(hostedRun, conversation, assistantMessage, agentEvent),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (hostedRun.RunControl?.Intent == CopilotAgentControlIntent.Pause && sessionCheckpoint != null)
            {
                conversation.AgentSessionCheckpoint ??= sessionCheckpoint;
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
                    PersistState();
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
                SetAssistantFallbackContent(assistantMessage, result.StopReason switch
                {
                    CopilotAgentStopReason.Paused => "Agent 任务已暂停；当前任务状态已经保存，可以稍后继续。",
                    CopilotAgentStopReason.Cancelled => "Agent 任务已取消；本轮新 checkpoint 已丢弃。",
                    _ => assistantMessage.Content,
                });
            }
            PersistState();
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
                && e.Run.State == CopilotHostedRunState.CancelRequested)
            {
                var conversation = Conversations.FirstOrDefault(item => string.Equals(item.Id, e.Run.ConversationId, StringComparison.Ordinal));
                if (conversation?.AgentSessionCheckpoint != null)
                {
                    conversation.AgentSessionCheckpoint = null;
                    PersistState();
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
                SetPendingActionFeedback($"Copy failed: {ex.Message}");
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
                SetPendingActionFeedback($"Copy failed: {ex.Message}");
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
                CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out var message);
                SetPendingActionFeedback($"{action.ActionId}: {message} The agent will resume in the same session.");
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
                    case ConfirmableActionStatus.Executed:
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
        }

        private void ClearPendingActionFeedback(CancellationTokenSource cts)
        {
            if (!ReferenceEquals(_pendingActionFeedbackCts, cts))
                return;

            _pendingActionFeedbackCts = null;
            PendingActionFeedbackText = string.Empty;
            cts.Dispose();
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

        private IReadOnlyList<CopilotContextItem> MergeCurrentLiveContextSummary(IReadOnlyList<CopilotContextItem> contextItems)
        {
            var liveContextItem = BuildCurrentLiveContextSummaryItem();
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

        private CopilotContextItem? BuildCurrentLiveContextSummaryItem()
        {
            var liveContext = _currentLiveContext;
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

        private CopilotHostedTurnSnapshot CaptureHostedTurnSnapshot(CopilotConversationRecord conversation)
        {
            return CaptureHostedTurnSnapshot(conversation.Attachments);
        }

        private CopilotHostedTurnSnapshot CaptureHostedTurnSnapshot(IEnumerable<CopilotAttachmentItem> attachments)
        {
            var attachmentSnapshot = attachments
                .Select(CloneAttachment)
                .ToArray();
            return new CopilotHostedTurnSnapshot(
                _activeDocumentPath,
                SolutionManager.GetInstance().CurrentSolutionExplorer?.DirectoryInfo?.FullName ?? string.Empty,
                attachmentSnapshot);
        }

        private static CopilotAttachmentItem CloneAttachment(CopilotAttachmentItem source)
        {
            return new CopilotAttachmentItem
            {
                Id = source.Id,
                Type = source.Type,
                Title = source.Title,
                Value = source.Value,
                Source = source.Source,
                CreatedAt = source.CreatedAt,
            };
        }

        private static IReadOnlyList<string> BuildSearchRootPaths(
            CopilotHostedTurnSnapshot turnSnapshot,
            IReadOnlyList<string> explicitLocalFilePaths)
        {
            var roots = new List<string>();

            AddSearchCandidate(roots, turnSnapshot.SolutionDirectoryPath);
            AddSearchCandidate(roots, turnSnapshot.ActiveDocumentPath);

            foreach (var path in explicitLocalFilePaths)
            {
                AddSearchCandidate(roots, path);
            }

            foreach (var attachment in turnSnapshot.Attachments.Where(item => item.Type == CopilotAttachmentType.File && !string.IsNullOrWhiteSpace(item.Value)))
            {
                AddSearchCandidate(roots, attachment.Value);
            }

            return CopilotWorkspaceSearchSupport.NormalizeSearchRoots(roots);
        }

        private static IReadOnlyList<string> BuildWritableLocalRootPaths(CopilotHostedTurnSnapshot turnSnapshot)
        {
            return CopilotWorkspaceSearchSupport.NormalizeSearchRoots([turnSnapshot.SolutionDirectoryPath]);
        }

        private static IReadOnlyList<string> BuildWritableLocalFilePaths(
            CopilotHostedTurnSnapshot turnSnapshot,
            IReadOnlyList<string> explicitLocalFilePaths)
        {
            var paths = explicitLocalFilePaths
                .Append(turnSnapshot.ActiveDocumentPath)
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Select(Path.GetFullPath)
                .Where(CopilotWorkspaceSearchSupport.IsTextLikeFile)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return paths;
        }

        private static CopilotContextScope MapContextScope(CopilotAgentMode mode)
        {
            return mode == CopilotAgentMode.Diagnose
                ? CopilotContextScope.Diagnose
                : mode == CopilotAgentMode.Chat
                    ? CopilotContextScope.Chat
                    : CopilotContextScope.Agent;
        }

        private static void AddSearchCandidate(List<string> roots, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                var fullPath = Path.GetFullPath(path);

                if (Directory.Exists(fullPath))
                {
                    roots.Add(fullPath);
                    return;
                }

                if (File.Exists(fullPath))
                {
                    var directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                        roots.Add(directory);
                }
            }
            catch
            {
            }
        }

        private static bool IsExistingDirectoryPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                return Directory.Exists(Path.GetFullPath(path));
            }
            catch
            {
                return false;
            }
        }

        private void ApplyAgentEvent(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            CopilotAgentEvent agentEvent)
        {
            switch (agentEvent.Type)
            {
                case CopilotAgentEventType.Status:
                    assistantMessage.BeginResponseTimeline();
                    assistantMessage.MarkThinkingStarted();
                    assistantMessage.IsExecutionInProgress = true;
                    assistantMessage.IsExecutionExpanded = true;
                    break;
                case CopilotAgentEventType.RuntimeDiagnostic:
                    assistantMessage.MarkThinkingStarted();
                    AppendAssistantExecutionTrace(assistantMessage, CopilotAgentTraceEntry.Sanitize(agentEvent.Text));
                    assistantMessage.IsExecutionInProgress = true;
                    assistantMessage.IsExecutionExpanded = true;
                    PersistState();
                    break;
                case CopilotAgentEventType.ToolStarted:
                    assistantMessage.MarkThinkingStarted();
                    if (agentEvent.ToolExecution != null)
                    {
                        assistantMessage.UpsertAgentTrace(CopilotAgentTraceEntry.FromStarted(agentEvent.ToolExecution));
                        assistantMessage.RecordResponseTimelineTool(agentEvent.ToolExecution.CallId);
                    }
                    else
                        AppendAssistantExecutionTrace(assistantMessage, BuildToolStartedTraceText(agentEvent.ToolExecution));
                    assistantMessage.IsExecutionInProgress = true;
                    assistantMessage.IsExecutionExpanded = true;
                    PersistState();
                    break;
                case CopilotAgentEventType.ToolResult:
                    assistantMessage.MarkThinkingStarted();
                    if (agentEvent.ToolExecution != null)
                    {
                        assistantMessage.UpsertAgentTrace(CopilotAgentTraceEntry.FromResult(agentEvent.ToolExecution, agentEvent.ToolResult));
                        assistantMessage.RecordResponseTimelineTool(agentEvent.ToolExecution.CallId);
                    }
                    else
                        AppendAssistantExecutionTrace(assistantMessage, BuildToolTraceText(agentEvent));
                    assistantMessage.IsExecutionInProgress = true;
                    assistantMessage.IsExecutionExpanded = true;
                    PersistState();
                    break;
                case CopilotAgentEventType.ReasoningDelta:
                    ApplyAssistantDelta(assistantMessage, new CopilotStreamDelta(agentEvent.Text, string.Empty));
                    break;
                case CopilotAgentEventType.AnswerDelta:
                    ApplyAssistantDelta(assistantMessage, new CopilotStreamDelta(string.Empty, agentEvent.Text), recordResponseTimeline: true);
                    break;
                case CopilotAgentEventType.AnswerReset:
                    assistantMessage.ResetResponseTimelineText();
                    PersistState();
                    break;
                case CopilotAgentEventType.Error:
                    AppendAssistantExecutionTrace(assistantMessage, CopilotAgentTraceEntry.Sanitize(agentEvent.Text));
                    assistantMessage.IsExecutionInProgress = false;
                    assistantMessage.IsReasoningInProgress = false;
                    assistantMessage.MarkThinkingCompleted();
                    PersistState();
                    break;
                case CopilotAgentEventType.Completed:
                    assistantMessage.IsExecutionInProgress = false;
                    assistantMessage.IsReasoningInProgress = false;
                    assistantMessage.MarkThinkingCompleted();
                    PersistState();
                    break;
                case CopilotAgentEventType.CheckpointReady:
                    _taskHost.MarkCheckpointReady(hostedRun.Id);
                    break;
                case CopilotAgentEventType.CheckpointUpdated:
                    if (hostedRun.State == CopilotHostedRunState.CancelRequested
                        || agentEvent.SessionCheckpoint?.IsStructurallyValid() != true
                        || agentEvent.TaskLedger == null)
                    {
                        break;
                    }

                    conversation.AgentSessionCheckpoint = agentEvent.SessionCheckpoint;
                    assistantMessage.AgentTaskLedger = agentEvent.TaskLedger;
                    PersistState();
                    if (ReferenceEquals(conversation, SelectedConversation))
                        RefreshAgentTasks();
                    break;
            }
        }

        private static void AppendAssistantExecutionTrace(CopilotChatMessage assistantMessage, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (!string.IsNullOrWhiteSpace(assistantMessage.ExecutionContent))
                assistantMessage.ExecutionContent += Environment.NewLine + Environment.NewLine;

            assistantMessage.ExecutionContent += text.Trim();
        }

        private static string BuildToolStartedTraceText(CopilotToolExecutionInfo? execution)
        {
            if (execution == null)
                return string.Empty;

            var queue = execution.QueueDurationMs > 0 ? $" · queued {FormatToolDuration(execution.QueueDurationMs)}" : string.Empty;
            return $"[Round {execution.Round} · {execution.ToolName}] Running · {execution.ConcurrencyMode}{queue}...";
        }

        private static string BuildToolTraceText(CopilotAgentEvent agentEvent)
        {
            var result = agentEvent.ToolResult;
            if (result == null)
                return string.Empty;

            var builder = new StringBuilder();
            var execution = agentEvent.ToolExecution;
            var toolName = execution?.ToolName ?? result.ToolName;
            var state = execution?.State switch
            {
                CopilotToolExecutionState.Completed => "Completed",
                CopilotToolExecutionState.TimedOut => "Timed out",
                CopilotToolExecutionState.Denied => "Denied",
                CopilotToolExecutionState.Cancelled => "Cancelled",
                CopilotToolExecutionState.AwaitingApproval => "Awaiting approval",
                _ => result.Success ? "Completed" : "Failed",
            };
            builder.Append('[');
            if (execution != null)
                builder.Append("Round ").Append(execution.Round).Append(" · ");
            builder.Append(toolName).Append("] ").Append(state);
            if (execution?.CompletedAtUtc != null)
                builder.Append(" · ").Append(FormatToolDuration(execution.DurationMs));
            if (execution?.QueueDurationMs > 0)
                builder.Append(" · queued ").Append(FormatToolDuration(execution.QueueDurationMs));

            if (!string.IsNullOrWhiteSpace(result.Summary))
                builder.AppendLine().Append(result.Summary.Trim());

            if (result.Success && string.IsNullOrWhiteSpace(result.Summary) && !string.IsNullOrWhiteSpace(result.Content))
            {
                var content = result.Content.Trim();
                builder.AppendLine().Append(content.Length <= 500 ? content : content[..500].TrimEnd() + "...");
            }

            if (!result.Success && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                builder.AppendLine().Append("Error: ").Append(CopilotMcpAuditLogger.RedactText(result.ErrorMessage));

            return builder.ToString().TrimEnd();
        }

        private static string FormatToolDuration(long durationMs)
        {
            return durationMs < 1000
                ? $"{Math.Max(0, durationMs)} ms"
                : $"{durationMs / 1000d:0.#} s";
        }

        private static void SetAssistantFallbackContent(CopilotChatMessage assistantMessage, string text)
        {
            if (!string.IsNullOrWhiteSpace(assistantMessage.Content) || string.IsNullOrWhiteSpace(text))
                return;

            if (assistantMessage.UsesResponseTimeline)
                assistantMessage.AppendResponseTimelineText(text);
            else
                assistantMessage.Content = text;
        }

        private static void FinalizeAssistantMessage(CopilotChatMessage assistantMessage)
        {
            assistantMessage.IsExecutionInProgress = false;
            assistantMessage.IsReasoningInProgress = false;
            assistantMessage.MarkThinkingCompleted();

            if (!string.IsNullOrWhiteSpace(assistantMessage.Content))
                return;

            SetAssistantFallbackContent(assistantMessage, assistantMessage.HasReasoning || assistantMessage.HasExecutionTrace
                ? "No final answer was received; only execution trace or reasoning content is available."
                : "The API returned successfully, but no displayable text was found.");
        }

        private void ApplyAssistantDelta(CopilotChatMessage assistantMessage, CopilotStreamDelta delta, bool recordResponseTimeline = false)
        {
            if (delta.HasReasoning)
            {
                assistantMessage.MarkThinkingStarted();
                assistantMessage.ReasoningContent += delta.ReasoningContent;
                assistantMessage.IsReasoningInProgress = true;
                assistantMessage.IsReasoningExpanded = true;
            }

            if (delta.HasContent)
            {
                var isFirstContentChunk = string.IsNullOrWhiteSpace(assistantMessage.Content);
                if (recordResponseTimeline)
                    assistantMessage.AppendResponseTimelineText(delta.Content);
                else
                    assistantMessage.Content += delta.Content;
                assistantMessage.IsReasoningInProgress = false;
                if (isFirstContentChunk && assistantMessage.HasReasoning)
                {
                    assistantMessage.IsReasoningExpanded = false;
                    assistantMessage.IsThinkingExpanded = false;
                }
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

            _ = SendAsync();
        }

        private void ExecuteSendOrSteer()
        {
            if (IsViewingActiveRun)
            {
                TrySteerCurrentRun();
                return;
            }
            if (IsViewingQueuedRun)
                return;

            _ = SendAsync();
        }

        private void TrySteerCurrentRun()
        {
            var steeringMessage = (InputText ?? string.Empty).Trim();
            var activeRun = ActiveHostedRun;
            if (!CanSteerCurrentRun || activeRun == null || string.IsNullOrWhiteSpace(steeringMessage))
                return;
            if (_agentRuntime is not ICopilotAgentSteeringRuntime steeringRuntime
                || !steeringRuntime.TryEnqueueSteeringMessage(steeringMessage))
                return;

            var activeConversation = Conversations.FirstOrDefault(conversation => string.Equals(conversation.Id, activeRun.ConversationId, StringComparison.Ordinal));
            var activeAssistant = activeConversation?.Messages.LastOrDefault(message => !message.IsUser && message.IsThinkingInProgress);
            if (activeAssistant != null)
                AppendAssistantExecutionTrace(activeAssistant, "User steering queued · " + CopilotAgentTraceEntry.Sanitize(steeringMessage));

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
            _ = SendAsync();
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
            return CanScheduleConversationRequest(SelectedConversation?.Id, mode);
        }

        private bool CanScheduleConversationRequest(string? conversationId, CopilotAgentMode mode)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return false;
            if (!IsBusy)
                return true;

            var activeRun = ActiveHostedRun;
            return activeRun?.IsAgent == true
                && !string.Equals(activeRun.ConversationId, conversationId, StringComparison.Ordinal)
                && _taskHost.FindRunByConversationId(conversationId) == null
                && mode != CopilotAgentMode.Chat
                && _taskHost.CanSchedule;
        }

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

            _ = SendAsync();
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

        private void OpenSettings()
        {
            var window = new CopilotSettingsWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            var result = window.ShowDialog();
            if (result != true && !window.HasAppliedChanges)
                return;

            ReloadStateFromConfig();
        }

        private void OpenMcpSettings()
        {
            var window = new CopilotMcpSettingsWindow(new CopilotSettingsViewModel())
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            var result = window.ShowDialog();
            if (result != true && !window.HasAppliedChanges)
                return;

            ReloadStateFromConfig();
        }

        private void ReloadStateFromConfig()
        {
            var preferredConversationId = SelectedConversation?.Id ?? _state.ActiveConversationId;
            var preferredProfileId = SelectedProfile?.Id ?? _state.ActiveProfileId;

            if (_config.EnsureInitialized())
                PersistConfig();

            _state = _stateStore.Load();
            if (_state.EnsureInitialized(_config))
                PersistState();

            OnPropertyChanged(nameof(Profiles));
            OnPropertyChanged(nameof(Conversations));
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(CanSelectProfile));
            RefreshMcpStatus();

            var conversation = Conversations.FirstOrDefault(item => item.Id == preferredConversationId)
                ?? Conversations.FirstOrDefault();

            SelectConversation(conversation, persist: false, preferredProfileId: preferredProfileId);
        }

        private List<CopilotRequestMessage> BuildConversationHistory(CopilotProfileConfig profile, bool includeAttachmentContext)
        {
            var history = SelectedConversation == null
                ? Array.Empty<CopilotRequestMessage>()
                : CopilotConversationCompactionContext.Build(SelectedConversation, stopBeforeMessage: null, useModelContent: true).ToArray();

            var attachmentContext = includeAttachmentContext ? BuildAttachmentContextBlock() : string.Empty;
            var limits = ResolveConversationHistoryLimits(profile);
            if (string.IsNullOrWhiteSpace(attachmentContext))
                return CopilotConversationHistoryWindow.Select(history, limits).ToList();

            var attachment = CopilotConversationHistoryWindow.Select(
                    [new CopilotRequestMessage("user", attachmentContext)],
                    maximumMessages: 1,
                    maximumCharacters: limits.MaximumContentCharacters,
                    maximumContentCharacters: limits.MaximumContentCharacters)
                .Single();
            var selected = CopilotConversationHistoryWindow.Select(
                    history,
                    Math.Max(1, limits.MaximumMessages - 1),
                    Math.Max(1, limits.MaximumCharacters - attachment.Content.Length),
                    limits.MaximumContentCharacters)
                .ToList();
            selected.Insert(0, attachment);

            return selected;
        }

        private List<CopilotRequestMessage> BuildVisibleConversationHistory(
            CopilotConversationRecord conversation,
            CopilotChatMessage? stopBeforeMessage,
            CopilotProfileConfig profile)
        {
            var history = CopilotConversationCompactionContext.Build(conversation, stopBeforeMessage, useModelContent: false);
            return CopilotConversationHistoryWindow.Select(history, ResolveConversationHistoryLimits(profile)).ToList();
        }

        private CopilotConversationHistoryLimits ResolveConversationHistoryLimits(CopilotProfileConfig? profile)
        {
            return CopilotConversationHistoryWindow.ResolveLimits(
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
                    SelectProfile(preferredProfile, syncConversation: false, persist: false);
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
            if (conversation == null)
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

            RemoveManagedAttachmentFiles(conversation.Attachments);
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
            var conversation = EnsureConversation();
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                CheckFileExists = true,
                Filter = "All files|*.*",
            };

            if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true)
                return;

            foreach (var fileName in dialog.FileNames.Where(fileName => !string.IsNullOrWhiteSpace(fileName)))
            {
                if (conversation.Attachments.Any(item => item.Type == CopilotAttachmentType.File && string.Equals(item.Value, fileName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                conversation.Attachments.Add(CopilotAttachmentItem.CreateFile(fileName));
            }

            UpdateAttachmentsState(conversation);
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
                    $"Failed to fetch web page: {ex.Message}",
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
                    $"Failed to paste image: {ex.Message}",
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
                    $"Failed to copy message: {ex.Message}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private bool CanRegenerateMessage(CopilotChatMessage? message)
        {
            if (IsBusy || message == null || SelectedConversation == null || SelectedProfile == null || !SelectedProfile.IsConfigured)
                return false;

            return TryResolveLatestTurn(message, out _, out _, out _);
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

            var prompt = (userMessage.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                return;

            var requestProfile = SelectedProfile.Clone();
            conversation.ProfileId = requestProfile.Id;
            conversation.ProfileDisplayName = requestProfile.DisplayLabel;
            conversation.AgentSessionCheckpoint = null;
            var turnSnapshot = CaptureHostedTurnSnapshot(conversation);
            PersistState();

            var hostedRun = _taskHost.Start(
                conversation.Id,
                userMessage.RequestMode,
                run => ExecuteHostedRetryAsync(run, conversation, requestProfile, userMessage, assistantMessage, turnSnapshot, refreshWebContext));
            await hostedRun.Completion;
        }

        private async Task ExecuteHostedRetryAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage? assistantMessage,
            CopilotHostedTurnSnapshot turnSnapshot,
            bool refreshWebContext)
        {
            CopilotChatMessage? replacementAssistantMessage = null;
            try
            {
                if (assistantMessage != null)
                    conversation.Messages.Remove(assistantMessage);

                replacementAssistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
                {
                    AssistantName = ResolveAssistantHeader(requestProfile),
                };
                replacementAssistantMessage.MarkThinkingStarted();
                conversation.Messages.Add(replacementAssistantMessage);

                var usage = await RunConversationTurnAsync(hostedRun, conversation, requestProfile, userMessage, replacementAssistantMessage, turnSnapshot, refreshWebContext);
                FinalizeAssistantMessage(replacementAssistantMessage);
                UpdateConversationUsage(conversation, usage);

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
                QueueConversationTitleGeneration(conversation, requestProfile);
            }
            catch (OperationCanceledException)
            {
                if (replacementAssistantMessage == null)
                    return;

                replacementAssistantMessage.IsExecutionInProgress = false;
                replacementAssistantMessage.IsReasoningInProgress = false;
                replacementAssistantMessage.MarkThinkingCompleted();

                var controlIntent = hostedRun.RunControl?.Intent ?? CopilotAgentControlIntent.None;
                if (controlIntent == CopilotAgentControlIntent.Cancel)
                {
                    conversation.AgentSessionCheckpoint = null;
                    replacementAssistantMessage.AgentStopReason = CopilotAgentStopReason.Cancelled;
                }
                else if (controlIntent == CopilotAgentControlIntent.Pause)
                {
                    replacementAssistantMessage.AgentStopReason = CopilotAgentStopReason.Paused;
                }

                if (string.IsNullOrWhiteSpace(replacementAssistantMessage.Content))
                {
                    replacementAssistantMessage.Content = controlIntent == CopilotAgentControlIntent.Pause
                        ? "Agent 任务已暂停；可从最近一次可用 checkpoint 继续。"
                        : "The current reply was cancelled.";
                }

                UpdateConversationUsage(conversation, CopilotTokenUsage.Empty);

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            catch (Exception ex)
            {
                if (replacementAssistantMessage == null)
                {
                    replacementAssistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, $"Request failed: {ex.Message}")
                    {
                        AssistantName = ResolveAssistantHeader(requestProfile),
                    };
                    replacementAssistantMessage.MarkThinkingStarted();
                    conversation.Messages.Add(replacementAssistantMessage);
                }

                replacementAssistantMessage.IsExecutionInProgress = false;
                replacementAssistantMessage.IsReasoningInProgress = false;
                replacementAssistantMessage.MarkThinkingCompleted();
                replacementAssistantMessage.Content = string.IsNullOrWhiteSpace(replacementAssistantMessage.Content)
                    ? $"Request failed: {ex.Message}"
                    : replacementAssistantMessage.Content;

                UpdateConversationUsage(conversation, CopilotTokenUsage.Empty);

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            finally
            {
                RefreshAgentTasks();
            }
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

        private void UpdateConversationUsage(CopilotConversationRecord conversation, CopilotTokenUsage usage)
        {
            if (usage.HasAny)
                conversation.SetLastUsage(usage);
            else
                conversation.ClearLastUsage();

            if (ReferenceEquals(conversation, SelectedConversation))
                RefreshComposerTokenEstimate();
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
            var selection = CaptureConversationHistorySelection();
            if (selection.SourceMessageCount == 0)
                return "None";

            var retained = $"{selection.Messages.Length} message(s), {selection.RetainedCharacters:N0} characters";
            return selection.WasReduced
                ? $"{retained} retained from {selection.SourceMessageCount} message(s), {selection.SourceCharacters:N0} characters"
                : retained;
        }

        private CopilotConversationHistorySelection CaptureConversationHistorySelection()
        {
            var history = SelectedConversation == null
                ? Array.Empty<CopilotRequestMessage>()
                : CopilotConversationCompactionContext.Build(SelectedConversation, stopBeforeMessage: null, useModelContent: true);
            return CopilotConversationHistoryWindow.SelectWithDiagnostics(history, ResolveConversationHistoryLimits(SelectedProfile));
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

        private void PersistState()
        {
            PublishSelectedTaskEventJournal();
            _stateStore.Save(_state);
            OnPropertyChanged(nameof(HasAttachments));
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

        private void AttachExternalContextSnapshot(
            CopilotConversationRecord conversation,
            string? attachmentTitle,
            string? attachmentSourceId,
            IReadOnlyList<CopilotContextItem> contextItems)
        {
            var content = BuildContextAttachmentContent(contextItems);
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

        private string BuildAttachmentContextBlock()
        {
            if (SelectedConversation == null || SelectedConversation.Attachments.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("The following context is attached to the current chat. It was explicitly provided by the user; use it when relevant.");

            foreach (var attachment in SelectedConversation.Attachments)
            {
                if (attachment.Type == CopilotAttachmentType.File)
                {
                    builder.AppendLine(BuildFileAttachmentBlock(attachment));
                    continue;
                }

                if (attachment.Type == CopilotAttachmentType.Image)
                {
                    builder.AppendLine(BuildImageAttachmentBlock(attachment));
                    continue;
                }

                if (attachment.Type == CopilotAttachmentType.WebPage)
                {
                    builder.AppendLine(BuildWebPageAttachmentBlock(attachment));
                    continue;
                }

                builder.AppendLine($"[{CopilotUiText.ContextBadge}] {attachment.DisplayLabel}");
                builder.AppendLine(attachment.Value);
                builder.AppendLine();
            }

            return builder.ToString().Trim();
        }

        private static string BuildContextAttachmentContent(IReadOnlyList<CopilotContextItem> contextItems)
        {
            if (contextItems == null || contextItems.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("The following business snapshots were explicitly attached by the user. They are fixed snapshots captured by the app or attached manually; prioritize them when answering.")
                .AppendLine();

            foreach (var item in contextItems)
            {
                if (item == null)
                    continue;

                var title = string.IsNullOrWhiteSpace(item.Title) ? CopilotUiText.ContextBadge : item.Title.Trim();
                builder.Append("## ").AppendLine(title);

                if (!string.IsNullOrWhiteSpace(item.Summary))
                    builder.Append("Summary: ").AppendLine(item.Summary.Trim());

                if (!string.IsNullOrWhiteSpace(item.Content))
                    builder.AppendLine(item.Content.Trim());

                builder.AppendLine();
            }

            return builder.ToString().Trim();
        }

        private static string BuildFileAttachmentBlock(CopilotAttachmentItem attachment)
        {
            try
            {
                if (!File.Exists(attachment.Value))
                    return $"[{CopilotUiText.FileBadge}] {attachment.Value}\nThe file does not exist and cannot be read.\n";

                var content = File.ReadAllText(attachment.Value);
                if (content.Length > AttachmentContentLimit)
                    content = content[..AttachmentContentLimit] + "\n...<truncated>";

                var fence = ResolveCodeFence(attachment.Value);
                return $"[{CopilotUiText.FileBadge}] {attachment.Value}\n~~~{fence}\n{content}\n~~~\n";
            }
            catch (Exception ex)
            {
                return $"[{CopilotUiText.FileBadge}] {attachment.Value}\nRead failed: {ex.Message}\n";
            }
        }

        private static string BuildImageAttachmentBlock(CopilotAttachmentItem attachment)
        {
            if (!File.Exists(attachment.Value))
                return $"[{CopilotUiText.ImageBadge}] {attachment.DisplayLabel}\nThe local image attachment does not exist: {attachment.Value}\n";

            return string.Join(Environment.NewLine, new[]
            {
                $"[{CopilotUiText.ImageBadge}] {attachment.DisplayLabel}",
                $"Local image path: {attachment.Value}",
                "The current version shows image previews in the UI but does not automatically upload pixel content to the model.",
                string.Empty,
            });
        }

        private static string BuildWebPageAttachmentBlock(CopilotAttachmentItem attachment)
        {
            var content = attachment.Value ?? string.Empty;
            if (content.Length > AttachmentContentLimit)
                content = content[..AttachmentContentLimit] + "\n...<truncated>";

            return string.Join(Environment.NewLine, new[]
            {
                $"[{CopilotUiText.WebPageBadge}] {attachment.DisplayLabel}",
                $"Source: {attachment.Source}",
                content,
                string.Empty,
            });
        }

        private async Task<string> BuildUserRequestContentAsync(string prompt, CancellationToken cancellationToken)
        {
            var builder = new StringBuilder();
            AppendCurrentLiveContextSummaryBlock(builder);

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(prompt);

            var urls = ExtractHttpUrls(prompt);
            if (urls.Count == 0)
                return builder.ToString().Trim();

            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("[Local Web Context Injection]");
            builder.AppendLine("The following web page content was fetched locally before sending. Answer web-page questions only from these fetched results. If fetching failed or the fetched content lacks relevant information, say so explicitly and do not assume unseen page content.");

            var remainingCharacters = MaxWebPageInjectionChars;
            foreach (var url in urls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var contextBlock = await BuildWebPageContextBlockAsync(url, cancellationToken);
                if (contextBlock.Length > remainingCharacters)
                {
                    builder.AppendLine();
                    builder.Append(contextBlock[..remainingCharacters]);
                    builder.AppendLine();
                    builder.AppendLine("...<web context truncated>");
                    break;
                }

                builder.AppendLine();
                builder.AppendLine(contextBlock);
                remainingCharacters -= contextBlock.Length;

                if (remainingCharacters <= 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("...<web context truncated>");
                    break;
                }
            }

            return builder.ToString().TrimEnd();
        }

        private void AppendCurrentLiveContextSummaryBlock(StringBuilder builder)
        {
            var liveContext = _currentLiveContext;
            if (liveContext == null)
                return;

            if (string.IsNullOrWhiteSpace(liveContext.Title) && string.IsNullOrWhiteSpace(liveContext.Summary))
                return;

            builder.AppendLine("[Current Window Context]");

            if (!string.IsNullOrWhiteSpace(liveContext.Title))
                builder.Append("Location: ").AppendLine(liveContext.Title.Trim());

            if (!string.IsNullOrWhiteSpace(liveContext.Summary))
                builder.Append("Summary: ").AppendLine(liveContext.Summary.Trim());

            builder.AppendLine("This is a lightweight summary of the current business window. If explicit snapshots are also attached, prioritize those snapshots when answering.");
        }

        private async Task<string> BuildWebPageContextBlockAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                var page = await LoadWebPageContentAsync(url, cancellationToken);
                return BuildFetchedWebPageContextBlock(page);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return BuildFailedWebPageContextBlock(url, ex.Message);
            }
        }

        private static string BuildFetchedWebPageContextBlock(CopilotFetchedWebPageContent page) =>
            CopilotWebPageToolSupport.BuildFetchedWebPageContextBlock(page);

        private static string BuildFailedWebPageContextBlock(string url, string failureMessage) =>
            CopilotWebPageToolSupport.BuildFailedWebPageContextBlock(url, failureMessage);

        private static List<string> ExtractHttpUrls(string text) => CopilotWebPageToolSupport.ExtractHttpUrls(text);

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

            try
            {
                var attachmentRoot = Path.GetFullPath(_stateStore.AttachmentDirectoryPath);
                var filePath = Path.GetFullPath(attachment.Value);
                if (!filePath.StartsWith(attachmentRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
                    return;

                File.Delete(filePath);
            }
            catch
            {
            }
        }

        private static string ResolveCodeFence(string filePath)
        {
            var extension = Path.GetExtension(filePath).Trim().TrimStart('.');
            return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension;
        }

        private static string NormalizeWebPageUrl(string value) => CopilotWebPageToolSupport.NormalizeWebPageUrl(value);

        private static Task<CopilotFetchedWebPageContent> LoadWebPageContentAsync(string url, CancellationToken cancellationToken) =>
            CopilotWebPageToolSupport.LoadWebPageContentAsync(url, cancellationToken);

        private sealed record CopilotHostedTurnSnapshot(
            string ActiveDocumentPath,
            string SolutionDirectoryPath,
            IReadOnlyList<CopilotAttachmentItem> Attachments);
    }
}

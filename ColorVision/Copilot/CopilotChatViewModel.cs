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
        private const int AgentHistoryMessageLimit = 8;
        private const int AttachmentContentLimit = 12000;
        private const int MaxWebPageInjectionChars = 8000;

        private readonly CopilotChatService _chatService;
        private readonly CopilotAgentContextBuilder _agentContextBuilder;
        private readonly CopilotAgentService _agentService;
        private readonly CopilotContextRegistry _contextRegistry;
        private readonly CopilotConfig _config;
        private readonly CopilotChatStateStore _stateStore;
        private readonly ObservableCollection<CopilotChatMessage> _emptyMessages = new();
        private readonly ObservableCollection<CopilotAttachmentItem> _emptyAttachments = new();
        private readonly ObservableCollection<ConfirmableAction> _pendingActions = new();
        private readonly DispatcherTimer _pendingActionExpiryTimer;
        private readonly IReadOnlyList<CopilotAgentModeOption> _agentModes = CopilotAgentModeOption.CreateDefaultOptions();
        private CancellationTokenSource? _currentRequestCts;
        private CancellationTokenSource? _pendingActionFeedbackCts;
        private CopilotLiveContext? _currentLiveContext;
        private CopilotChatState _state = new();
        private CopilotConversationRecord? _selectedConversation;
        private CopilotProfileConfig? _selectedProfile;
        private CopilotAgentMode _selectedAgentMode = CopilotAgentMode.Auto;
        private string _activeDocumentPath = string.Empty;
        private string _pendingActionFeedbackText = string.Empty;

        public CopilotChatViewModel()
            : this(new CopilotChatService())
        {
        }

        public CopilotChatViewModel(CopilotChatService chatService)
        {
            _chatService = chatService;
            _agentContextBuilder = new CopilotAgentContextBuilder();
            _agentService = new CopilotAgentService(chatService, CopilotToolRegistry.CreateDefault(), _agentContextBuilder);
            _contextRegistry = CopilotContextRegistry.CreateDefault();
            _config = CopilotConfig.Instance;
            _stateStore = CopilotChatStateStore.Instance;
            _currentLiveContext = CopilotLiveContextRegistry.Current;

            WorkspaceManager.ContentIdSelected -= WorkspaceManager_ContentIdSelected;
            WorkspaceManager.ContentIdSelected += WorkspaceManager_ContentIdSelected;
            CopilotLiveContextRegistry.CurrentChanged -= CopilotLiveContextRegistry_CurrentChanged;
            CopilotLiveContextRegistry.CurrentChanged += CopilotLiveContextRegistry_CurrentChanged;
            CopilotMcpConfirmationStore.Instance.ActionsChanged -= ConfirmationStore_ActionsChanged;
            CopilotMcpConfirmationStore.Instance.ActionsChanged += ConfirmationStore_ActionsChanged;

            if (_config.EnsureInitialized())
                PersistConfig();

            _state = _stateStore.Load();
            if (_state.EnsureInitialized(_config))
                PersistState();

            var initialConversation = Conversations.Count > 0
                ? Conversations[0]
                : CopilotConversationRecord.CreateEmpty(_state.ActiveProfileId, string.Empty);

            if (Conversations.Count == 0)
                Conversations.Add(initialConversation);

            SelectConversation(Conversations.FirstOrDefault(conversation => conversation.Id == _state.ActiveConversationId) ?? initialConversation, persist: false);

            SendCommand = new RelayCommand(_ => _ = SendAsync());
            NewChatCommand = new RelayCommand(_ => StartNewChat());
            CancelCommand = new RelayCommand(_ => CancelCurrentReply());
            PrimaryActionCommand = new RelayCommand(_ => ExecutePrimaryAction());
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            AddFileAttachmentCommand = new RelayCommand(_ => AddFileAttachment(), _ => !IsBusy);
            AddContextAttachmentCommand = new RelayCommand(_ => AddContextAttachment(), _ => !IsBusy);
            AddWebPageAttachmentCommand = new RelayCommand(_ => _ = AddWebPageAttachmentAsync(), _ => !IsBusy);
            PasteImageAttachmentCommand = new RelayCommand(_ => PasteImageAttachment(), _ => !IsBusy);
            AttachCurrentLiveContextCommand = new RelayCommand(_ => AttachCurrentLiveContext(), _ => HasCurrentLiveContext);
            CopyMessageCommand = new RelayCommand<CopilotChatMessage>(CopyMessage, message => message != null);
            RetryMessageCommand = new RelayCommand<CopilotChatMessage>(message => _ = RetryMessageAsync(message, refreshWebContext: false), CanRegenerateMessage);
            RefreshMessageCommand = new RelayCommand<CopilotChatMessage>(message => _ = RetryMessageAsync(message, refreshWebContext: true), CanRegenerateMessage);
            RemoveAttachmentCommand = new RelayCommand<CopilotAttachmentItem>(RemoveAttachment, attachment => !IsBusy && attachment != null);
            RenameConversationCommand = new RelayCommand<CopilotConversationRecord>(RenameConversation, conversation => !IsBusy && conversation != null);
            DeleteConversationCommand = new RelayCommand<CopilotConversationRecord>(DeleteConversation, conversation => !IsBusy && conversation != null);
            TogglePinConversationCommand = new RelayCommand<CopilotConversationRecord>(TogglePinConversation, conversation => !IsBusy && conversation != null);
            CopyPendingActionIdCommand = new RelayCommand<ConfirmableAction>(CopyPendingActionId, action => action != null);
            ApprovePendingActionCommand = new RelayCommand<ConfirmableAction>(ApprovePendingAction, action => action?.IsPending == true);
            RejectPendingActionCommand = new RelayCommand<ConfirmableAction>(RejectPendingAction, action => action?.IsPending == true);

            _pendingActionExpiryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5),
            };
            _pendingActionExpiryTimer.Tick += (_, _) => RefreshPendingActions();
            _pendingActionExpiryTimer.Start();

            RefreshPendingActions();
            RefreshComposerTokenEstimate();
        }

        public ObservableCollection<CopilotConversationRecord> Conversations => _state.Conversations;

        public ObservableCollection<CopilotProfileConfig> Profiles => _config.Profiles;

        public ObservableCollection<CopilotChatMessage> Messages => SelectedConversation?.Messages ?? _emptyMessages;

        public ObservableCollection<CopilotAttachmentItem> Attachments => SelectedConversation?.Attachments ?? _emptyAttachments;

        public ObservableCollection<ConfirmableAction> PendingActions => _pendingActions;

        public bool HasPendingActions => _pendingActions.Count > 0;

        public bool HasPendingActionFeedback => !string.IsNullOrWhiteSpace(PendingActionFeedbackText);

        public bool HasPendingActionPanel => HasPendingActions || HasPendingActionFeedback;

        public string PendingActionFeedbackText
        {
            get => _pendingActionFeedbackText;
            private set
            {
                if (SetProperty(ref _pendingActionFeedbackText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasPendingActionFeedback));
                    OnPropertyChanged(nameof(HasPendingActionPanel));
                }
            }
        }

        public IReadOnlyList<CopilotAgentModeOption> AgentModes => _agentModes;

        public ICommand SendCommand { get; }

        public ICommand NewChatCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand PrimaryActionCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand AddFileAttachmentCommand { get; }

        public ICommand AddContextAttachmentCommand { get; }

        public ICommand AddWebPageAttachmentCommand { get; }

        public ICommand PasteImageAttachmentCommand { get; }

        public ICommand AttachCurrentLiveContextCommand { get; }

        public ICommand CopyMessageCommand { get; }

        public ICommand RetryMessageCommand { get; }

        public ICommand RefreshMessageCommand { get; }

        public ICommand RemoveAttachmentCommand { get; }

        public ICommand RenameConversationCommand { get; }

        public ICommand DeleteConversationCommand { get; }

        public ICommand TogglePinConversationCommand { get; }

        public ICommand CopyPendingActionIdCommand { get; }

        public ICommand ApprovePendingActionCommand { get; }

        public ICommand RejectPendingActionCommand { get; }

        public bool IsConversationEmpty => Messages.Count == 0;

        public bool HasAttachments => Attachments.Count > 0;

        public bool HasCurrentLiveContext => _currentLiveContext != null;

        public string CurrentLiveContextTitle => _currentLiveContext?.Title ?? string.Empty;

        public string CurrentLiveContextSummary => _currentLiveContext?.Summary ?? string.Empty;

        public bool CanAttachCurrentLiveContext => _currentLiveContext != null;

        public bool IsCurrentLiveContextAttached => _currentLiveContext != null
            && SelectedConversation?.Attachments.Any(item => item.Type == CopilotAttachmentType.Context
                && string.Equals(item.Source, _currentLiveContext.SourceId, StringComparison.Ordinal)) == true;

        public string CurrentLiveContextActionText => IsCurrentLiveContextAttached ? Properties.Resources.CopilotUpdateSnapshot : Properties.Resources.CopilotAttachToQuestion;

        public string EmptyStateText => _config.IsConfigured
            ? Properties.Resources.CopilotSelectHistoryOrNew
            : Properties.Resources.CopilotConfigureModelFirst;

        public string PrimaryActionGlyph => IsBusy ? "■" : "↑";

        public string PrimaryActionToolTip => IsBusy ? Properties.Resources.CopilotStopGeneration : SelectedAgentMode == CopilotAgentMode.Chat ? Properties.Resources.CopilotSend : Properties.Resources.CopilotExecuteAgent;

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

        public CopilotAgentMode SelectedAgentMode
        {
            get => _selectedAgentMode;
            set
            {
                if (SetProperty(ref _selectedAgentMode, value))
                {
                    OnPropertyChanged(nameof(SelectedAgentModeDescription));
                    OnPropertyChanged(nameof(PrimaryActionToolTip));
                    RefreshComposerTokenEstimate();
                }
            }
        }

        public string SelectedAgentModeDescription => AgentModes.FirstOrDefault(option => option.Mode == SelectedAgentMode)?.Description ?? string.Empty;

        public string ComposerTokenSummary
        {
            get => _composerTokenSummary;
            private set => SetProperty(ref _composerTokenSummary, value ?? string.Empty);
        }
        private string _composerTokenSummary = "发送后显示真实 token usage";

        public string ComposerTokenDetails
        {
            get => _composerTokenDetails;
            private set => SetProperty(ref _composerTokenDetails, value ?? string.Empty);
        }
        private string _composerTokenDetails = "当前不再做本地预估，只显示接口真实返回的 token usage。";

        public string TokenUsageBadgeText
        {
            get
            {
                if (IsTokenUsagePending)
                    return "...";

                if (HasRealTokenUsage)
                    return CopilotTokenUsage.FormatCount(SelectedConversation?.LastUsage.EffectiveTotalTokens ?? 0);

                return SelectedConversation?.Messages.Count > 0
                    ? "NA"
                    : "--";
            }
        }

        public bool HasRealTokenUsage => SelectedConversation?.LastUsage.HasAny == true;

        public bool IsTokenUsagePending => IsBusy && SelectedProfile != null;

        public bool IsTokenUsageUnavailable => !IsTokenUsagePending
            && !HasRealTokenUsage
            && SelectedProfile != null
            && SelectedConversation?.Messages.Count > 0;

        public string InputText
        {
            get => _inputText;
            set
            {
                if (SetProperty(ref _inputText, value ?? string.Empty))
                    RefreshComposerTokenEstimate();
            }
        }
        private string _inputText = string.Empty;

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
                OnPropertyChanged(nameof(CanSelectAgentMode));
                OnPropertyChanged(nameof(PrimaryActionGlyph));
                OnPropertyChanged(nameof(PrimaryActionToolTip));
                RefreshComposerTokenEstimate();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private bool _isBusy;

        public bool CanSwitchConversation => !IsBusy;

        public bool CanSelectProfile => !IsBusy && Profiles.Count > 0;

        public bool CanSelectAgentMode => !IsBusy;

        private async Task SendAsync()
        {
            if (IsBusy)
                return;

            var prompt = (InputText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(prompt))
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

            var userMessage = new CopilotChatMessage(CopilotChatRole.User, prompt)
            {
                RequestMode = SelectedAgentMode,
            };
            var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
            {
                AssistantName = ResolveAssistantHeader(requestProfile),
            };

            Messages.Add(userMessage);
            Messages.Add(assistantMessage);
            UpdateConversationMetadata(conversation, touch: true);
            PersistState();
            InputText = string.Empty;

            BeginRequest();

            try
            {
                var usage = await RunConversationTurnAsync(conversation, requestProfile, userMessage, assistantMessage, refreshExternalContext: true);
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

                if (string.IsNullOrWhiteSpace(assistantMessage.Content))
                    assistantMessage.Content = "已取消当前回复。";

                UpdateConversationUsage(conversation, CopilotTokenUsage.Empty);

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            catch (Exception ex)
            {
                assistantMessage.IsExecutionInProgress = false;
                assistantMessage.IsReasoningInProgress = false;
                assistantMessage.Content = string.IsNullOrWhiteSpace(assistantMessage.Content)
                    ? $"请求失败：{ex.Message}"
                    : assistantMessage.Content;

                UpdateConversationUsage(conversation, CopilotTokenUsage.Empty);

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            finally
            {
                EndRequest();
            }
        }

        private void BeginRequest()
        {
            IsBusy = true;

            _currentRequestCts?.Cancel();
            _currentRequestCts?.Dispose();
            _currentRequestCts = new CancellationTokenSource();
        }

        private void EndRequest()
        {
            IsBusy = false;
            _currentRequestCts?.Dispose();
            _currentRequestCts = null;
        }

        private async Task<CopilotTokenUsage> RunConversationTurnAsync(
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            bool refreshExternalContext)
        {
            if (_currentRequestCts == null)
                throw new InvalidOperationException("请求上下文尚未初始化。");

            if (userMessage.RequestMode == CopilotAgentMode.Chat)
            {
                return await RunChatTurnAsync(requestProfile, userMessage, assistantMessage, refreshExternalContext, _currentRequestCts.Token);
            }

            return await RunAgentTurnAsync(conversation, requestProfile, userMessage, assistantMessage, refreshExternalContext, _currentRequestCts.Token);
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

            var history = BuildConversationHistory(includeAttachmentContext: true);
            return await _chatService.StreamReplyAsync(
                requestProfile,
                history,
                delta => ApplyAssistantDelta(assistantMessage, delta),
                cancellationToken);
        }

        private async Task<CopilotTokenUsage> RunAgentTurnAsync(
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            bool refreshExternalContext,
            CancellationToken cancellationToken)
        {
            if (!refreshExternalContext && !string.IsNullOrWhiteSpace(userMessage.RequestContent))
            {
                AppendAssistantExecutionTrace(assistantMessage, "复用上次执行得到的上下文，未重新调用工具。");
                assistantMessage.IsExecutionInProgress = true;
                assistantMessage.IsExecutionExpanded = true;

                var history = BuildVisibleConversationHistory(conversation, userMessage, 8);
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
            var searchRootPaths = BuildSearchRootPaths(conversation, explicitLocalPaths);
            IReadOnlyList<CopilotContextItem> contextItems = await _contextRegistry.CaptureAsync(
                new CopilotContextRequest
                {
                    Scope = MapContextScope(userMessage.RequestMode),
                    UserText = (userMessage.Content ?? string.Empty).Trim(),
                    SolutionDirectoryPath = SolutionManager.GetInstance().CurrentSolutionExplorer?.DirectoryInfo?.FullName ?? string.Empty,
                    ActiveDocumentPath = _activeDocumentPath,
                    SearchRootPaths = searchRootPaths,
                },
                cancellationToken);

            contextItems = MergeCurrentLiveContextSummary(contextItems);

            var agentRequest = new CopilotAgentRequest
            {
                UserText = (userMessage.Content ?? string.Empty).Trim(),
                Profile = requestProfile,
                History = BuildVisibleConversationHistory(conversation, userMessage, 8),
                Attachments = conversation.Attachments.ToArray(),
                ContextItems = contextItems,
                SearchRootPaths = searchRootPaths,
                ActiveDocumentPath = _activeDocumentPath,
                ReadableLocalFilePaths = explicitLocalFilePaths,
                ReadableLocalDirectoryPaths = explicitLocalDirectoryPaths,
                PreferBatchReadLocalFiles = explicitLocalDirectoryPaths.Length > 0 && explicitLocalFilePaths.Length == 0,
                Mode = userMessage.RequestMode,
            };

            var result = await _agentService.RunAsync(
                agentRequest,
                agentEvent => ApplyAgentEvent(assistantMessage, agentEvent),
                cancellationToken);

            userMessage.RequestContent = result.PreparedUserMessageContent;
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

        private void ConfirmationStore_ActionsChanged(object? sender, EventArgs e)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => ConfirmationStore_ActionsChanged(sender, e)));
                return;
            }

            RefreshPendingActions();
        }

        private void RefreshPendingActions()
        {
            _pendingActions.Clear();
            foreach (var action in CopilotMcpConfirmationStore.Instance.GetPendingActions())
                _pendingActions.Add(action);

            OnPropertyChanged(nameof(HasPendingActions));
            OnPropertyChanged(nameof(HasPendingActionPanel));
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

        private void ApprovePendingAction(ConfirmableAction? action)
        {
            if (action == null)
                return;

            CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out var message);
            SetPendingActionFeedback($"{action.ActionId}: {message}");
            RefreshPendingActions();
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
            OnPropertyChanged(nameof(CurrentLiveContextActionText));
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

        private IReadOnlyList<string> BuildSearchRootPaths(
            CopilotConversationRecord conversation,
            IReadOnlyList<string> explicitLocalFilePaths)
        {
            var roots = new List<string>();

            AddSearchCandidate(roots, SolutionManager.GetInstance().CurrentSolutionExplorer?.DirectoryInfo?.FullName);
            AddSearchCandidate(roots, _activeDocumentPath);

            foreach (var path in explicitLocalFilePaths)
            {
                AddSearchCandidate(roots, path);
            }

            foreach (var attachment in conversation.Attachments.Where(item => item.Type == CopilotAttachmentType.File && !string.IsNullOrWhiteSpace(item.Value)))
            {
                AddSearchCandidate(roots, attachment.Value);
            }

            return CopilotWorkspaceSearchSupport.NormalizeSearchRoots(roots);
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

        private void ApplyAgentEvent(CopilotChatMessage assistantMessage, CopilotAgentEvent agentEvent)
        {
            switch (agentEvent.Type)
            {
                case CopilotAgentEventType.Status:
                    AppendAssistantExecutionTrace(assistantMessage, agentEvent.Text);
                    assistantMessage.IsExecutionInProgress = true;
                    assistantMessage.IsExecutionExpanded = true;
                    break;
                case CopilotAgentEventType.ToolResult:
                    AppendAssistantExecutionTrace(assistantMessage, BuildToolTraceText(agentEvent.ToolResult));
                    assistantMessage.IsExecutionInProgress = true;
                    assistantMessage.IsExecutionExpanded = true;
                    break;
                case CopilotAgentEventType.ReasoningDelta:
                    ApplyAssistantDelta(assistantMessage, new CopilotStreamDelta(agentEvent.Text, string.Empty));
                    break;
                case CopilotAgentEventType.AnswerDelta:
                    ApplyAssistantDelta(assistantMessage, new CopilotStreamDelta(string.Empty, agentEvent.Text));
                    break;
                case CopilotAgentEventType.Error:
                    AppendAssistantExecutionTrace(assistantMessage, agentEvent.Text);
                    assistantMessage.IsExecutionInProgress = false;
                    assistantMessage.IsReasoningInProgress = false;
                    break;
                case CopilotAgentEventType.Completed:
                    assistantMessage.IsExecutionInProgress = false;
                    assistantMessage.IsReasoningInProgress = false;
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

        private static string BuildToolTraceText(CopilotToolResult? result)
        {
            if (result == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.Append('[').Append(result.ToolName).Append(']').AppendLine();
            builder.Append("状态：").AppendLine(result.Success ? "成功" : "失败");

            if (!string.IsNullOrWhiteSpace(result.Summary))
                builder.Append("摘要：").AppendLine(result.Summary);

            if (!string.IsNullOrWhiteSpace(result.Content))
            {
                builder.AppendLine("结果：");
                builder.AppendLine(result.Content.Trim());
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                builder.Append("错误：").Append(result.ErrorMessage);

            return builder.ToString().TrimEnd();
        }

        private static void FinalizeAssistantMessage(CopilotChatMessage assistantMessage)
        {
            assistantMessage.IsExecutionInProgress = false;
            assistantMessage.IsReasoningInProgress = false;

            if (!string.IsNullOrWhiteSpace(assistantMessage.Content))
                return;

            assistantMessage.Content = assistantMessage.HasReasoning || assistantMessage.HasExecutionTrace
                ? "未收到最终回答，只拿到了执行过程或推理内容。"
                : "接口返回成功，但没有可显示的文本。";
        }

        private void ApplyAssistantDelta(CopilotChatMessage assistantMessage, CopilotStreamDelta delta)
        {
            if (delta.HasReasoning)
            {
                assistantMessage.ReasoningContent += delta.ReasoningContent;
                assistantMessage.IsReasoningInProgress = true;
                assistantMessage.IsReasoningExpanded = true;
            }

            if (delta.HasContent)
            {
                var isFirstContentChunk = string.IsNullOrWhiteSpace(assistantMessage.Content);
                assistantMessage.Content += delta.Content;
                assistantMessage.IsReasoningInProgress = false;
                if (isFirstContentChunk && assistantMessage.HasReasoning)
                    assistantMessage.IsReasoningExpanded = false;
            }
        }

        private void StartNewChat()
        {
            CancelCurrentReply();

            if (IsReusableEmptyConversation(SelectedConversation))
                return;

            var conversation = ResolveNewConversationTarget();
            if (!ReferenceEquals(conversation, SelectedConversation))
            {
                SelectConversation(conversation, persist: false);
                PersistState();
            }
        }

        private static bool IsReusableEmptyConversation(CopilotConversationRecord? conversation)
        {
            return conversation != null && conversation.Messages.Count == 0;
        }

        private CopilotConversationRecord ResolveNewConversationTarget()
        {
            if (IsReusableEmptyConversation(SelectedConversation))
                return SelectedConversation!;

            var reusableConversation = Conversations.FirstOrDefault(IsReusableEmptyConversation);
            return reusableConversation ?? CreateConversation();
        }

        private void ExecutePrimaryAction()
        {
            if (IsBusy)
            {
                CancelCurrentReply();
                return;
            }

            _ = SendAsync();
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

            SelectedAgentMode = mode;
            InputText = normalizedPrompt;

            if (!sendNow || IsBusy)
                return new CopilotPromptQueueResult(true, false);

            _ = SendAsync();
            return new CopilotPromptQueueResult(true, true);
        }

        private void CancelCurrentReply()
        {
            if (!IsBusy)
                return;

            _currentRequestCts?.Cancel();
        }

        private void OpenSettings()
        {
            var window = new CopilotSettingsWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (window.ShowDialog() != true)
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

            var conversation = Conversations.FirstOrDefault(item => item.Id == preferredConversationId)
                ?? Conversations.FirstOrDefault();

            SelectConversation(conversation, persist: false, preferredProfileId: preferredProfileId);
        }

        private List<CopilotRequestMessage> BuildConversationHistory(bool includeAttachmentContext)
        {
            var history = new List<CopilotRequestMessage>();

            if (includeAttachmentContext)
            {
                var attachmentContext = BuildAttachmentContextBlock();
                if (!string.IsNullOrWhiteSpace(attachmentContext))
                    history.Add(new CopilotRequestMessage("user", attachmentContext));
            }

            history.AddRange(Messages
                .Where(message => !string.IsNullOrWhiteSpace(message.ModelContent))
                .Select(message => new CopilotRequestMessage(
                    message.IsUser ? "user" : "assistant",
                    message.ModelContent.Trim())));

            return history;
        }

        private static List<CopilotRequestMessage> BuildVisibleConversationHistory(
            CopilotConversationRecord conversation,
            CopilotChatMessage? stopBeforeMessage,
            int maxMessages)
        {
            var messages = conversation.Messages.AsEnumerable();
            if (stopBeforeMessage != null)
            {
                var index = conversation.Messages.IndexOf(stopBeforeMessage);
                if (index >= 0)
                    messages = conversation.Messages.Take(index);
            }

            return messages
                .Where(message => !string.IsNullOrWhiteSpace(message.Content))
                .TakeLast(maxMessages)
                .Select(message => new CopilotRequestMessage(
                    message.IsUser ? "user" : "assistant",
                    message.Content.Trim()))
                .ToList();
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsConversationEmpty));
            RefreshComposerTokenEstimate();
            CommandManager.InvalidateRequerySuggested();
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
            if (_selectedConversation != null)
                _selectedConversation.Attachments.CollectionChanged += Attachments_CollectionChanged;

            if (_selectedConversation != null)
                _selectedConversation.Messages.CollectionChanged += Messages_CollectionChanged;

            OnPropertyChanged(nameof(SelectedConversation));
            OnPropertyChanged(nameof(Messages));
            OnPropertyChanged(nameof(Attachments));
            OnPropertyChanged(nameof(HasAttachments));
            OnPropertyChanged(nameof(IsConversationEmpty));

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

            _selectedProfile = profile;
            OnPropertyChanged(nameof(SelectedProfile));

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
            var conversation = CopilotConversationRecord.CreateEmpty(profile?.Id ?? string.Empty, profile?.DisplayLabel ?? string.Empty);
            Conversations.Insert(GetUnpinnedInsertIndex(), conversation);
            return conversation;
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
                requestProfile.SystemPrompt = "You are a conversation title generator. Generate a short, natural English title for the given conversation. Return only the title itself, with no explanation.";
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
            MoveConversationToPreferredIndex(conversation);
            _state.ActiveConversationId = conversation.Id;
        }

        private void RenameConversation(CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return;

            var window = new CopilotTextInputWindow("重命名会话", "输入新的会话名称", conversation.Title)
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
                $"确定要删除会话“{conversation.Title}”吗？",
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            RemoveManagedAttachmentFiles(conversation.Attachments);

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
            MoveConversationToPreferredIndex(conversation);
            PersistState();
        }

        private void AddFileAttachment()
        {
            var conversation = EnsureConversation();
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                CheckFileExists = true,
                Filter = "所有文件|*.*",
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
            var window = new CopilotTextInputWindow("挂载上下文", "输入要附加到当前会话的上下文说明", string.Empty, isMultiline: true)
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
            var window = new CopilotTextInputWindow("挂载网页", "输入要抓取并附加到当前会话的网页地址", "https://")
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
                    "网页地址格式不正确。",
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
                    $"抓取网页失败：{ex.Message}",
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
                "剪贴板里没有可挂载的图片。",
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
                var title = $"粘贴图片 {DateTime.Now:HH:mm:ss}";
                conversation.Attachments.Add(CopilotAttachmentItem.CreateImage(imagePath, title));
                UpdateAttachmentsState(conversation);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"粘贴图片失败：{ex.Message}",
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
                    $"复制消息失败：{ex.Message}",
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

            CopilotChatMessage? replacementAssistantMessage = null;

            BeginRequest();

            try
            {
                if (assistantMessage != null)
                    conversation.Messages.Remove(assistantMessage);

                replacementAssistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
                {
                    AssistantName = ResolveAssistantHeader(requestProfile),
                };
                conversation.Messages.Add(replacementAssistantMessage);

                var usage = await RunConversationTurnAsync(conversation, requestProfile, userMessage, replacementAssistantMessage, refreshWebContext);
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

                if (string.IsNullOrWhiteSpace(replacementAssistantMessage.Content))
                    replacementAssistantMessage.Content = "已取消当前回复。";

                UpdateConversationUsage(conversation, CopilotTokenUsage.Empty);

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            catch (Exception ex)
            {
                if (replacementAssistantMessage == null)
                {
                    replacementAssistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, $"请求失败：{ex.Message}")
                    {
                        AssistantName = ResolveAssistantHeader(requestProfile),
                    };
                    conversation.Messages.Add(replacementAssistantMessage);
                }

                replacementAssistantMessage.IsExecutionInProgress = false;
                replacementAssistantMessage.IsReasoningInProgress = false;
                replacementAssistantMessage.Content = string.IsNullOrWhiteSpace(replacementAssistantMessage.Content)
                    ? $"请求失败：{ex.Message}"
                    : replacementAssistantMessage.Content;

                UpdateConversationUsage(conversation, CopilotTokenUsage.Empty);

                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
            }
            finally
            {
                EndRequest();
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
                sections.Add("执行过程：");
                sections.Add(execution);
            }

            if (!string.IsNullOrWhiteSpace(reasoning))
            {
                if (sections.Count > 0)
                    sections.Add(string.Empty);

                sections.Add("推理：");
                sections.Add(reasoning);
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                if (sections.Count > 0)
                    sections.Add(string.Empty);

                sections.Add("回答：");
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
                summary = "正在等待接口返回真实 token usage...";
                details = BuildPendingComposerTokenDetails();
            }
            else if (SelectedConversation?.LastUsage.HasAny == true)
            {
                summary = BuildActualUsageSummary(SelectedConversation.LastUsage);
                details = BuildActualUsageDetails(SelectedConversation, SelectedConversation.LastUsage);
            }
            else if (SelectedProfile == null)
            {
                summary = "未选择模型";
                details = "请选择或配置模型后再发送。当前面板只显示接口真实返回的 token usage。";
            }
            else if (SelectedConversation?.Messages.Count > 0)
            {
                summary = "最近一次请求未返回 token usage";
                details = BuildUnavailableUsageDetails(SelectedConversation);
            }
            else
            {
                summary = "发送后显示真实 token usage";
                details = BuildIdleComposerTokenDetails();
            }

            ComposerTokenSummary = summary;
            ComposerTokenDetails = details;
            NotifyTokenUsageBadgeChanged();
        }

        private void NotifyTokenUsageBadgeChanged()
        {
            OnPropertyChanged(nameof(TokenUsageBadgeText));
            OnPropertyChanged(nameof(HasRealTokenUsage));
            OnPropertyChanged(nameof(IsTokenUsagePending));
            OnPropertyChanged(nameof(IsTokenUsageUnavailable));
        }

        private void InvalidateChatAttachmentTokenEstimate()
        {
            RefreshComposerTokenEstimate();
        }

        private string BuildActualUsageSummary(CopilotTokenUsage usage)
        {
            return $"最近一次：输入 {CopilotTokenUsage.FormatCount(usage.InputTokens)} · 输出 {CopilotTokenUsage.FormatCount(usage.OutputTokens)} · 总计 {CopilotTokenUsage.FormatCount(usage.EffectiveTotalTokens)}";
        }

        private string BuildActualUsageDetails(CopilotConversationRecord conversation, CopilotTokenUsage usage)
        {
            var mode = ResolveLastRequestMode(conversation);
            var builder = new StringBuilder();
            builder.AppendLine($"模型：{ResolveUsageModelLabel(conversation)}");
            builder.AppendLine($"模式：{ResolveModeLabel(mode)}");
            builder.AppendLine($"输入 tokens：{CopilotTokenUsage.FormatCount(usage.InputTokens)}");
            builder.AppendLine($"输出 tokens：{CopilotTokenUsage.FormatCount(usage.OutputTokens)}");
            builder.AppendLine($"总计 tokens：{CopilotTokenUsage.FormatCount(usage.EffectiveTotalTokens)}");
            builder.AppendLine();
            builder.Append("说明：这里显示的是接口真实返回的最近一次请求 usage。");

            if (mode != CopilotAgentMode.Chat)
                builder.Append(" Agent 模式已累计 planner 和最终回答的多次模型调用。");

            return builder.ToString().TrimEnd();
        }

        private string BuildPendingComposerTokenDetails()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"模型：{SelectedProfile?.DisplayLabel}");
            builder.AppendLine($"模式：{ResolveModeLabel(SelectedAgentMode)}");
            builder.AppendLine();
            builder.Append("说明：当前只显示接口真实返回的 usage，统计会在本次请求完成后刷新。");
            return builder.ToString();
        }

        private string BuildUnavailableUsageDetails(CopilotConversationRecord conversation)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"模型：{ResolveUsageModelLabel(conversation)}");
            builder.AppendLine($"模式：{ResolveModeLabel(ResolveLastRequestMode(conversation))}");
            builder.AppendLine();
            builder.Append("说明：当前面板不再做本地预估，只显示接口真实返回的 usage。最近一次请求没有返回 usage 字段，所以这里没有可显示的输入/输出 token。");
            return builder.ToString();
        }

        private string BuildIdleComposerTokenDetails()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"模型：{SelectedProfile?.DisplayLabel}");
            builder.AppendLine($"模式：{ResolveModeLabel(SelectedAgentMode)}");
            builder.AppendLine();
            builder.Append("说明：发送后如果接口返回 usage 字段，这里会显示真实的输入、输出和总计 tokens。");
            return builder.ToString();
        }

        private string ResolveUsageModelLabel(CopilotConversationRecord conversation)
        {
            if (!string.IsNullOrWhiteSpace(conversation.ProfileDisplayName))
                return conversation.ProfileDisplayName;

            if (!string.IsNullOrWhiteSpace(SelectedProfile?.DisplayLabel))
                return SelectedProfile.DisplayLabel;

            return "Unnamed model";
        }

        private CopilotAgentMode ResolveLastRequestMode(CopilotConversationRecord conversation)
        {
            return conversation.Messages.LastOrDefault(message => message.IsUser)?.RequestMode ?? SelectedAgentMode;
        }

        private string ResolveModeLabel(CopilotAgentMode mode)
        {
            return AgentModes.FirstOrDefault(option => option.Mode == mode)?.Label ?? mode.ToString();
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

            if (title.StartsWith("标题", StringComparison.Ordinal))
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
            _stateStore.Save(_state);
            OnPropertyChanged(nameof(HasAttachments));
        }

        private void PersistConfig()
        {
            ConfigHandler.GetInstance().Save<CopilotConfig>();
            OnPropertyChanged(nameof(EmptyStateText));
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
                ? contextItems.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Title))?.Title ?? "附加上下文"
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

        private void MoveConversationToPreferredIndex(CopilotConversationRecord conversation)
        {
            var currentIndex = Conversations.IndexOf(conversation);
            if (currentIndex < 0)
                return;

            var targetIndex = conversation.IsPinned ? 0 : GetUnpinnedInsertIndex(conversation);
            if (currentIndex == targetIndex)
                return;

            Conversations.Move(currentIndex, targetIndex);
        }

        private int GetUnpinnedInsertIndex(CopilotConversationRecord? exclude = null)
        {
            var count = 0;
            foreach (var conversation in Conversations)
            {
                if (ReferenceEquals(conversation, exclude))
                    continue;

                if (!conversation.IsPinned)
                    break;

                count++;
            }

            return count;
        }

        private string BuildAttachmentContextBlock()
        {
            if (SelectedConversation == null || SelectedConversation.Attachments.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("以下是当前会话挂载的附加上下文。它们是用户明确提供的参考信息，回答时请按需使用：");

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

                builder.AppendLine($"[上下文] {attachment.DisplayLabel}");
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
            builder.AppendLine("以下是用户显式附加的业务快照。它是在点击“问 Copilot”或手动附加时抓取的固定内容；回答时应优先基于这些快照分析。")
                .AppendLine();

            foreach (var item in contextItems)
            {
                if (item == null)
                    continue;

                var title = string.IsNullOrWhiteSpace(item.Title) ? "上下文" : item.Title.Trim();
                builder.Append("## ").AppendLine(title);

                if (!string.IsNullOrWhiteSpace(item.Summary))
                    builder.Append("摘要：").AppendLine(item.Summary.Trim());

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
                    return $"[文件] {attachment.Value}\n文件当前不存在，无法读取。\n";

                var content = File.ReadAllText(attachment.Value);
                if (content.Length > AttachmentContentLimit)
                    content = content[..AttachmentContentLimit] + "\n...<已截断>";

                var fence = ResolveCodeFence(attachment.Value);
                return $"[文件] {attachment.Value}\n~~~{fence}\n{content}\n~~~\n";
            }
            catch (Exception ex)
            {
                return $"[文件] {attachment.Value}\n读取失败：{ex.Message}\n";
            }
        }

        private static string BuildImageAttachmentBlock(CopilotAttachmentItem attachment)
        {
            if (!File.Exists(attachment.Value))
                return $"[图片] {attachment.DisplayLabel}\n本地图片附件不存在：{attachment.Value}\n";

            return string.Join(Environment.NewLine, new[]
            {
                $"[图片] {attachment.DisplayLabel}",
                $"本地图片路径：{attachment.Value}",
                "当前版本会在界面显示图片预览，但不会自动把像素内容上传给模型。",
                string.Empty,
            });
        }

        private static string BuildWebPageAttachmentBlock(CopilotAttachmentItem attachment)
        {
            var content = attachment.Value ?? string.Empty;
            if (content.Length > AttachmentContentLimit)
                content = content[..AttachmentContentLimit] + "\n...<已截断>";

            return string.Join(Environment.NewLine, new[]
            {
                $"[网页] {attachment.DisplayLabel}",
                $"来源：{attachment.Source}",
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
            builder.AppendLine("[本地网页上下文注入]");
            builder.AppendLine("以下网页内容由本地程序在发送前实际抓取。你必须只基于这些抓取结果回答网页问题，不要再说无法浏览互联网；如果抓取失败，或抓取内容里没有相关信息，必须明确说明无法基于真实网页内容完成分析，不能假设网页包含未抓取到的信息。");

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
                    builder.AppendLine("...<网页上下文已截断>");
                    break;
                }

                builder.AppendLine();
                builder.AppendLine(contextBlock);
                remainingCharacters -= contextBlock.Length;

                if (remainingCharacters <= 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("...<网页上下文已截断>");
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

            builder.AppendLine("[当前窗口上下文]");

            if (!string.IsNullOrWhiteSpace(liveContext.Title))
                builder.Append("位置：").AppendLine(liveContext.Title.Trim());

            if (!string.IsNullOrWhiteSpace(liveContext.Summary))
                builder.Append("摘要：").AppendLine(liveContext.Summary.Trim());

            builder.AppendLine("以上仅是当前业务窗口的轻量摘要；若当前会话同时挂载了显式快照，请优先结合该快照回答。");
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
    }
}

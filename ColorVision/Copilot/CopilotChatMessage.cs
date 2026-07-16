using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public enum CopilotChatRole
    {
        User,
        Assistant,
    }

    public enum CopilotAttachmentType
    {
        File,
        Context,
        Image,
        WebPage,
    }

    public readonly record struct CopilotStreamDelta(string ReasoningContent, string Content)
    {
        public static CopilotStreamDelta Empty => new(string.Empty, string.Empty);

        public bool HasReasoning => !string.IsNullOrWhiteSpace(ReasoningContent);

        public bool HasContent => !string.IsNullOrWhiteSpace(Content);

        public bool HasAny => HasReasoning || HasContent;
    }

    public readonly record struct CopilotTokenUsage(int InputTokens, int OutputTokens, int TotalTokens)
    {
        public static CopilotTokenUsage Empty => new(0, 0, 0);

        public bool HasAny => InputTokens > 0 || OutputTokens > 0 || TotalTokens > 0;

        public int EffectiveTotalTokens => TotalTokens > 0 ? TotalTokens : Math.Max(0, InputTokens) + Math.Max(0, OutputTokens);

        public CopilotTokenUsage MergeProgress(CopilotTokenUsage other)
        {
            if (!HasAny)
                return other;

            if (!other.HasAny)
                return this;

            var inputTokens = other.InputTokens > 0 ? Math.Max(InputTokens, other.InputTokens) : InputTokens;
            var outputTokens = other.OutputTokens > 0 ? Math.Max(OutputTokens, other.OutputTokens) : OutputTokens;
            var totalTokens = other.TotalTokens > 0
                ? Math.Max(EffectiveTotalTokens, other.TotalTokens)
                : Math.Max(0, inputTokens) + Math.Max(0, outputTokens);

            return new CopilotTokenUsage(inputTokens, outputTokens, totalTokens);
        }

        public CopilotTokenUsage Add(CopilotTokenUsage other)
        {
            if (!HasAny)
                return other;

            if (!other.HasAny)
                return this;

            var inputTokens = Math.Max(0, InputTokens) + Math.Max(0, other.InputTokens);
            var outputTokens = Math.Max(0, OutputTokens) + Math.Max(0, other.OutputTokens);
            return new CopilotTokenUsage(inputTokens, outputTokens, inputTokens + outputTokens);
        }

        public static string FormatCount(int value)
        {
            var normalized = Math.Max(0, value);
            return normalized >= 1000
                ? $"{normalized / 1000d:0.#}k"
                : normalized.ToString();
        }
    }

    public readonly record struct CopilotChatReply(CopilotStreamDelta Delta, CopilotTokenUsage Usage)
    {
        public static CopilotChatReply Empty => new(CopilotStreamDelta.Empty, CopilotTokenUsage.Empty);

        public string ReasoningContent => Delta.ReasoningContent;

        public string Content => Delta.Content;
    }

    public sealed class CopilotChatMessage : ViewModelBase
    {
        private const int MaximumResponseInterruptionDetailLength = 800;
        private static readonly char[] ExecutionLineSeparators = { '\r', '\n' };
        private static readonly string[] ExecutionBlockSeparators = { "\r\n\r\n", "\n\n", "\r\r" };

        public CopilotChatMessage()
        {
        }

        public CopilotChatMessage(CopilotChatRole role, string content)
        {
            Role = role;
            _content = content ?? string.Empty;
            CreatedAt = DateTime.Now;
        }

        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public CopilotChatRole Role
        {
            get => _role;
            set
            {
                if (SetProperty(ref _role, value))
                {
                    OnPropertyChanged(nameof(IsUser));
                    OnPropertyChanged(nameof(Header));
                    OnPropertyChanged(nameof(HasResponseInterruption));
                }
            }
        }
        private CopilotChatRole _role;

        [JsonIgnore]
        public bool IsUser => Role == CopilotChatRole.User;

        [JsonIgnore]
        public string Header => IsUser ? CopilotUiText.UserHeader : string.IsNullOrWhiteSpace(AssistantName) ? "AI" : AssistantName;

        public string AssistantName
        {
            get => _assistantName;
            set
            {
                if (SetProperty(ref _assistantName, value ?? string.Empty))
                    OnPropertyChanged(nameof(Header));
            }
        }
        private string _assistantName = string.Empty;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                if (SetProperty(ref _createdAt, value))
                    OnPropertyChanged(nameof(TimeLabel));
            }
        }
        private DateTime _createdAt = DateTime.Now;

        [JsonIgnore]
        public string TimeLabel => CreatedAt.ToString("HH:mm");

        public string Content
        {
            get => _content;
            set
            {
                if (SetProperty(ref _content, value ?? string.Empty))
                    OnResponseTimelineChanged();
            }
        }
        private string _content = string.Empty;

        public string RequestContent
        {
            get => _requestContent;
            set => SetProperty(ref _requestContent, value ?? string.Empty);
        }
        private string _requestContent = string.Empty;

        public bool IsContentDisplayOnly
        {
            get => _isContentDisplayOnly;
            set => SetProperty(ref _isContentDisplayOnly, value);
        }
        private bool _isContentDisplayOnly;

        public bool ShouldSerializeIsContentDisplayOnly() => IsContentDisplayOnly;

        public ObservableCollection<CopilotAttachmentItem> Attachments { get; set; } = new();

        public bool AttachmentSnapshotCaptured { get; set; }

        [JsonIgnore]
        public bool HasAttachments => Attachments?.Count > 0;

        public bool ShouldSerializeAttachments() => HasAttachments;

        public bool ShouldSerializeAttachmentSnapshotCaptured() => AttachmentSnapshotCaptured;

        public CopilotAgentMode RequestMode
        {
            get => _requestMode;
            set
            {
                if (SetProperty(ref _requestMode, value))
                {
                    OnPropertyChanged(nameof(ResponseInterruptionText));
                    OnPropertyChanged(nameof(RetryActionLabel));
                    OnPropertyChanged(nameof(RetryActionToolTip));
                    OnPropertyChanged(nameof(RefreshActionToolTip));
                    OnPropertyChanged(nameof(ShowsRefreshAction));
                }
            }
        }
        private CopilotAgentMode _requestMode = CopilotAgentMode.Chat;

        [JsonIgnore]
        public string RetryActionLabel => RequestMode == CopilotAgentMode.Chat
            ? Properties.Resources.CopilotRetry
            : "重新运行";

        [JsonIgnore]
        public string RetryActionToolTip => RequestMode == CopilotAgentMode.Chat
            ? Properties.Resources.CopilotRegenerateAnswer
            : "重新执行本轮 Agent，并重新读取图片、文件、工作区和工具状态；受保护写操作仍需再次审批。";

        [JsonIgnore]
        public string RefreshActionToolTip => RequestMode == CopilotAgentMode.Chat
            ? Properties.Resources.CopilotRefreshWebContext
            : string.Empty;

        [JsonIgnore]
        public bool ShowsRefreshAction => RequestMode == CopilotAgentMode.Chat;

        public bool IsResponsePending
        {
            get => _isResponsePending;
            set
            {
                if (SetProperty(ref _isResponsePending, value))
                {
                    OnPropertyChanged(nameof(IsThinkingInProgress));
                    OnPropertyChanged(nameof(HasThinkingTrace));
                    OnPropertyChanged(nameof(ThinkingHeader));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                }
            }
        }
        private bool _isResponsePending;

        public bool WasResponseInterrupted
        {
            get => _wasResponseInterrupted;
            set
            {
                if (SetProperty(ref _wasResponseInterrupted, value))
                {
                    OnPropertyChanged(nameof(HasResponseInterruption));
                    OnPropertyChanged(nameof(ResponseInterruptionText));
                }
            }
        }
        private bool _wasResponseInterrupted;

        public string ResponseInterruptionDetail
        {
            get => _responseInterruptionDetail;
            set
            {
                var normalized = (value ?? string.Empty).Trim();
                if (normalized.Length > MaximumResponseInterruptionDetailLength)
                    normalized = normalized[..(MaximumResponseInterruptionDetailLength - 3)] + "...";
                if (SetProperty(ref _responseInterruptionDetail, normalized))
                    OnPropertyChanged(nameof(ResponseInterruptionText));
            }
        }
        private string _responseInterruptionDetail = string.Empty;

        public bool ShouldSerializeResponseInterruptionDetail() => WasResponseInterrupted && !string.IsNullOrWhiteSpace(ResponseInterruptionDetail);

        [JsonIgnore]
        public bool HasResponseInterruption => !IsUser && WasResponseInterrupted;

        [JsonIgnore]
        public string ResponseInterruptionText => !string.IsNullOrWhiteSpace(ResponseInterruptionDetail)
            ? ResponseInterruptionDetail
            : RequestMode == CopilotAgentMode.Chat
                ? "应用退出时该回答仍在生成；已保留现有内容，但回答可能不完整。"
                : "应用退出时该回答仍在生成，且没有可安全恢复的 Agent checkpoint；已保留现有内容。";

        public void MarkResponseInterrupted(string? detail = null)
        {
            ResponseInterruptionDetail = detail;
            WasResponseInterrupted = true;
        }

        [JsonIgnore]
        public string ModelContent => IsContentDisplayOnly
            ? string.Empty
            : string.IsNullOrWhiteSpace(RequestContent) ? Content : RequestContent;

        public string ExecutionContent
        {
            get => _executionContent;
            set
            {
                if (SetProperty(ref _executionContent, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasExecutionTrace));
                    OnPropertyChanged(nameof(HasExecutionFailures));
                    OnPropertyChanged(nameof(HasThinkingTrace));
                    OnPropertyChanged(nameof(HasStandaloneThinkingTrace));
                    OnPropertyChanged(nameof(HasLegacyThinkingTrace));
                    OnPropertyChanged(nameof(ThinkingContent));
                    OnPropertyChanged(nameof(HasThinkingContent));
                    OnPropertyChanged(nameof(LegacyThinkingContent));
                    OnPropertyChanged(nameof(HasLegacyThinkingContent));
                    OnPropertyChanged(nameof(ExecutionSummary));
                    OnPropertyChanged(nameof(ExecutionSummaryToolTip));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                }
            }
        }
        private string _executionContent = string.Empty;

        public ObservableCollection<CopilotAgentTraceEntry> AgentTraceEntries { get; set; } = new();

        public ObservableCollection<CopilotResponseTimelineEvent> ResponseTimelineEvents { get; set; } = new();

        private readonly ObservableCollection<CopilotResponseTimelineItem> _visibleResponseTimelineItems = new();

        public bool UsesResponseTimeline
        {
            get => _usesResponseTimeline;
            set
            {
                SetProperty(ref _usesResponseTimeline, value);
                OnResponseTimelineChanged();
            }
        }
        private bool _usesResponseTimeline;

        public CopilotAgentTaskLedgerSnapshot AgentTaskLedger
        {
            get => _agentTaskLedger;
            set
            {
                var normalized = value ?? new CopilotAgentTaskLedgerSnapshot();
                normalized.EnsureValid();
                if (SetProperty(ref _agentTaskLedger, normalized))
                    OnAgentTaskStateChanged();
            }
        }
        private CopilotAgentTaskLedgerSnapshot _agentTaskLedger = new();

        public CopilotAgentStopReason AgentStopReason
        {
            get => _agentStopReason;
            set
            {
                var normalized = Enum.IsDefined(value) ? value : CopilotAgentStopReason.None;
                if (SetProperty(ref _agentStopReason, normalized))
                    OnAgentTaskStateChanged();
            }
        }
        private CopilotAgentStopReason _agentStopReason;

        public IReadOnlyList<CopilotAgentBlockerSnapshot> AgentBlockers
        {
            get => _agentBlockers;
            set
            {
                var normalized = (value ?? Array.Empty<CopilotAgentBlockerSnapshot>())
                    .Where(item => item?.IsStructurallyValid() == true)
                    .Take(8)
                    .ToArray();
                if (SetProperty(ref _agentBlockers, normalized))
                    OnAgentTaskStateChanged();
            }
        }
        private IReadOnlyList<CopilotAgentBlockerSnapshot> _agentBlockers = Array.Empty<CopilotAgentBlockerSnapshot>();

        [JsonIgnore]
        public CopilotAgentRecoveryRequest? RecoveryRequest { get; set; }

        [JsonIgnore]
        public bool HasAgentTaskLedger => !IsUser && AgentTaskLedger.TotalCount > 0;

        [JsonIgnore]
        public bool HasAgentTaskState => !IsUser && (HasAgentTaskLedger || HasAgentBlockers || HasRecoverableAgentTasks);

        [JsonIgnore]
        public bool HasIncompleteAgentTasks => HasAgentTaskLedger && AgentTaskLedger.RemainingCount > 0;

        [JsonIgnore]
        public bool HasRecoverableFinalAnswer => !HasIncompleteAgentTasks
            && (AgentStopReason == CopilotAgentStopReason.Interrupted
                || (AgentStopReason is (CopilotAgentStopReason.IncompleteOutput
                        or CopilotAgentStopReason.BudgetExhausted
                        or CopilotAgentStopReason.ProviderFailure)
                    && AgentBlockers.Any(blocker => blocker?.Kind == CopilotAgentBlockerKind.ProviderOutput)));

        [JsonIgnore]
        public bool HasRecoverableAgentTasks => (HasIncompleteAgentTasks
                && AgentStopReason is CopilotAgentStopReason.BudgetExhausted or CopilotAgentStopReason.TaskPassLimit or CopilotAgentStopReason.Paused)
            || (HasIncompleteAgentTasks && AgentStopReason == CopilotAgentStopReason.Interrupted)
            || HasRecoverableFinalAnswer;

        [JsonIgnore]
        public string AgentRecoveryActionLabel => HasRecoverableFinalAnswer
                ? "重试最终回答"
                : AgentTraceEntries?.LastOrDefault(entry => entry != null
            && entry.IsFailure
            && entry.RetryEligible
            && entry.Access == CopilotToolAccess.ReadOnly
            && entry.Idempotency == CopilotToolIdempotency.Idempotent) != null
                ? "重试只读检查"
                : "继续任务";

        [JsonIgnore]
        public string AgentRecoveryToolTip => HasRecoverableFinalAnswer
            ? "仅使用已保存的上下文和证据生成最终回答；不会再次调用工具"
            : "从当前 AgentSession 继续未完成任务；写操作仍需重新审批";

        [JsonIgnore]
        public bool HasAgentBlockers => !IsUser && AgentBlockers.Count > 0;

        [JsonIgnore]
        public string AgentBlockerLabel
        {
            get
            {
                if (AgentBlockers.Count == 0)
                    return string.Empty;
                var blocker = AgentBlockers[0];
                return blocker.Kind switch
                {
                    CopilotAgentBlockerKind.UserDecision => "需要您的决定",
                    CopilotAgentBlockerKind.Approval => "操作未获批准",
                    CopilotAgentBlockerKind.ProviderOutput when blocker.Code == "provider_interrupted" => "模型连接中断",
                    CopilotAgentBlockerKind.ProviderOutput => "模型未返回最终回答",
                    _ when !string.IsNullOrWhiteSpace(blocker.ToolName) => $"{blocker.ToolName} 无法继续",
                    _ => "任务暂时受阻",
                };
            }
        }

        [JsonIgnore]
        public string AgentTaskModeLabel => string.Equals(AgentTaskLedger.Mode, "plan", StringComparison.OrdinalIgnoreCase) ? "计划" : "执行";

        [JsonIgnore]
        public string AgentTaskProgressLabel => $"{AgentTaskLedger.CompletedCount}/{AgentTaskLedger.TotalCount} 已完成";

        [JsonIgnore]
        public string AgentStopReasonLabel => AgentStopReason switch
        {
            CopilotAgentStopReason.Completed => "任务完成",
            CopilotAgentStopReason.AwaitingUser => "等待用户决定",
            CopilotAgentStopReason.ApprovalDenied => "审批未通过",
            CopilotAgentStopReason.BudgetExhausted => "本轮预算已用尽",
            CopilotAgentStopReason.TaskPassLimit => "达到本轮继续上限",
            CopilotAgentStopReason.Blocked => "任务受阻",
            CopilotAgentStopReason.Paused => "任务已暂停",
            CopilotAgentStopReason.Cancelled => "任务已取消",
            CopilotAgentStopReason.IncompleteOutput => "未收到最终回答",
            CopilotAgentStopReason.ProviderFailure => "模型连接中断",
            CopilotAgentStopReason.Interrupted => "应用中断后可恢复",
            _ => "Agent 已停止",
        };

        [JsonIgnore]
        public string AgentTaskSummaryToolTip => $"Agent 任务 · {AgentTaskModeLabel} · {AgentTaskProgressLabel}{Environment.NewLine}{AgentStopReasonLabel}";

        [JsonIgnore]
        public bool HasExecutionTrace => !string.IsNullOrWhiteSpace(ExecutionContent);

        [JsonIgnore]
        public bool HasExecutionFailures => AgentTraceEntries.Any(entry => entry != null
                && entry.IsVisibleInActivity
                && IsFailedTraceState(entry.State))
            || AnalyzeExecutionTrace(FilterDisplayableExecutionContent(ExecutionContent)).FailedCount > 0;

        public bool IsExecutionExpanded
        {
            get => _isExecutionExpanded;
            set => SetProperty(ref _isExecutionExpanded, value);
        }
        private bool _isExecutionExpanded = true;

        public bool IsExecutionInProgress
        {
            get => _isExecutionInProgress;
            set
            {
                if (SetProperty(ref _isExecutionInProgress, value))
                {
                    OnPropertyChanged(nameof(IsThinkingInProgress));
                    OnPropertyChanged(nameof(HasThinkingTrace));
                    OnPropertyChanged(nameof(HasStandaloneThinkingTrace));
                    OnPropertyChanged(nameof(HasLegacyThinkingTrace));
                    OnPropertyChanged(nameof(ThinkingHeader));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                    OnPropertyChanged(nameof(ExecutionHeader));
                    OnPropertyChanged(nameof(ExecutionSummary));
                    OnPropertyChanged(nameof(ExecutionSummaryToolTip));
                }
            }
        }
        private bool _isExecutionInProgress;

        [JsonIgnore]
        public string ExecutionHeader => IsExecutionInProgress ? CopilotUiText.ExecutionInProgressHeader : CopilotUiText.ExecutionHeader;

        [JsonIgnore]
        public string ExecutionSummary
        {
            get
            {
                var visibleEntries = VisibleAgentTraceEntries;
                return visibleEntries.Count > 0
                    ? BuildAgentTraceSummary(visibleEntries, IsExecutionInProgress)
                    : BuildExecutionSummary(FilterDisplayableExecutionContent(ExecutionContent), IsExecutionInProgress);
            }
        }

        [JsonIgnore]
        public string ExecutionSummaryToolTip
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ExecutionContent))
                    return ExecutionSummary;

                return $"{ExecutionHeader}: {ExecutionSummary}{Environment.NewLine}{Environment.NewLine}{TrimForTooltip(ExecutionContent)}";
            }
        }

        public string ReasoningContent
        {
            get => _reasoningContent;
            set
            {
                if (SetProperty(ref _reasoningContent, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasReasoning));
                    OnPropertyChanged(nameof(HasThinkingTrace));
                    OnPropertyChanged(nameof(HasStandaloneThinkingTrace));
                    OnPropertyChanged(nameof(HasLegacyThinkingTrace));
                    OnPropertyChanged(nameof(ThinkingContent));
                    OnPropertyChanged(nameof(HasThinkingContent));
                    OnPropertyChanged(nameof(LegacyThinkingContent));
                    OnPropertyChanged(nameof(HasLegacyThinkingContent));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                }
            }
        }
        private string _reasoningContent = string.Empty;

        [JsonIgnore]
        public bool HasReasoning => !string.IsNullOrWhiteSpace(ReasoningContent);

        public bool IsReasoningExpanded
        {
            get => _isReasoningExpanded;
            set => SetProperty(ref _isReasoningExpanded, value);
        }
        private bool _isReasoningExpanded = true;

        public bool IsThinkingExpanded
        {
            get => _isThinkingExpanded;
            set => SetProperty(ref _isThinkingExpanded, value);
        }
        private bool _isThinkingExpanded;

        public bool IsReasoningInProgress
        {
            get => _isReasoningInProgress;
            set
            {
                if (SetProperty(ref _isReasoningInProgress, value))
                {
                    OnPropertyChanged(nameof(IsThinkingInProgress));
                    OnPropertyChanged(nameof(HasThinkingTrace));
                    OnPropertyChanged(nameof(HasStandaloneThinkingTrace));
                    OnPropertyChanged(nameof(HasLegacyThinkingTrace));
                    OnPropertyChanged(nameof(ThinkingHeader));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                    OnPropertyChanged(nameof(ReasoningHeader));
                }
            }
        }
        private bool _isReasoningInProgress;

        [JsonIgnore]
        public string ReasoningHeader => IsReasoningInProgress ? CopilotUiText.ReasoningInProgressHeader : CopilotUiText.ReasoningHeader;

        public DateTime ThinkingStartedAt
        {
            get => _thinkingStartedAt;
            set
            {
                if (SetProperty(ref _thinkingStartedAt, value))
                {
                    OnPropertyChanged(nameof(ThinkingHeader));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                }
            }
        }
        private DateTime _thinkingStartedAt;

        public DateTime ThinkingCompletedAt
        {
            get => _thinkingCompletedAt;
            set
            {
                if (SetProperty(ref _thinkingCompletedAt, value))
                {
                    OnPropertyChanged(nameof(ThinkingHeader));
                    OnPropertyChanged(nameof(ThinkingSummaryToolTip));
                }
            }
        }
        private DateTime _thinkingCompletedAt;

        [JsonIgnore]
        public bool IsThinkingInProgress => _isProcessingInProgress || IsResponsePending || IsExecutionInProgress || IsReasoningInProgress;

        private bool _isProcessingInProgress;

        [JsonIgnore]
        public bool HasThinkingTrace => HasExecutionTrace || HasReasoning || IsThinkingInProgress || ThinkingStartedAt != default;

        [JsonIgnore]
        public bool HasStandaloneThinkingTrace => HasThinkingTrace && !HasAgentTraceEntries;

        [JsonIgnore]
        public bool HasLegacyThinkingTrace => HasThinkingTrace && HasLegacyResponseLayout;

        [JsonIgnore]
        public bool HasThinkingContent => !string.IsNullOrWhiteSpace(ThinkingContent);

        [JsonIgnore]
        public bool HasAgentTraceEntries => VisibleAgentTraceEntries.Count > 0;

        [JsonIgnore]
        public IReadOnlyList<CopilotAgentTraceEntry> VisibleAgentTraceEntries => AgentTraceEntries
            .Where(entry => entry != null && entry.IsVisibleInActivity)
            .ToArray();

        [JsonIgnore]
        public IReadOnlyList<CopilotAgentTraceGroup> VisibleAgentTraceGroups => CopilotAgentTraceGroup.Create(VisibleAgentTraceEntries);

        [JsonIgnore]
        public IReadOnlyList<CopilotResponseTimelineItem> VisibleResponseTimelineItems => _visibleResponseTimelineItems;

        [JsonIgnore]
        public bool HasResponseTimeline => _visibleResponseTimelineItems.Count > 0;

        [JsonIgnore]
        public bool HasLegacyResponseLayout => !HasResponseTimeline;

        [JsonIgnore]
        public string LegacyThinkingContent => HasAgentTraceEntries ? string.Empty : BuildThinkingContent(ExecutionContent, ReasoningContent);

        [JsonIgnore]
        public bool HasLegacyThinkingContent => !string.IsNullOrWhiteSpace(LegacyThinkingContent);

        [JsonIgnore]
        public string ThinkingHeader
        {
            get
            {
                if (IsThinkingInProgress)
                    return CopilotUiText.ProcessingHeader;

                var elapsed = FormatCompletedProcessingElapsed();
                return string.IsNullOrWhiteSpace(elapsed)
                    ? CopilotUiText.ProcessedHeader
                    : $"{CopilotUiText.ProcessedHeader} {elapsed}";
            }
        }

        [JsonIgnore]
        public string ThinkingContent => HasAgentTraceEntries
            ? string.Join(Environment.NewLine, VisibleAgentTraceGroups.Select(group => group.ActivityLabel))
            : LegacyThinkingContent;

        [JsonIgnore]
        public string ThinkingSummaryToolTip => ThinkingHeader;

        public void MarkThinkingStarted()
        {
            ClearDisplayOnlyContent();
            _isProcessingInProgress = true;
            IsResponsePending = true;
            ResponseInterruptionDetail = string.Empty;
            WasResponseInterrupted = false;

            if (ThinkingStartedAt == default)
                ThinkingStartedAt = DateTime.Now;

            ThinkingCompletedAt = default;
            IsThinkingExpanded = true;
            OnPropertyChanged(nameof(IsThinkingInProgress));
            OnPropertyChanged(nameof(HasThinkingTrace));
            OnPropertyChanged(nameof(HasStandaloneThinkingTrace));
            OnPropertyChanged(nameof(HasLegacyThinkingTrace));
            OnPropertyChanged(nameof(HasThinkingContent));
            OnPropertyChanged(nameof(ThinkingHeader));
            OnPropertyChanged(nameof(ThinkingSummaryToolTip));
        }

        public void MarkThinkingCompleted()
        {
            _isProcessingInProgress = false;
            IsResponsePending = false;

            if (ThinkingStartedAt == default)
                ThinkingStartedAt = CreatedAt == default ? DateTime.Now : CreatedAt;

            if (ThinkingCompletedAt == default)
                ThinkingCompletedAt = DateTime.Now;

            IsThinkingExpanded = false;
            OnPropertyChanged(nameof(IsThinkingInProgress));
            OnPropertyChanged(nameof(HasThinkingTrace));
            OnPropertyChanged(nameof(HasStandaloneThinkingTrace));
            OnPropertyChanged(nameof(HasLegacyThinkingTrace));
            OnPropertyChanged(nameof(HasThinkingContent));
            OnPropertyChanged(nameof(ThinkingHeader));
            OnPropertyChanged(nameof(ThinkingSummaryToolTip));
        }

        public bool EnsureValid()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (_content == null)
            {
                Content = string.Empty;
                changed = true;
            }

            if (_requestContent == null)
            {
                RequestContent = string.Empty;
                changed = true;
            }

            if (_responseInterruptionDetail == null)
            {
                ResponseInterruptionDetail = string.Empty;
                changed = true;
            }
            else if (!WasResponseInterrupted && _responseInterruptionDetail.Length > 0)
            {
                ResponseInterruptionDetail = string.Empty;
                changed = true;
            }

            if (!Enum.IsDefined(RequestMode))
            {
                RequestMode = CopilotAgentMode.Chat;
                changed = true;
            }

            if (_reasoningContent == null)
            {
                ReasoningContent = string.Empty;
                changed = true;
            }

            if (_executionContent == null)
            {
                ExecutionContent = string.Empty;
                changed = true;
            }

            if (Attachments == null)
            {
                Attachments = new ObservableCollection<CopilotAttachmentItem>();
                changed = true;
            }
            for (var index = Attachments.Count - 1; index >= 0; index--)
            {
                if (Attachments[index] != null)
                    continue;

                Attachments.RemoveAt(index);
                changed = true;
            }
            foreach (var attachment in Attachments)
            {
                changed |= attachment.EnsureValid();
            }

            if (AgentTraceEntries == null)
            {
                AgentTraceEntries = new ObservableCollection<CopilotAgentTraceEntry>();
                changed = true;
            }

            if (_agentTaskLedger == null)
            {
                _agentTaskLedger = new CopilotAgentTaskLedgerSnapshot();
                changed = true;
            }
            else
            {
                changed |= _agentTaskLedger.EnsureValid();
            }

            if (!Enum.IsDefined(AgentStopReason))
            {
                AgentStopReason = CopilotAgentStopReason.None;
                changed = true;
            }

            var validBlockers = (_agentBlockers ?? Array.Empty<CopilotAgentBlockerSnapshot>())
                .Where(item => item?.IsStructurallyValid() == true)
                .Take(8)
                .ToArray();
            if (_agentBlockers == null || validBlockers.Length != _agentBlockers.Count)
            {
                _agentBlockers = validBlockers;
                changed = true;
            }

            for (var index = AgentTraceEntries.Count - 1; index >= 0; index--)
            {
                if (AgentTraceEntries[index] != null)
                    continue;

                AgentTraceEntries.RemoveAt(index);
                changed = true;
            }

            var recoveredAtUtc = DateTimeOffset.UtcNow;
            foreach (var entry in AgentTraceEntries)
                changed |= entry.EnsureValid(recoveredAtUtc);

            if (ResponseTimelineEvents == null)
            {
                ResponseTimelineEvents = new ObservableCollection<CopilotResponseTimelineEvent>();
                changed = true;
            }

            var isTimelineStructurallyValid = true;
            foreach (var timelineEvent in ResponseTimelineEvents)
            {
                if (timelineEvent == null || !timelineEvent.Normalize(out var timelineEventChanged))
                {
                    isTimelineStructurallyValid = false;
                    continue;
                }

                changed |= timelineEventChanged;
            }

            if (UsesResponseTimeline && (!isTimelineStructurallyValid || !IsResponseTimelineComplete()))
            {
                ResponseTimelineEvents.Clear();
                UsesResponseTimeline = false;
                changed = true;
            }
            else if (!UsesResponseTimeline && ResponseTimelineEvents.Count > 0)
            {
                ResponseTimelineEvents.Clear();
                changed = true;
            }

            if (AgentTraceEntries.Count > 0)
            {
                var previousExecutionContent = ExecutionContent;
                RebuildExecutionContentFromAgentTrace();
                changed |= !string.Equals(previousExecutionContent, ExecutionContent, StringComparison.Ordinal);
            }

            var wasResponsePending = IsResponsePending;
            if (wasResponsePending || IsExecutionInProgress || IsReasoningInProgress)
            {
                IsExecutionInProgress = false;
                IsReasoningInProgress = false;
                IsResponsePending = false;
                _isProcessingInProgress = false;
                if (ThinkingCompletedAt == default)
                    ThinkingCompletedAt = DateTime.Now;
                if (wasResponsePending && !IsUser)
                {
                    WasResponseInterrupted = true;
                    if (string.IsNullOrWhiteSpace(Content))
                    {
                        const string interruptedMessage = "回答因应用退出而中断，未收到可显示内容；可以重试本轮请求。";
                        if (UsesResponseTimeline)
                            AppendResponseTimelineText(interruptedMessage);
                        else
                            Content = interruptedMessage;
                        IsContentDisplayOnly = true;
                    }
                }
                changed = true;
            }

            if (IsContentDisplayOnly && (IsUser || string.IsNullOrWhiteSpace(Content)))
            {
                IsContentDisplayOnly = false;
                changed = true;
            }

            if (!IsThinkingInProgress && HasThinkingTrace)
            {
                IsThinkingExpanded = false;
                OnPropertyChanged(nameof(IsThinkingExpanded));
                OnPropertyChanged(nameof(ThinkingHeader));
            }

            if (_requestContent == null)
            {
                RequestContent = string.Empty;
                changed = true;
            }

            if (_assistantName == null)
            {
                AssistantName = string.Empty;
                changed = true;
            }

            if (!Enum.IsDefined(RequestMode))
            {
                RequestMode = CopilotAgentMode.Chat;
                changed = true;
            }

            OnResponseTimelineChanged();
            return changed;
        }

        private void OnAgentTaskStateChanged()
        {
            OnPropertyChanged(nameof(HasAgentTaskLedger));
            OnPropertyChanged(nameof(HasAgentTaskState));
            OnPropertyChanged(nameof(HasIncompleteAgentTasks));
            OnPropertyChanged(nameof(HasRecoverableFinalAnswer));
            OnPropertyChanged(nameof(HasRecoverableAgentTasks));
            OnPropertyChanged(nameof(AgentRecoveryActionLabel));
            OnPropertyChanged(nameof(AgentRecoveryToolTip));
            OnPropertyChanged(nameof(HasAgentBlockers));
            OnPropertyChanged(nameof(AgentBlockerLabel));
            OnPropertyChanged(nameof(AgentTaskModeLabel));
            OnPropertyChanged(nameof(AgentTaskProgressLabel));
            OnPropertyChanged(nameof(AgentStopReasonLabel));
            OnPropertyChanged(nameof(AgentTaskSummaryToolTip));
        }

        public void BeginResponseTimeline()
        {
            if (UsesResponseTimeline)
                return;

            ResponseTimelineEvents ??= new ObservableCollection<CopilotResponseTimelineEvent>();
            ResponseTimelineEvents.Clear();
            UsesResponseTimeline = true;
        }

        public void RecordResponseTimelineTool(string callId)
        {
            BeginResponseTimeline();
            var normalizedCallId = CopilotResponseTimelineEvent.NormalizeCallId(callId);
            if (string.IsNullOrWhiteSpace(normalizedCallId)
                || ResponseTimelineEvents.Any(item => item != null
                    && item.Kind == CopilotResponseTimelineEventKind.ToolCall
                    && string.Equals(item.CallId, normalizedCallId, StringComparison.Ordinal)))
            {
                return;
            }

            ResponseTimelineEvents.Add(CopilotResponseTimelineEvent.ToolCall(normalizedCallId));
            OnResponseTimelineChanged();
        }

        public void AppendResponseTimelineText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            BeginResponseTimeline();
            var contentStart = _content.Length;
            var lastEvent = ResponseTimelineEvents.LastOrDefault();
            var visibleMarkdownItem = _visibleResponseTimelineItems.LastOrDefault();
            if (lastEvent?.Kind != CopilotResponseTimelineEventKind.Markdown
                || lastEvent.ContentStart + lastEvent.ContentLength != contentStart
                || visibleMarkdownItem?.IsMarkdown != true)
            {
                visibleMarkdownItem = null;
            }
            if (lastEvent?.Kind == CopilotResponseTimelineEventKind.Markdown
                && lastEvent.ContentStart + lastEvent.ContentLength == contentStart)
            {
                lastEvent.ContentLength += text.Length;
            }
            else
            {
                ResponseTimelineEvents.Add(CopilotResponseTimelineEvent.Markdown(contentStart, text.Length));
            }

            _content += text;
            OnPropertyChanged(nameof(Content));
            if (visibleMarkdownItem != null)
                visibleMarkdownItem.AppendMarkdown(text);
            else
                OnResponseTimelineChanged();
        }

        public void ResetResponseTimelineText()
        {
            if (!UsesResponseTimeline)
            {
                Content = string.Empty;
                IsContentDisplayOnly = false;
                return;
            }

            for (var index = ResponseTimelineEvents.Count - 1; index >= 0; index--)
            {
                if (ResponseTimelineEvents[index]?.Kind == CopilotResponseTimelineEventKind.Markdown)
                    ResponseTimelineEvents.RemoveAt(index);
            }

            _content = string.Empty;
            IsContentDisplayOnly = false;
            OnPropertyChanged(nameof(Content));
            OnResponseTimelineChanged();
        }

        public void ClearDisplayOnlyContent()
        {
            if (!IsContentDisplayOnly)
                return;

            if (UsesResponseTimeline)
                ResetResponseTimelineText();
            else
            {
                Content = string.Empty;
                IsContentDisplayOnly = false;
            }
        }

        public void UpsertAgentTrace(CopilotAgentTraceEntry traceEntry)
        {
            ArgumentNullException.ThrowIfNull(traceEntry);
            AgentTraceEntries ??= new ObservableCollection<CopilotAgentTraceEntry>();

            var index = AgentTraceEntries
                .Select((entry, entryIndex) => new { entry, entryIndex })
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(traceEntry.CallId)
                    && string.Equals(item.entry.CallId, traceEntry.CallId, StringComparison.Ordinal))
                ?.entryIndex ?? -1;

            if (index >= 0)
                AgentTraceEntries[index] = traceEntry;
            else
                AgentTraceEntries.Add(traceEntry);

            RebuildExecutionContentFromAgentTrace();
            OnPropertyChanged(nameof(AgentRecoveryActionLabel));
            OnPropertyChanged(nameof(AgentRecoveryToolTip));
        }

        public void RebuildExecutionContentFromAgentTrace()
        {
            if (AgentTraceEntries == null || AgentTraceEntries.Count == 0)
                return;

            var blocks = AgentTraceEntries
                .Where(entry => entry != null)
                .Select(BuildAgentTraceBlock)
                .Where(block => !string.IsNullOrWhiteSpace(block));
            ExecutionContent = string.Join(Environment.NewLine + Environment.NewLine, blocks);
            OnPropertyChanged(nameof(HasAgentTraceEntries));
            OnPropertyChanged(nameof(VisibleAgentTraceEntries));
            OnPropertyChanged(nameof(VisibleAgentTraceGroups));
            OnPropertyChanged(nameof(HasStandaloneThinkingTrace));
            OnPropertyChanged(nameof(HasLegacyThinkingTrace));
            OnResponseTimelineChanged();
            OnPropertyChanged(nameof(ThinkingContent));
            OnPropertyChanged(nameof(HasThinkingContent));
            OnPropertyChanged(nameof(LegacyThinkingContent));
            OnPropertyChanged(nameof(HasLegacyThinkingContent));
            OnPropertyChanged(nameof(HasExecutionFailures));
            OnPropertyChanged(nameof(ExecutionSummary));
            OnPropertyChanged(nameof(ExecutionSummaryToolTip));
        }

        private void OnResponseTimelineChanged()
        {
            var refreshedItems = BuildResponseTimelineItems();
            var canUpdateInPlace = refreshedItems.Count == _visibleResponseTimelineItems.Count
                && refreshedItems.Select(item => item.IsMarkdown)
                    .SequenceEqual(_visibleResponseTimelineItems.Select(item => item.IsMarkdown));
            if (canUpdateInPlace)
            {
                for (var index = 0; index < refreshedItems.Count; index++)
                    _visibleResponseTimelineItems[index].UpdateFrom(refreshedItems[index]);
            }
            else
            {
                _visibleResponseTimelineItems.Clear();
                foreach (var item in refreshedItems)
                    _visibleResponseTimelineItems.Add(item);
            }

            OnPropertyChanged(nameof(VisibleResponseTimelineItems));
            OnPropertyChanged(nameof(HasResponseTimeline));
            OnPropertyChanged(nameof(HasLegacyResponseLayout));
            OnPropertyChanged(nameof(HasLegacyThinkingTrace));
        }

        private IReadOnlyList<CopilotResponseTimelineItem> BuildResponseTimelineItems()
        {
            if (!IsResponseTimelineComplete())
                return Array.Empty<CopilotResponseTimelineItem>();

            var tracesByCallId = new Dictionary<string, CopilotAgentTraceEntry>(StringComparer.Ordinal);
            foreach (var trace in AgentTraceEntries.Where(trace => trace != null && !string.IsNullOrWhiteSpace(trace.CallId)))
                tracesByCallId[trace.CallId] = trace;

            var items = new List<CopilotResponseTimelineItem>();
            for (var index = 0; index < ResponseTimelineEvents.Count;)
            {
                var timelineEvent = ResponseTimelineEvents[index];
                if (timelineEvent.Kind == CopilotResponseTimelineEventKind.Markdown)
                {
                    var markdown = Content.Substring(timelineEvent.ContentStart, timelineEvent.ContentLength);
                    if (!string.IsNullOrWhiteSpace(markdown))
                    {
                        if (items.Count > 0 && items[^1].IsMarkdown)
                            items[^1] = CopilotResponseTimelineItem.FromMarkdown(items[^1].Markdown + markdown);
                        else
                            items.Add(CopilotResponseTimelineItem.FromMarkdown(markdown));
                    }
                    index++;
                    continue;
                }

                var adjacentTraces = new List<CopilotAgentTraceEntry>();
                while (index < ResponseTimelineEvents.Count
                    && ResponseTimelineEvents[index].Kind == CopilotResponseTimelineEventKind.ToolCall)
                {
                    var toolEvent = ResponseTimelineEvents[index];
                    if (tracesByCallId.TryGetValue(toolEvent.CallId, out var trace) && trace.IsVisibleInActivity)
                        adjacentTraces.Add(trace);
                    index++;
                }

                foreach (var group in CopilotAgentTraceGroup.Create(adjacentTraces))
                    items.Add(CopilotResponseTimelineItem.FromToolGroup(group));
            }

            return items;
        }

        private bool IsResponseTimelineComplete()
        {
            if (!UsesResponseTimeline || ResponseTimelineEvents == null || AgentTraceEntries == null)
                return false;

            var expectedContentStart = 0;
            var observedCallIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var timelineEvent in ResponseTimelineEvents)
            {
                if (timelineEvent == null || !Enum.IsDefined(timelineEvent.Kind))
                    return false;

                if (timelineEvent.Kind == CopilotResponseTimelineEventKind.Markdown)
                {
                    if (timelineEvent.ContentStart != expectedContentStart
                        || timelineEvent.ContentLength <= 0
                        || timelineEvent.ContentLength > Content.Length - expectedContentStart)
                    {
                        return false;
                    }

                    expectedContentStart += timelineEvent.ContentLength;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(timelineEvent.CallId)
                    || !observedCallIds.Add(timelineEvent.CallId)
                    || !AgentTraceEntries.Any(trace => trace != null
                        && string.Equals(trace.CallId, timelineEvent.CallId, StringComparison.Ordinal)))
                {
                    return false;
                }
            }

            return expectedContentStart == Content.Length;
        }

        private static string BuildAgentTraceBlock(CopilotAgentTraceEntry entry)
        {
            return entry.DiagnosticDetails;
        }

        private static string BuildAgentTraceSummary(IReadOnlyList<CopilotAgentTraceEntry> entries, bool isInProgress)
        {
            var failedCount = entries.Count(entry => entry != null && IsFailedTraceState(entry.State));
            var latestTool = entries.LastOrDefault(entry => entry != null)?.ToolName ?? string.Empty;
            var isAwaitingApproval = entries.Any(entry => entry?.State == CopilotToolExecutionState.AwaitingApproval);
            var hasRunningTool = entries.Any(entry => entry?.State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running);
            var builder = new StringBuilder(isAwaitingApproval
                ? "Awaiting approval"
                : (isInProgress || hasRunningTool ? "Running" : "Completed"));
            builder.Append(" - ").Append(entries.Count).Append(entries.Count == 1 ? " tool" : " tools");
            if (failedCount > 0)
                builder.Append(" - ").Append(failedCount).Append(" failed");
            if (!string.IsNullOrWhiteSpace(latestTool))
                builder.Append(" - latest ").Append(TrimForInline(latestTool));
            return builder.ToString();
        }

        private static bool IsFailedTraceState(CopilotToolExecutionState state)
        {
            return state is CopilotToolExecutionState.Failed
                or CopilotToolExecutionState.TimedOut
                or CopilotToolExecutionState.Denied
                or CopilotToolExecutionState.Cancelled
                or CopilotToolExecutionState.Interrupted;
        }

        private static string FormatTraceState(CopilotToolExecutionState state) => state switch
        {
            CopilotToolExecutionState.Pending => "Pending",
            CopilotToolExecutionState.Running => "Running...",
            CopilotToolExecutionState.Completed => "Completed",
            CopilotToolExecutionState.Failed => "Failed",
            CopilotToolExecutionState.TimedOut => "Timed out",
            CopilotToolExecutionState.Denied => "Denied",
            CopilotToolExecutionState.Cancelled => "Cancelled",
            CopilotToolExecutionState.Interrupted => "Interrupted",
            CopilotToolExecutionState.AwaitingApproval => "Awaiting approval",
            _ => "Unknown",
        };

        private static string FormatTraceDuration(long durationMs)
        {
            if (durationMs < 1000)
                return $"{Math.Max(0, durationMs)}ms";

            return $"{durationMs / 1000d:0.#}s";
        }

        private static string BuildThinkingContent(string? executionContent, string? reasoningContent)
        {
            var builder = new StringBuilder();
            var execution = FilterDisplayableExecutionContent(executionContent);
            var reasoning = (reasoningContent ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(execution))
                builder.AppendLine(execution);

            if (!string.IsNullOrWhiteSpace(reasoning))
            {
                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();

                builder.AppendLine(CopilotUiText.ThinkingDetailsHeader);
                builder.AppendLine(reasoning);
            }

            return builder.ToString().TrimEnd();
        }

        private string FormatCompletedProcessingElapsed()
        {
            var startedAt = ThinkingStartedAt == default ? CreatedAt : ThinkingStartedAt;
            if (IsThinkingInProgress || startedAt == default || ThinkingCompletedAt == default || ThinkingCompletedAt < startedAt)
                return string.Empty;

            var elapsed = ThinkingCompletedAt - startedAt;
            if (elapsed.TotalSeconds < 1)
                return "<1s";

            var totalSeconds = Math.Max(1, (int)Math.Floor(elapsed.TotalSeconds));
            var hours = totalSeconds / 3600;
            var minutes = totalSeconds % 3600 / 60;
            var seconds = totalSeconds % 60;

            if (hours > 0)
                return $"{hours}h {minutes}m {seconds}s";

            return minutes > 0 ? $"{minutes}m {seconds}s" : $"{seconds}s";
        }

        private static string FilterDisplayableExecutionContent(string? content)
        {
            var text = (content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var blocks = text.Split(ExecutionBlockSeparators, StringSplitOptions.RemoveEmptyEntries);
            var keptBlocks = blocks
                .Select(FilterExecutionBlock)
                .Where(block => !string.IsNullOrWhiteSpace(block))
                .ToArray();

            return string.Join(Environment.NewLine + Environment.NewLine, keptBlocks).Trim();
        }

        private static string FilterExecutionBlock(string block)
        {
            var lines = block
                .Split(ExecutionLineSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            if (lines.Length == 0 || IsHiddenExecutionBlock(lines))
                return string.Empty;

            var keptLines = lines.Where(line => !IsHiddenExecutionLine(line)).ToArray();
            return string.Join(Environment.NewLine, keptLines).Trim();
        }

        private static bool IsHiddenExecutionBlock(string[] lines)
        {
            if (IsFailedSearchExecutionBlock(lines))
                return true;

            return lines.All(IsHiddenExecutionLine);
        }

        private static bool IsFailedSearchExecutionBlock(string[] lines)
        {
            var mentionsSearchTool = lines.Any(line =>
                line.Contains("SearchFiles", StringComparison.OrdinalIgnoreCase)
                || line.Contains("GrepText", StringComparison.OrdinalIgnoreCase)
                || line.Contains("SearchDocs", StringComparison.OrdinalIgnoreCase)
                || line.Contains("WebSearch", StringComparison.OrdinalIgnoreCase));
            if (!mentionsSearchTool)
                return false;

            return lines.Any(line =>
                line.StartsWith("Status: Failed", StringComparison.OrdinalIgnoreCase)
                || line.Contains("] Failed", StringComparison.OrdinalIgnoreCase)
                || line.Contains("] Timed out", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsHiddenExecutionLine(string line)
        {
            return line.Equals("Analyzing task...", StringComparison.OrdinalIgnoreCase)
                || line.Equals("Generating answer...", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Round ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Tool phase converged", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("No extra tools are needed", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Reused the context", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Agent Skills enabled", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Agent Skills selected", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Agent Skill history", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("MCP client", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildExecutionSummary(string? content, bool isInProgress)
        {
            var text = content ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return isInProgress ? "Starting" : string.Empty;

            var (toolCount, failedCount, latestTool) = AnalyzeExecutionTrace(text);

            if (toolCount == 0)
            {
                var firstLine = text
                    .Split(ExecutionLineSeparators, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
                return isInProgress
                    ? TrimForInline(string.IsNullOrWhiteSpace(firstLine) ? "Running" : firstLine)
                    : "Trace available";
            }

            var builder = new StringBuilder(isInProgress ? "Running" : "Completed");
            builder.Append(" - ").Append(toolCount).Append(toolCount == 1 ? " tool" : " tools");

            if (failedCount > 0)
                builder.Append(" - ").Append(failedCount).Append(" failed");

            if (!string.IsNullOrWhiteSpace(latestTool))
                builder.Append(" - latest ").Append(TrimForInline(latestTool));

            return builder.ToString();
        }

        private static (int ToolCount, int FailedCount, string LatestTool) AnalyzeExecutionTrace(string? content)
        {
            var toolCount = 0;
            var failedCount = 0;
            var latestTool = string.Empty;

            foreach (var rawLine in (content ?? string.Empty).Split(ExecutionLineSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.Length > 2 && line[0] == '[')
                {
                    var closeIndex = line.IndexOf(']');
                    if (closeIndex > 1)
                    {
                        latestTool = line[1..closeIndex].Trim();
                        toolCount++;
                    }
                }

                if (line.StartsWith("Status:", StringComparison.OrdinalIgnoreCase)
                    && line.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    failedCount++;
                }
            }

            return (toolCount, failedCount, latestTool);
        }

        private static string TrimForInline(string value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 48 ? text : text[..48] + "...";
        }

        private static string TrimForTooltip(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length <= 1600 ? text : text[..1600] + Environment.NewLine + "...";
        }
    }

    public sealed class CopilotConversationRecord : ViewModelBase
    {
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, NormalizeText(value));
        }
        private string _id = Guid.NewGuid().ToString("N");

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, NormalizeText(value));
        }
        private string _title = CopilotUiText.NewConversationTitle;

        public bool HasCustomTitle
        {
            get => _hasCustomTitle;
            set => SetProperty(ref _hasCustomTitle, value);
        }
        private bool _hasCustomTitle;

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (SetProperty(ref _isPinned, value))
                {
                    OnPropertyChanged(nameof(PinLabel));
                    OnPropertyChanged(nameof(PinMenuText));
                }
            }
        }
        private bool _isPinned;

        public string PreviewText
        {
            get => _previewText;
            set
            {
                if (SetProperty(ref _previewText, value ?? string.Empty))
                    OnPropertyChanged(nameof(ConversationListPreviewText));
            }
        }
        private string _previewText = CopilotUiText.EmptyConversationPreview;

        public string DraftText
        {
            get => _draftText;
            set
            {
                if (SetProperty(ref _draftText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasDraft));
                    OnPropertyChanged(nameof(ConversationListPreviewText));
                }
            }
        }
        private string _draftText = string.Empty;

        public bool ShouldSerializeDraftText() => HasDraft;

        public string ProfileId
        {
            get => _profileId;
            set => SetProperty(ref _profileId, NormalizeText(value));
        }
        private string _profileId = string.Empty;

        public string ProfileDisplayName
        {
            get => _profileDisplayName;
            set => SetProperty(ref _profileDisplayName, NormalizeText(value));
        }
        private string _profileDisplayName = string.Empty;

        public int LastUsageInputTokens
        {
            get => _lastUsageInputTokens;
            set => SetProperty(ref _lastUsageInputTokens, Math.Max(0, value));
        }
        private int _lastUsageInputTokens;

        public int LastUsageOutputTokens
        {
            get => _lastUsageOutputTokens;
            set => SetProperty(ref _lastUsageOutputTokens, Math.Max(0, value));
        }
        private int _lastUsageOutputTokens;

        public int LastUsageTotalTokens
        {
            get => _lastUsageTotalTokens;
            set => SetProperty(ref _lastUsageTotalTokens, Math.Max(0, value));
        }
        private int _lastUsageTotalTokens;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }
        private DateTime _createdAt = DateTime.Now;

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set
            {
                if (SetProperty(ref _updatedAt, value))
                    OnPropertyChanged(nameof(UpdatedLabel));
            }
        }
        private DateTime _updatedAt = DateTime.Now;

        public ObservableCollection<CopilotChatMessage> Messages { get; set; } = new();

        public ObservableCollection<CopilotAttachmentItem> Attachments { get; set; } = new();

        public CopilotAgentSessionCheckpoint? AgentSessionCheckpoint { get; set; }

        public CopilotConversationCompaction? Compaction { get; set; }

        [JsonIgnore]
        public string UpdatedLabel => UpdatedAt.Date == DateTime.Today ? UpdatedAt.ToString("HH:mm") : UpdatedAt.ToString("M/d");

        [JsonIgnore]
        public bool HasDraft => !string.IsNullOrWhiteSpace(DraftText);

        [JsonIgnore]
        public string ConversationListPreviewText => HasDraft ? $"草稿：{BuildPreview(DraftText, 42)}" : PreviewText;

        [JsonIgnore]
        public string PinLabel => IsPinned ? CopilotUiText.PinnedLabel : string.Empty;

        [JsonIgnore]
        public string PinMenuText => IsPinned ? CopilotUiText.UnpinMenuText : CopilotUiText.PinMenuText;

        [JsonIgnore]
        public string AgentRunStatusLabel
        {
            get => _agentRunStatusLabel;
            internal set
            {
                if (SetProperty(ref _agentRunStatusLabel, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasAgentRunStatus));
            }
        }
        private string _agentRunStatusLabel = string.Empty;

        [JsonIgnore]
        public bool HasAgentRunStatus => !string.IsNullOrWhiteSpace(AgentRunStatusLabel);

        [JsonIgnore]
        public CopilotTokenUsage LastUsage => new(LastUsageInputTokens, LastUsageOutputTokens, LastUsageTotalTokens);

        public bool EnsureValid()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (UpdatedAt == default)
            {
                UpdatedAt = CreatedAt;
                changed = true;
            }

            if (_draftText == null)
            {
                DraftText = string.Empty;
                changed = true;
            }

            if (Messages == null)
            {
                Messages = new ObservableCollection<CopilotChatMessage>();
                changed = true;
            }
            if (Attachments == null)
            {
                Attachments = new ObservableCollection<CopilotAttachmentItem>();
                changed = true;
            }
            for (var index = Messages.Count - 1; index >= 0; index--)
            {
                if (Messages[index] != null)
                    continue;

                Messages.RemoveAt(index);
                changed = true;
            }
            for (var index = Attachments.Count - 1; index >= 0; index--)
            {
                if (Attachments[index] != null)
                    continue;

                Attachments.RemoveAt(index);
                changed = true;
            }
            if (AgentSessionCheckpoint != null && !AgentSessionCheckpoint.IsStructurallyValid())
            {
                AgentSessionCheckpoint = null;
                changed = true;
            }
            if (Compaction != null && !Compaction.IsStructurallyValid())
            {
                Compaction = null;
                changed = true;
            }

            var lastUserRequestMode = CopilotAgentMode.Chat;
            foreach (var message in Messages)
            {
                changed |= message.EnsureValid();
                if (message.IsUser)
                {
                    lastUserRequestMode = message.RequestMode;
                }
                else if (message.RequestMode != lastUserRequestMode)
                {
                    message.RequestMode = lastUserRequestMode;
                    changed = true;
                }
            }

            foreach (var attachment in Attachments)
            {
                changed |= attachment.EnsureValid();
            }

            return changed;
        }

        internal IEnumerable<CopilotAttachmentItem> EnumerateReferencedAttachments()
        {
            foreach (var attachment in Attachments?.Where(attachment => attachment != null) ?? Enumerable.Empty<CopilotAttachmentItem>())
                yield return attachment;

            foreach (var message in Messages?.Where(message => message != null) ?? Enumerable.Empty<CopilotChatMessage>())
            {
                foreach (var attachment in message.Attachments?.Where(attachment => attachment != null) ?? Enumerable.Empty<CopilotAttachmentItem>())
                    yield return attachment;
            }
        }

        public void Touch()
        {
            UpdatedAt = DateTime.Now;
        }

        public void SetLastUsage(CopilotTokenUsage usage)
        {
            LastUsageInputTokens = usage.InputTokens;
            LastUsageOutputTokens = usage.OutputTokens;
            LastUsageTotalTokens = usage.EffectiveTotalTokens;
        }

        public void ClearLastUsage()
        {
            LastUsageInputTokens = 0;
            LastUsageOutputTokens = 0;
            LastUsageTotalTokens = 0;
        }

        public void RefreshSummary()
        {
            var firstUserMessage = Messages.FirstOrDefault(message => message.Role == CopilotChatRole.User && !string.IsNullOrWhiteSpace(message.Content));
            var generatedTitle = firstUserMessage == null ? CopilotUiText.NewConversationTitle : BuildPreview(firstUserMessage.Content, 24);
            if (!HasCustomTitle || string.IsNullOrWhiteSpace(Title))
                Title = generatedTitle;

            var lastVisibleMessage = Messages.LastOrDefault(message => !string.IsNullOrWhiteSpace(message.Content));
            if (lastVisibleMessage != null)
            {
                PreviewText = BuildPreview(lastVisibleMessage.Content, 42);
                return;
            }

            PreviewText = Attachments.Count > 0
                ? CopilotUiText.FormatAttachmentMountedCount(Attachments.Count)
                : CopilotUiText.EmptyConversationPreview;
        }

        public void SetCustomTitle(string title)
        {
            Title = title;
            HasCustomTitle = true;
        }

        public void SetGeneratedTitle(string title)
        {
            Title = title;
            HasCustomTitle = true;
        }

        public static CopilotConversationRecord CreateEmpty(string profileId, string profileDisplayName)
        {
            return new CopilotConversationRecord
            {
                HasCustomTitle = false,
                ProfileId = profileId,
                ProfileDisplayName = profileDisplayName,
                Title = CopilotUiText.NewConversationTitle,
                PreviewText = CopilotUiText.EmptyConversationPreview,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
            };
        }

        private static string BuildPreview(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    }

    public sealed class CopilotAttachmentItem : ViewModelBase
    {
        private const int PreviewDecodePixelWidth = 256;
        private const int MaximumConcurrentPreviewLoads = 2;
        public const int MaximumStoredTextCharacters = 12_000;
        private const string StoredTextTruncationMarker = "\n...<attachment truncated>";
        private static readonly SemaphoreSlim PreviewLoadSlots = new(MaximumConcurrentPreviewLoads, MaximumConcurrentPreviewLoads);
        private readonly object _previewSync = new();

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, NormalizeText(value));
        }
        private string _id = Guid.NewGuid().ToString("N");

        public CopilotAttachmentType Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    ResetPreviewImage();
                    OnPropertyChanged(nameof(BadgeText));
                    OnPropertyChanged(nameof(DisplayLabel));
                }
            }
        }
        private CopilotAttachmentType _type;

        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, NormalizeText(value)))
                    OnPropertyChanged(nameof(DisplayLabel));
            }
        }
        private string _title = string.Empty;

        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value?.Trim() ?? string.Empty))
                {
                    ResetPreviewImage();
                    OnPropertyChanged(nameof(DisplayLabel));
                    OnPropertyChanged(nameof(TooltipText));
                }
            }
        }
        private string _value = string.Empty;

        public string Source
        {
            get => _source;
            set
            {
                if (SetProperty(ref _source, value?.Trim() ?? string.Empty))
                    OnPropertyChanged(nameof(TooltipText));
            }
        }
        private string _source = string.Empty;

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }
        private DateTime _createdAt = DateTime.Now;

        [JsonIgnore]
        public string BadgeText => Type switch
        {
            CopilotAttachmentType.File => CopilotUiText.FileBadge,
            CopilotAttachmentType.Image => CopilotUiText.ImageBadge,
            CopilotAttachmentType.WebPage => CopilotUiText.WebPageBadge,
            _ => CopilotUiText.ContextBadge,
        };

        [JsonIgnore]
        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Title))
                    return Title;

                if (Type == CopilotAttachmentType.File || Type == CopilotAttachmentType.Image)
                    return Path.GetFileName(Value);

                if (Type == CopilotAttachmentType.WebPage)
                    return TryGetHostLabel(Source);

                return BuildPreview(Value, 20);
            }
        }

        [JsonIgnore]
        public string TooltipText => Type == CopilotAttachmentType.WebPage && !string.IsNullOrWhiteSpace(Source)
            ? Source
            : Value;

        [JsonIgnore]
        public ImageSource? PreviewImage
        {
            get
            {
                string imagePath;
                int generation;
                lock (_previewSync)
                {
                    if (Type != CopilotAttachmentType.Image || string.IsNullOrWhiteSpace(Value))
                        return null;

                    imagePath = Value;
                    if (string.Equals(_previewImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
                        return _previewImage;
                    if (string.Equals(_previewLoadingPath, imagePath, StringComparison.OrdinalIgnoreCase))
                        return null;

                    _previewLoadingPath = imagePath;
                    generation = ++_previewGeneration;
                }

                _ = LoadPreviewImageAsync(imagePath, generation);
                return null;
            }
        }

        private async Task LoadPreviewImageAsync(string imagePath, int generation)
        {
            ImageSource? previewImage = null;
            var enteredLoadSlot = false;
            try
            {
                await PreviewLoadSlots.WaitAsync().ConfigureAwait(false);
                enteredLoadSlot = true;
                var bytes = await CopilotImagePayloadLoader.LoadImageBytesAsync(
                    imagePath,
                    Path.GetFileName(imagePath),
                    CancellationToken.None).ConfigureAwait(false);
                using var stream = new MemoryStream(bytes, writable: false);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.DecodePixelWidth = PreviewDecodePixelWidth;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                previewImage = image;
            }
            catch
            {
            }
            finally
            {
                if (enteredLoadSlot)
                    PreviewLoadSlots.Release();
            }

            try
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.CheckAccess())
                {
                    ApplyPreviewImage(imagePath, generation, previewImage);
                    return;
                }
                if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                    return;

                var operation = dispatcher.InvokeAsync(
                    () => ApplyPreviewImage(imagePath, generation, previewImage),
                    DispatcherPriority.Background);
                await operation.Task.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private void ApplyPreviewImage(string imagePath, int generation, ImageSource? previewImage)
        {
            lock (_previewSync)
            {
                if (generation != _previewGeneration
                    || Type != CopilotAttachmentType.Image
                    || !string.Equals(Value, imagePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _previewImage = previewImage;
                _previewImagePath = imagePath;
                _previewLoadingPath = string.Empty;
            }

            OnPropertyChanged(nameof(PreviewImage));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(ImageFallbackText));
        }

        [JsonIgnore]
        public bool HasPreviewImage => PreviewImage != null;

        [JsonIgnore]
        public bool IsImage => Type == CopilotAttachmentType.Image;

        [JsonIgnore]
        public bool IsStoredImageFile => Type == CopilotAttachmentType.Image && !string.IsNullOrWhiteSpace(Value);

        [JsonIgnore]
        public string ImageFallbackText => HasPreviewImage ? string.Empty : CopilotUiText.ImagePreviewUnavailable;

        [JsonIgnore]
        public string ImageMetaText => CreatedAt.ToString("M/d HH:mm");

        private ImageSource? _previewImage;

        private string _previewImagePath = string.Empty;

        private string _previewLoadingPath = string.Empty;

        private int _previewGeneration;

        public bool EnsureValid()
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
                changed = true;
            }

            if (CreatedAt == default)
            {
                CreatedAt = DateTime.Now;
                changed = true;
            }

            if (_value == null)
            {
                Value = string.Empty;
                changed = true;
            }
            else if (Type is CopilotAttachmentType.Context or CopilotAttachmentType.WebPage)
            {
                var normalizedValue = NormalizeStoredText(_value);
                if (!string.Equals(normalizedValue, _value, StringComparison.Ordinal))
                {
                    Value = normalizedValue;
                    changed = true;
                }
            }

            if (_title == null)
            {
                Title = string.Empty;
                changed = true;
            }

            if (_source == null)
            {
                Source = string.Empty;
                changed = true;
            }

            return changed;
        }

        internal CopilotAttachmentItem CreateSnapshot()
        {
            return new CopilotAttachmentItem
            {
                Id = Id,
                Type = Type,
                Title = Title,
                Value = Value,
                Source = Source,
                CreatedAt = CreatedAt,
            };
        }

        public static CopilotAttachmentItem CreateFile(string filePath)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.File,
                Title = Path.GetFileName(filePath),
                Value = filePath,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateContext(string text, string? title = null, string? source = null)
        {
            var normalizedText = NormalizeStoredText(text);
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.Context,
                Title = string.IsNullOrWhiteSpace(title) ? BuildPreview(normalizedText, 18) : title,
                Source = source ?? string.Empty,
                Value = normalizedText,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateImage(string imagePath, string? title = null)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.Image,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(imagePath) : title,
                Value = imagePath,
                CreatedAt = DateTime.Now,
            };
        }

        public static CopilotAttachmentItem CreateWebPage(string url, string title, string content)
        {
            return new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.WebPage,
                Title = title,
                Source = url,
                Value = NormalizeStoredText(content),
                CreatedAt = DateTime.Now,
            };
        }

        private void ResetPreviewImage()
        {
            lock (_previewSync)
            {
                _previewGeneration++;
                _previewImage = null;
                _previewImagePath = string.Empty;
                _previewLoadingPath = string.Empty;
            }
            OnPropertyChanged(nameof(PreviewImage));
            OnPropertyChanged(nameof(HasPreviewImage));
            OnPropertyChanged(nameof(ImageFallbackText));
        }

        private static string BuildPreview(string content, int maxLength)
        {
            var normalized = (content ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length <= maxLength)
                return normalized;

            return normalized[..maxLength] + "...";
        }

        internal static string NormalizeStoredText(string? value)
        {
            var source = value ?? string.Empty;
            var start = 0;
            while (start < source.Length && char.IsWhiteSpace(source[start]))
                start++;
            var end = source.Length;
            while (end > start && char.IsWhiteSpace(source[end - 1]))
                end--;

            var length = end - start;
            if (length <= MaximumStoredTextCharacters)
                return length == 0 ? string.Empty : source.Substring(start, length);

            var retainedLength = MaximumStoredTextCharacters - StoredTextTruncationMarker.Length;
            if (retainedLength > 0
                && start + retainedLength < end
                && char.IsHighSurrogate(source[start + retainedLength - 1])
                && char.IsLowSurrogate(source[start + retainedLength]))
            {
                retainedLength--;
            }
            return source.Substring(start, retainedLength).TrimEnd() + StoredTextTruncationMarker;
        }

        private static string TryGetHostLabel(string? value)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
                return uri.Host;

            return BuildPreview(value ?? string.Empty, 20);
        }

        private static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;
    }

    public readonly record struct CopilotRequestMessage(string Role, string Content);

    public sealed class CopilotProviderOption
    {
        public string Label { get; init; } = string.Empty;

        public CopilotProviderType Value { get; init; }
    }
}

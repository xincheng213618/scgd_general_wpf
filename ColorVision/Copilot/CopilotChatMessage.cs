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

    public readonly record struct CopilotTokenUsage(
        int InputTokens,
        int OutputTokens,
        int TotalTokens,
        int? CachedInputTokens = null)
    {
        public static CopilotTokenUsage Empty => new(0, 0, 0, null);

        public bool HasAny => InputTokens > 0 || OutputTokens > 0 || TotalTokens > 0;

        public int EffectiveTotalTokens => Math.Max(
            Math.Max(0, TotalTokens),
            AddClamped(InputTokens, OutputTokens));

        public int EffectiveCachedInputTokens => Math.Clamp(CachedInputTokens ?? 0, 0, Math.Max(0, InputTokens));

        public double CachedInputPercentage => InputTokens > 0
            ? EffectiveCachedInputTokens * 100d / InputTokens
            : 0d;

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
            var cachedInputTokens = other.CachedInputTokens.HasValue
                ? Math.Max(EffectiveCachedInputTokens, other.EffectiveCachedInputTokens)
                : CachedInputTokens;

            return new CopilotTokenUsage(inputTokens, outputTokens, totalTokens, cachedInputTokens);
        }

        public CopilotTokenUsage Add(CopilotTokenUsage other)
        {
            if (!HasAny)
                return other;

            if (!other.HasAny)
                return this;

            var inputTokens = AddClamped(InputTokens, other.InputTokens);
            var outputTokens = AddClamped(OutputTokens, other.OutputTokens);
            var totalTokens = AddClamped(EffectiveTotalTokens, other.EffectiveTotalTokens);
            int? cachedInputTokens = CachedInputTokens.HasValue || other.CachedInputTokens.HasValue
                ? AddClamped(EffectiveCachedInputTokens, other.EffectiveCachedInputTokens)
                : null;
            return new CopilotTokenUsage(inputTokens, outputTokens, totalTokens, cachedInputTokens);
        }

        public static string FormatCount(int value)
        {
            var normalized = Math.Max(0, value);
            return normalized >= 1000
                ? $"{normalized / 1000d:0.#}k"
                : normalized.ToString();
        }

        private static int AddClamped(int left, int right)
        {
            return (int)Math.Min(
                int.MaxValue,
                Math.Max(0L, left) + Math.Max(0L, right));
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
        internal const int MaximumAssistantTextCharacters = 262_144;
        internal const string CompressedRequestContentPrefix = "cv-request-gzip-v1:";
        internal const string ResponseTruncationMarker = "\n\n...<response truncated by app>";
        internal const string ReasoningTruncationMarker = "\n...<reasoning truncated by app>";
        private const int MinimumRequestContentCompressionCharacters = 1_024;
        private const int MaximumCompressibleRequestContentCharacters = CopilotAgentSessionCheckpoint.MaxSerializedSessionCharacters;
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
                    OnPropertyChanged(nameof(HasCompletedPlan));
                    OnPropertyChanged(nameof(HasAgentTaskState));
                }
            }
        }
        private CopilotChatRole _role;

        [JsonIgnore]
        public bool IsUser => Role == CopilotChatRole.User;

        [JsonIgnore]
        public bool IsConversationFindMatch
        {
            get => _isConversationFindMatch;
            private set => SetProperty(ref _isConversationFindMatch, value);
        }
        private bool _isConversationFindMatch;

        [JsonIgnore]
        public bool IsCurrentConversationFindMatch
        {
            get => _isCurrentConversationFindMatch;
            private set => SetProperty(ref _isCurrentConversationFindMatch, value);
        }
        private bool _isCurrentConversationFindMatch;

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

        public bool ShouldSerializeAssistantName() => !string.IsNullOrEmpty(AssistantName);

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

        public bool IsResponseContentTruncated
        {
            get => _isResponseContentTruncated;
            set
            {
                if (SetProperty(ref _isResponseContentTruncated, value))
                {
                    OnPropertyChanged(nameof(HasCompletedPlan));
                    OnPropertyChanged(nameof(HasAgentTaskState));
                }
            }
        }
        private bool _isResponseContentTruncated;

        public bool ShouldSerializeIsResponseContentTruncated() => IsResponseContentTruncated;

        [Newtonsoft.Json.JsonIgnore]
        public string RequestContent
        {
            get => _requestContent;
            set
            {
                if (SetProperty(ref _requestContent, value ?? string.Empty))
                    _requestContentPayload = string.Empty;
            }
        }
        private string _requestContent = string.Empty;
        private string _requestContentPayload = string.Empty;

        [Newtonsoft.Json.JsonProperty(nameof(RequestContent))]
        private string RequestContentPayload
        {
            get
            {
                if (_requestContentPayload.Length == 0 && _requestContent.Length > 0)
                {
                    _requestContentPayload = CopilotPersistedTextCodec.Encode(
                        _requestContent,
                        CompressedRequestContentPrefix,
                        MinimumRequestContentCompressionCharacters,
                        MaximumCompressibleRequestContentCharacters);
                }

                return _requestContentPayload;
            }
            set
            {
                var payload = value ?? string.Empty;
                _requestContent = CopilotPersistedTextCodec.Decode(
                    payload,
                    CompressedRequestContentPrefix,
                    MaximumCompressibleRequestContentCharacters);
                _requestContentPayload = CopilotPersistedTextCodec.RetainOrEncode(
                    payload,
                    _requestContent,
                    CompressedRequestContentPrefix,
                    MinimumRequestContentCompressionCharacters,
                    MaximumCompressibleRequestContentCharacters);
            }
        }

        public bool ShouldSerializeRequestContentPayload() => !string.IsNullOrEmpty(RequestContent);

        public bool IsContentDisplayOnly
        {
            get => _isContentDisplayOnly;
            set => SetProperty(ref _isContentDisplayOnly, value);
        }
        private bool _isContentDisplayOnly;

        public bool ShouldSerializeIsContentDisplayOnly() => IsContentDisplayOnly;

        public ObservableCollection<CopilotAttachmentItem> Attachments { get; set; } = new();

        public bool AttachmentSnapshotCaptured { get; set; }

        public bool ChatAttachmentContextCaptured { get; set; }

        [JsonIgnore]
        public bool HasAttachments => Attachments?.Count > 0;

        public bool ShouldSerializeAttachments() => HasAttachments;

        public bool ShouldSerializeAttachmentSnapshotCaptured() => AttachmentSnapshotCaptured;

        public bool ShouldSerializeChatAttachmentContextCaptured() => ChatAttachmentContextCaptured;

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
                    OnPropertyChanged(nameof(HasCompletedPlan));
                    OnPropertyChanged(nameof(HasAgentTaskState));
                    OnPropertyChanged(nameof(AgentTaskProgressLabel));
                    OnPropertyChanged(nameof(AgentStopReasonLabel));
                    OnPropertyChanged(nameof(AgentTaskSummaryToolTip));
                }
            }
        }
        private CopilotAgentMode _requestMode = CopilotAgentMode.Chat;

        public bool ShouldSerializeRequestMode() => RequestMode != CopilotAgentMode.Chat;

        [JsonIgnore]
        public string RetryActionLabel => RequestMode == CopilotAgentMode.Chat
            ? Properties.Resources.CopilotRetry
            : "重新运行";

        [JsonIgnore]
        public string RetryActionToolTip => RequestMode == CopilotAgentMode.Chat
            ? "使用本轮已保存的文件、图片和网页上下文重新生成回答。"
            : "重新执行本轮 Agent，并重新读取图片、文件、工作区和工具状态；受保护写操作仍需再次审批。";

        [JsonIgnore]
        public string RefreshActionToolTip => RequestMode == CopilotAgentMode.Chat
            ? "重新读取本轮文件、图片和网页上下文后生成新回答。"
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
                    OnPropertyChanged(nameof(HasCompletedPlan));
                    OnPropertyChanged(nameof(HasAgentTaskState));
                }
            }
        }
        private bool _isResponsePending;

        public bool ShouldSerializeIsResponsePending() => IsResponsePending;

        public bool WasResponseInterrupted
        {
            get => _wasResponseInterrupted;
            set
            {
                if (SetProperty(ref _wasResponseInterrupted, value))
                {
                    OnPropertyChanged(nameof(HasResponseInterruption));
                    OnPropertyChanged(nameof(ResponseInterruptionText));
                    OnPropertyChanged(nameof(HasCompletedPlan));
                    OnPropertyChanged(nameof(HasAgentTaskState));
                }
            }
        }
        private bool _wasResponseInterrupted;

        public bool ShouldSerializeWasResponseInterrupted() => WasResponseInterrupted;

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

        public bool ShouldSerializeExecutionContent() =>
            !string.IsNullOrEmpty(ExecutionContent) && (AgentTraceEntries?.Count ?? 0) == 0;

        public ObservableCollection<CopilotAgentTraceEntry> AgentTraceEntries { get; set; } = new();

        public bool ShouldSerializeAgentTraceEntries() => AgentTraceEntries?.Count > 0;

        public ObservableCollection<CopilotResponseTimelineEvent> ResponseTimelineEvents { get; set; } = new();

        public bool ShouldSerializeResponseTimelineEvents() =>
            UsesResponseTimeline && ResponseTimelineEvents?.Count > 0;

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

        public bool ShouldSerializeUsesResponseTimeline() => UsesResponseTimeline;

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

        public bool ShouldSerializeAgentTaskLedger() => AgentTaskLedger?.TotalCount > 0;

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

        public bool ShouldSerializeAgentStopReason() => AgentStopReason != CopilotAgentStopReason.None;

        public CopilotAgentBudgetSnapshot AgentRunBudget
        {
            get => _agentRunBudget;
            set
            {
                var normalized = NormalizeAgentRunBudget(value);
                if (AgentRunBudgetsEqual(_agentRunBudget, normalized))
                    return;

                _agentRunBudget = normalized;
                OnPropertyChanged();
                OnAgentRunMetricsChanged();
            }
        }
        private CopilotAgentBudgetSnapshot _agentRunBudget = new();

        public bool ShouldSerializeAgentRunBudget() => HasAgentRunMetrics;

        public int ReportedUsageInputTokens
        {
            get => _reportedUsageInputTokens;
            set => _reportedUsageInputTokens = Math.Max(0, value);
        }
        private int _reportedUsageInputTokens;

        public int ReportedUsageOutputTokens
        {
            get => _reportedUsageOutputTokens;
            set => _reportedUsageOutputTokens = Math.Max(0, value);
        }
        private int _reportedUsageOutputTokens;

        public int ReportedUsageTotalTokens
        {
            get => _reportedUsageTotalTokens;
            set => _reportedUsageTotalTokens = Math.Max(0, value);
        }
        private int _reportedUsageTotalTokens;

        public int? ReportedUsageCachedInputTokens
        {
            get => _reportedUsageCachedInputTokens;
            set => _reportedUsageCachedInputTokens = value.HasValue ? Math.Max(0, value.Value) : null;
        }
        private int? _reportedUsageCachedInputTokens;

        [JsonIgnore]
        public CopilotTokenUsage ReportedUsage => new(
            ReportedUsageInputTokens,
            ReportedUsageOutputTokens,
            ReportedUsageTotalTokens,
            ReportedUsageCachedInputTokens);

        public bool ShouldSerializeReportedUsageInputTokens() => ReportedUsageInputTokens > 0;

        public bool ShouldSerializeReportedUsageOutputTokens() => ReportedUsageOutputTokens > 0;

        public bool ShouldSerializeReportedUsageTotalTokens() => ReportedUsageTotalTokens > 0;

        public bool ShouldSerializeReportedUsageCachedInputTokens() => ReportedUsageCachedInputTokens.HasValue;

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

        public bool ShouldSerializeAgentBlockers() => AgentBlockers?.Count > 0;

        [JsonIgnore]
        public CopilotUserQuestionSnapshot? UserQuestion
        {
            get => _userQuestion;
            set
            {
                var normalized = value?.IsStructurallyValid() == true ? value : null;
                if (SetProperty(ref _userQuestion, normalized))
                {
                    OnPropertyChanged(nameof(HasUserQuestion));
                    OnPropertyChanged(nameof(HasPendingUserQuestion));
                    OnPropertyChanged(nameof(HasResolvedUserQuestion));
                    OnPropertyChanged(nameof(UserQuestionStatusText));
                }
            }
        }
        private CopilotUserQuestionSnapshot? _userQuestion;

        [JsonIgnore]
        public bool HasUserQuestion => !IsUser && UserQuestion != null;

        [JsonIgnore]
        public bool HasPendingUserQuestion => HasUserQuestion && UserQuestion!.IsPending;

        [JsonIgnore]
        public bool HasResolvedUserQuestion => HasUserQuestion && !UserQuestion!.IsPending;

        [JsonIgnore]
        public string UserQuestionStatusText => UserQuestion?.Resolution switch
        {
            CopilotUserQuestionResolution.Answered => "已回答：" + UserQuestion.Answer,
            CopilotUserQuestionResolution.Cancelled => "问题已取消",
            _ => "可选择一个选项，或在输入框中直接回答。",
        };

        [JsonIgnore]
        public CopilotAgentRecoveryRequest? RecoveryRequest { get; set; }

        [JsonIgnore]
        public bool HasAgentTaskLedger => !IsUser && AgentTaskLedger.TotalCount > 0;

        [JsonIgnore]
        public bool HasAgentTaskState => !IsUser && (HasAgentTaskLedger || HasAgentBlockers || HasRecoverableAgentTasks || HasCompletedPlan);

        [JsonIgnore]
        public bool HasCompletedPlan => CopilotPlanHandoff.IsCompletedPlan(this);

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
        public bool HasRecoverableAgentTasks => (!IsUser && AgentStopReason == CopilotAgentStopReason.Paused)
            || (HasIncompleteAgentTasks
                && AgentStopReason is CopilotAgentStopReason.BudgetExhausted
                    or CopilotAgentStopReason.TaskPassLimit
                    or CopilotAgentStopReason.Paused
                    or CopilotAgentStopReason.ProviderFailure)
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
        public string AgentTaskProgressLabel => RequestMode == CopilotAgentMode.Plan
            ? $"{AgentTaskLedger.TotalCount} 个计划步骤"
            : $"{AgentTaskLedger.CompletedCount}/{AgentTaskLedger.TotalCount} 已完成";

        [JsonIgnore]
        public string AgentStopReasonLabel => AgentStopReason switch
        {
            CopilotAgentStopReason.None when IsExecutionInProgress => "任务执行中",
            CopilotAgentStopReason.None when HasIncompleteAgentTasks => "任务尚未完成",
            CopilotAgentStopReason.Completed when RequestMode == CopilotAgentMode.Plan => "计划已生成",
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
        public bool HasAgentRunMetrics => !IsUser
            && (AgentRunBudget.ProviderCalls > 0
                || AgentRunBudget.ToolCalls > 0
                || AgentRunBudget.ConsumedTokens > 0
                || AgentRunBudget.PeakEstimatedInputTokens > 0
                || AgentRunBudget.ProviderRetryCount > 0
                || AgentRunBudget.ContextRecoveryCount > 0
                || AgentRunBudget.ReportedTotalTokens > 0
                || AgentRunBudget.ElapsedMs > 0
                || AgentRunBudget.UsedDelegatedDirectAnswer
                || AgentRunBudget.RegisteredToolCount > 0
                || AgentRunBudget.AvailableToolCount > 0
                || AgentRunBudget.AvailableToolDefinitionCharacters > 0
                || AgentRunBudget.HarnessInstructionCharacters > 0);

        [JsonIgnore]
        public string AgentRunCompactLabel
        {
            get
            {
                if (!HasAgentRunMetrics)
                    return string.Empty;

                var parts = new List<string>();
                var delegatedProviderCalls = GetDelegatedProviderCalls();
                var totalProviderCalls = Math.Max(AgentRunBudget.ProviderCalls, delegatedProviderCalls);
                if (totalProviderCalls > 0)
                {
                    parts.Add(delegatedProviderCalls > 0
                        ? $"父 {Math.Max(0, totalProviderCalls - delegatedProviderCalls)} / 子 {delegatedProviderCalls}"
                        : $"模型 {totalProviderCalls}");
                }
                var totalTokens = Math.Max(AgentRunBudget.ConsumedTokens, GetDelegatedConsumedTokens());
                if (totalTokens > 0)
                    parts.Add($"{FormatTokenCount(totalTokens)} tokens");
                if (AgentRunBudget.UsedDelegatedDirectAnswer)
                    parts.Add("委派直返");
                return string.Join(" · ", parts);
            }
        }

        [JsonIgnore]
        public string AgentRunMetricsToolTip
        {
            get
            {
                if (!HasAgentRunMetrics)
                    return string.Empty;

                var delegatedProviderCalls = GetDelegatedProviderCalls();
                var totalProviderCalls = Math.Max(AgentRunBudget.ProviderCalls, delegatedProviderCalls);
                var parentProviderCalls = Math.Max(0, totalProviderCalls - delegatedProviderCalls);
                var delegatedTokens = GetDelegatedConsumedTokens();
                var totalTokens = Math.Max(AgentRunBudget.ConsumedTokens, delegatedTokens);
                var parentTokens = Math.Max(0, totalTokens - delegatedTokens);
                var delegatedToolSurface = GetDelegatedToolSurfacePeak();
                var hasDelegatedToolSurface = delegatedToolSurface.RegisteredToolCount > 0
                    || delegatedToolSurface.AvailableToolCount > 0
                    || delegatedToolSurface.AvailableToolDefinitionCharacters > 0
                    || delegatedToolSurface.HarnessInstructionCharacters > 0;
                var builder = new StringBuilder();
                builder.Append("模型调用：").Append(totalProviderCalls);
                if (delegatedProviderCalls > 0)
                {
                    builder.Append("（父 ").Append(parentProviderCalls)
                        .Append(" / 子 ").Append(delegatedProviderCalls).Append('）');
                }
                builder.AppendLine();
                builder.Append("令牌：").Append(totalTokens.ToString("N0"));
                if (delegatedTokens > 0)
                {
                    builder.Append("（父 ").Append(parentTokens.ToString("N0"))
                        .Append(" / 子 ").Append(delegatedTokens.ToString("N0")).Append('）');
                }
                if (AgentRunBudget.RequestTokenBudget > 0)
                    builder.Append(" / ").Append(AgentRunBudget.RequestTokenBudget.ToString("N0"));
                if (AgentRunBudget.UsedEstimatedUsage)
                    builder.Append("（包含估算）");
                builder.AppendLine();
                if (AgentRunBudget.ReportedInputTokens > 0
                    || AgentRunBudget.ReportedOutputTokens > 0
                    || AgentRunBudget.ReportedTotalTokens > 0)
                {
                    builder.Append("提供商用量：输入 ")
                        .Append(AgentRunBudget.ReportedInputTokens.ToString("N0"))
                        .Append(" · 输出 ")
                        .Append(AgentRunBudget.ReportedOutputTokens.ToString("N0"))
                        .Append(" · 总计 ")
                        .Append(AgentRunBudget.ReportedTotalTokens.ToString("N0"));
                    if (AgentRunBudget.ReportedCachedInputTokens.HasValue)
                    {
                        var cachedInputTokens = Math.Clamp(
                            AgentRunBudget.ReportedCachedInputTokens.Value,
                            0,
                            AgentRunBudget.ReportedInputTokens);
                        builder.Append(" · 缓存输入 ")
                            .Append(cachedInputTokens.ToString("N0"));
                        if (AgentRunBudget.ReportedInputTokens > 0)
                        {
                            builder.Append('（')
                                .Append((cachedInputTokens * 100d / AgentRunBudget.ReportedInputTokens).ToString("0.#"))
                                .Append("%）");
                        }
                    }
                    else
                    {
                        builder.Append(" · 缓存未上报");
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.ProviderRetryCount > 0)
                {
                    builder.Append("提供商重试：")
                        .Append(AgentRunBudget.ProviderRetryCount.ToString("N0"))
                        .Append(" 次");
                    if (AgentRunBudget.ProviderRetryDelayMs > 0)
                    {
                        builder.Append(" · 计划等待 ")
                            .Append(FormatTraceDuration(AgentRunBudget.ProviderRetryDelayMs));
                    }
                    if (AgentRunBudget.ProviderRateLimitRetryCount > 0)
                    {
                        builder.Append(" · 限流 ")
                            .Append(AgentRunBudget.ProviderRateLimitRetryCount.ToString("N0"))
                            .Append(" 次");
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.ProviderFirstContentTimeoutCount > 0
                    || AgentRunBudget.ProviderStreamInactivityTimeoutCount > 0)
                {
                    builder.Append("模型停顿中止：");
                    if (AgentRunBudget.ProviderFirstContentTimeoutCount > 0)
                    {
                        builder.Append("首内容 ")
                            .Append(AgentRunBudget.ProviderFirstContentTimeoutCount.ToString("N0"))
                            .Append(" 次");
                    }
                    if (AgentRunBudget.ProviderStreamInactivityTimeoutCount > 0)
                    {
                        if (AgentRunBudget.ProviderFirstContentTimeoutCount > 0)
                            builder.Append(" · ");
                        builder.Append("流式输出 ")
                            .Append(AgentRunBudget.ProviderStreamInactivityTimeoutCount.ToString("N0"))
                            .Append(" 次");
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.ProviderResponseCount > 0
                    || AgentRunBudget.ProviderCallDurationTotalMs > 0)
                {
                    builder.Append("模型延迟：");
                    var hasLatencyValue = false;
                    if (AgentRunBudget.ProviderResponseCount > 0)
                    {
                        var averageFirstResponseLatencyMs =
                            AgentRunBudget.ProviderFirstResponseLatencyTotalMs
                            / AgentRunBudget.ProviderResponseCount;
                        builder.Append("首响应平均 ")
                            .Append(FormatTraceDuration(averageFirstResponseLatencyMs))
                            .Append(" · 最慢 ")
                            .Append(FormatTraceDuration(AgentRunBudget.ProviderFirstResponseLatencyMaxMs));
                        hasLatencyValue = true;
                        if (AgentRunBudget.ProviderResponseCount < totalProviderCalls)
                        {
                            builder.Append(" · 有效响应 ")
                                .Append(AgentRunBudget.ProviderResponseCount)
                                .Append(" / ")
                                .Append(totalProviderCalls);
                        }
                    }
                    if (AgentRunBudget.ProviderCallDurationTotalMs > 0)
                    {
                        if (hasLatencyValue)
                            builder.Append(" · ");
                        builder.Append("调用累计 ")
                            .Append(FormatTraceDuration(AgentRunBudget.ProviderCallDurationTotalMs));
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.ProviderStreamChunkCount > 0)
                {
                    builder.Append("流式输出：")
                        .Append(AgentRunBudget.ProviderStreamChunkCount.ToString("N0"))
                        .Append(" 个内容片段");
                    if (AgentRunBudget.ProviderStreamInterChunkLatencyCount > 0)
                    {
                        var averageInterChunkLatencyMs =
                            AgentRunBudget.ProviderStreamInterChunkLatencyTotalMs
                            / AgentRunBudget.ProviderStreamInterChunkLatencyCount;
                        builder.Append(" · 片段间平均 ")
                            .Append(FormatTraceDuration(averageInterChunkLatencyMs))
                            .Append(" · 最慢 ")
                            .Append(FormatTraceDuration(AgentRunBudget.ProviderStreamInterChunkLatencyMaxMs));
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.PeakEstimatedInputTokens > 0)
                {
                    builder.Append("峰值输入（估算）：")
                        .Append(AgentRunBudget.PeakEstimatedInputTokens.ToString("N0"));
                    if (AgentRunBudget.InputBudgetTokens > 0)
                    {
                        builder.Append(" / ")
                            .Append(AgentRunBudget.InputBudgetTokens.ToString("N0"));
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.ContextRecoveryCount > 0)
                {
                    builder.Append("窗口恢复：")
                        .Append(AgentRunBudget.ContextRecoveryCount.ToString("N0"))
                        .Append(" 次");
                    var recoveryInputTokensBefore = Math.Max(
                        0,
                        AgentRunBudget.ContextRecoveryEstimatedInputTokensBefore);
                    if (recoveryInputTokensBefore > 0)
                    {
                        var recoveryInputTokensAfter = Math.Clamp(
                            AgentRunBudget.ContextRecoveryEstimatedInputTokensAfter,
                            0,
                            recoveryInputTokensBefore);
                        builder.Append(" · 累计输入（估算）")
                            .Append(recoveryInputTokensBefore.ToString("N0"))
                            .Append(" → ")
                            .Append(recoveryInputTokensAfter.ToString("N0"))
                            .Append(" tokens（缩减 ")
                            .Append(((recoveryInputTokensBefore - recoveryInputTokensAfter) * 100d
                                / recoveryInputTokensBefore).ToString("0.#"))
                            .Append("%）");
                    }
                    builder.AppendLine();
                }
                var delegatedToolCalls = GetDelegatedToolCalls();
                builder.Append("工具调用：");
                if (delegatedToolCalls > 0)
                    builder.Append("父 ");
                builder.Append(AgentRunBudget.ToolCalls);
                if (AgentRunBudget.MaxToolCalls > 0)
                    builder.Append(" / ").Append(AgentRunBudget.MaxToolCalls);
                if (delegatedToolCalls > 0)
                    builder.Append(" · 子 ").Append(delegatedToolCalls);
                if (AgentRunBudget.RegisteredToolCount > 0
                    || AgentRunBudget.AvailableToolCount > 0
                    || AgentRunBudget.AvailableToolDefinitionCharacters > 0)
                {
                    builder.AppendLine();
                    builder.Append(hasDelegatedToolSurface ? "父工具面：" : "工具面：")
                        .Append(AgentRunBudget.AvailableToolCount)
                        .Append(" / ")
                        .Append(AgentRunBudget.RegisteredToolCount);
                    if (AgentRunBudget.AvailableToolDefinitionCharacters > 0)
                    {
                        builder.Append(" · 定义 ")
                            .Append(AgentRunBudget.AvailableToolDefinitionCharacters.ToString("N0"))
                            .Append(" 字符");
                    }
                }
                if (delegatedToolSurface.RegisteredToolCount > 0
                    || delegatedToolSurface.AvailableToolCount > 0
                    || delegatedToolSurface.AvailableToolDefinitionCharacters > 0)
                {
                    builder.AppendLine();
                    builder.Append("子工具面（峰值）：")
                        .Append(delegatedToolSurface.AvailableToolCount)
                        .Append(" / ")
                        .Append(delegatedToolSurface.RegisteredToolCount);
                    if (delegatedToolSurface.AvailableToolDefinitionCharacters > 0)
                    {
                        builder.Append(" · 定义 ")
                            .Append(delegatedToolSurface.AvailableToolDefinitionCharacters.ToString("N0"))
                            .Append(" 字符");
                    }
                }
                if (AgentRunBudget.HarnessInstructionCharacters > 0)
                {
                    builder.AppendLine();
                    builder.Append(hasDelegatedToolSurface ? "父运行指令：" : "运行指令：")
                        .Append(AgentRunBudget.HarnessInstructionCharacters.ToString("N0"))
                        .Append(" 字符");
                }
                if (delegatedToolSurface.HarnessInstructionCharacters > 0)
                {
                    builder.AppendLine();
                    builder.Append("子运行指令（峰值）：")
                        .Append(delegatedToolSurface.HarnessInstructionCharacters.ToString("N0"))
                        .Append(" 字符");
                }
                if (AgentRunBudget.ElapsedMs > 0)
                {
                    builder.AppendLine();
                    builder.Append("运行耗时：").Append(FormatTraceDuration(AgentRunBudget.ElapsedMs));
                    if (AgentRunBudget.TotalDurationMs > 0)
                        builder.Append(" / ").Append(FormatTraceDuration(AgentRunBudget.TotalDurationMs));
                }
                if (AgentRunBudget.UsedDelegatedDirectAnswer)
                    builder.AppendLine().Append("委派直返：是（省略第二次父级模型调用）");
                return builder.ToString();
            }
        }

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

        public bool ShouldSerializeIsExecutionExpanded() => !IsExecutionExpanded;

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
                    OnPropertyChanged(nameof(AgentStopReasonLabel));
                    OnPropertyChanged(nameof(AgentTaskSummaryToolTip));
                }
            }
        }
        private bool _isExecutionInProgress;

        public bool ShouldSerializeIsExecutionInProgress() => IsExecutionInProgress;

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

        public bool ShouldSerializeReasoningContent() => !string.IsNullOrEmpty(ReasoningContent);

        public bool IsReasoningContentTruncated { get; set; }

        public bool ShouldSerializeIsReasoningContentTruncated() => IsReasoningContentTruncated;

        [JsonIgnore]
        public bool HasReasoning => !string.IsNullOrWhiteSpace(ReasoningContent);

        public bool IsReasoningExpanded
        {
            get => _isReasoningExpanded;
            set => SetProperty(ref _isReasoningExpanded, value);
        }
        private bool _isReasoningExpanded = true;

        public bool ShouldSerializeIsReasoningExpanded() => !IsReasoningExpanded;

        public bool IsThinkingExpanded
        {
            get => _isThinkingExpanded;
            set => SetProperty(ref _isThinkingExpanded, value);
        }
        private bool _isThinkingExpanded;

        public bool ShouldSerializeIsThinkingExpanded() => IsThinkingExpanded;

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

        public bool ShouldSerializeIsReasoningInProgress() => IsReasoningInProgress;

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

        public bool ShouldSerializeThinkingStartedAt() => ThinkingStartedAt != default;

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

        public bool ShouldSerializeThinkingCompletedAt() => ThinkingCompletedAt != default;

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
                var header = string.IsNullOrWhiteSpace(elapsed)
                    ? CopilotUiText.ProcessedHeader
                    : $"{CopilotUiText.ProcessedHeader} {elapsed}";
                return string.IsNullOrWhiteSpace(AgentRunCompactLabel)
                    ? header
                    : $"{header} · {AgentRunCompactLabel}";
            }
        }

        [JsonIgnore]
        public string ThinkingContent => HasAgentTraceEntries
            ? string.Join(Environment.NewLine, VisibleAgentTraceGroups.Select(group => group.ActivityLabel))
            : LegacyThinkingContent;

        [JsonIgnore]
        public string ThinkingSummaryToolTip => string.IsNullOrWhiteSpace(AgentRunMetricsToolTip)
            ? ThinkingHeader
            : $"{ThinkingHeader}{Environment.NewLine}{AgentRunMetricsToolTip}";

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

        internal void SetConversationFindState(bool isMatch, bool isCurrent)
        {
            IsConversationFindMatch = isMatch;
            IsCurrentConversationFindMatch = isMatch && isCurrent;
        }

        internal bool SetReportedUsage(CopilotTokenUsage usage)
        {
            var inputTokens = Math.Max(0, usage.InputTokens);
            var outputTokens = Math.Max(0, usage.OutputTokens);
            var totalTokens = usage.HasAny ? usage.EffectiveTotalTokens : 0;
            int? cachedInputTokens = usage.HasAny && usage.CachedInputTokens.HasValue
                ? Math.Clamp(usage.CachedInputTokens.Value, 0, inputTokens)
                : null;
            if (ReportedUsageInputTokens == inputTokens
                && ReportedUsageOutputTokens == outputTokens
                && ReportedUsageTotalTokens == totalTokens
                && ReportedUsageCachedInputTokens == cachedInputTokens)
            {
                return false;
            }

            ReportedUsageInputTokens = inputTokens;
            ReportedUsageOutputTokens = outputTokens;
            ReportedUsageTotalTokens = totalTokens;
            ReportedUsageCachedInputTokens = cachedInputTokens;
            return true;
        }

        internal bool ClearReportedUsage()
        {
            return SetReportedUsage(CopilotTokenUsage.Empty);
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
            else if (!IsUser && _content.Length > MaximumAssistantTextCharacters)
            {
                Content = TruncateAssistantText(_content, ResponseTruncationMarker);
                IsResponseContentTruncated = true;
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
            else if (_reasoningContent.Length > MaximumAssistantTextCharacters)
            {
                ReasoningContent = TruncateAssistantText(_reasoningContent, ReasoningTruncationMarker);
                IsReasoningContentTruncated = true;
                changed = true;
            }
            if (IsUser && (IsResponseContentTruncated || IsReasoningContentTruncated))
            {
                IsResponseContentTruncated = false;
                IsReasoningContentTruncated = false;
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
            if (ChatAttachmentContextCaptured && (!HasAttachments || string.IsNullOrWhiteSpace(RequestContent)))
            {
                ChatAttachmentContextCaptured = false;
                changed = true;
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

            var normalizedAgentRunBudget = NormalizeAgentRunBudget(_agentRunBudget);
            if (!AgentRunBudgetsEqual(_agentRunBudget, normalizedAgentRunBudget))
            {
                _agentRunBudget = normalizedAgentRunBudget;
                OnAgentRunMetricsChanged();
                changed = true;
            }
            changed |= SetReportedUsage(IsUser ? CopilotTokenUsage.Empty : ReportedUsage);

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
            OnPropertyChanged(nameof(HasCompletedPlan));
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

        private void OnAgentRunMetricsChanged()
        {
            OnPropertyChanged(nameof(HasAgentRunMetrics));
            OnPropertyChanged(nameof(AgentRunCompactLabel));
            OnPropertyChanged(nameof(AgentRunMetricsToolTip));
            OnPropertyChanged(nameof(ThinkingHeader));
            OnPropertyChanged(nameof(ThinkingSummaryToolTip));
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
            IsResponseContentTruncated = false;
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

        internal bool CompleteActiveAgentTraces(
            CopilotToolExecutionState terminalState,
            CopilotToolFailureKind failureKind,
            string failureCode,
            string errorMessage,
            DateTimeOffset? completedAtUtc = null)
        {
            if (AgentTraceEntries == null || AgentTraceEntries.Count == 0)
                return false;

            var completedAt = completedAtUtc ?? DateTimeOffset.UtcNow;
            var changed = false;
            foreach (var entry in AgentTraceEntries.Where(entry => entry != null))
            {
                changed |= entry.CompleteActiveExecution(
                    terminalState,
                    failureKind,
                    failureCode,
                    errorMessage,
                    completedAt);
            }

            if (!changed)
                return false;

            RebuildExecutionContentFromAgentTrace();
            OnPropertyChanged(nameof(AgentRecoveryActionLabel));
            OnPropertyChanged(nameof(AgentRecoveryToolTip));
            return true;
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
            OnAgentRunMetricsChanged();
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
            OnPropertyChanged(nameof(HasCompletedPlan));
            OnPropertyChanged(nameof(HasAgentTaskState));
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

        private int GetDelegatedProviderCalls()
        {
            long total = 0;
            foreach (var entry in AgentTraceEntries ?? new ObservableCollection<CopilotAgentTraceEntry>())
            {
                if (entry == null)
                    continue;
                total = Math.Min(int.MaxValue, total + Math.Max(0, entry.DelegatedProviderCalls));
            }
            return (int)total;
        }

        private long GetDelegatedConsumedTokens()
        {
            long total = 0;
            foreach (var entry in AgentTraceEntries ?? new ObservableCollection<CopilotAgentTraceEntry>())
            {
                if (entry == null)
                    continue;
                var value = Math.Max(0, entry.DelegatedConsumedTokens);
                if (long.MaxValue - total < value)
                    return long.MaxValue;
                total += value;
            }
            return total;
        }

        private int GetDelegatedToolCalls()
        {
            long total = 0;
            foreach (var entry in AgentTraceEntries ?? new ObservableCollection<CopilotAgentTraceEntry>())
            {
                if (entry == null)
                    continue;
                total = Math.Min(int.MaxValue, total + Math.Max(0, entry.DelegatedToolCalls));
            }
            return (int)total;
        }

        private CopilotAgentToolSurfaceMetrics GetDelegatedToolSurfacePeak()
        {
            var registeredToolCount = 0;
            var availableToolCount = 0;
            var definitionCharacters = 0;
            var harnessInstructionCharacters = 0;
            foreach (var entry in AgentTraceEntries ?? new ObservableCollection<CopilotAgentTraceEntry>())
            {
                if (entry == null)
                    continue;

                registeredToolCount = Math.Max(registeredToolCount, entry.DelegatedRegisteredToolCount);
                availableToolCount = Math.Max(availableToolCount, entry.DelegatedAvailableToolCount);
                definitionCharacters = Math.Max(
                    definitionCharacters,
                    entry.DelegatedAvailableToolDefinitionCharacters);
                harnessInstructionCharacters = Math.Max(
                    harnessInstructionCharacters,
                    entry.DelegatedHarnessInstructionCharacters);
            }

            availableToolCount = Math.Max(0, availableToolCount);
            registeredToolCount = Math.Max(Math.Max(0, registeredToolCount), availableToolCount);
            return new CopilotAgentToolSurfaceMetrics(
                registeredToolCount,
                availableToolCount,
                Math.Max(0, definitionCharacters),
                Math.Max(0, harnessInstructionCharacters));
        }

        private static string FormatTokenCount(long value)
        {
            var normalized = Math.Max(0, value);
            return normalized >= 1_000_000
                ? $"{normalized / 1_000_000d:0.#}m"
                : normalized >= 1000
                    ? $"{normalized / 1000d:0.#}k"
                    : normalized.ToString();
        }

        private static CopilotAgentBudgetSnapshot NormalizeAgentRunBudget(CopilotAgentBudgetSnapshot? budget)
        {
            budget ??= new CopilotAgentBudgetSnapshot();
            var maxToolCalls = Math.Max(0, budget.MaxToolCalls);
            var toolCalls = Math.Max(0, budget.ToolCalls);
            if (maxToolCalls > 0)
                toolCalls = Math.Min(toolCalls, maxToolCalls);
            var reportedInputTokens = Math.Max(0, budget.ReportedInputTokens);
            var reportedOutputTokens = Math.Max(0, budget.ReportedOutputTokens);
            var reportedTotalTokens = (int)Math.Clamp(
                Math.Max(
                    (long)Math.Max(0, budget.ReportedTotalTokens),
                    (long)reportedInputTokens + reportedOutputTokens),
                0,
                int.MaxValue);
            var contextRecoveryEstimatedInputTokensBefore = Math.Max(
                0,
                budget.ContextRecoveryEstimatedInputTokensBefore);
            var providerCalls = Math.Max(0, budget.ProviderCalls);
            var providerRetryCount = Math.Clamp(
                budget.ProviderRetryCount,
                0,
                providerCalls);
            var providerFirstContentTimeoutCount = Math.Clamp(
                budget.ProviderFirstContentTimeoutCount,
                0,
                providerCalls);
            var providerStreamInactivityTimeoutCount = Math.Clamp(
                budget.ProviderStreamInactivityTimeoutCount,
                0,
                providerCalls - providerFirstContentTimeoutCount);
            var providerResponseCount = Math.Clamp(
                budget.ProviderResponseCount,
                0,
                providerCalls);
            var providerFirstResponseLatencyTotalMs = providerResponseCount > 0
                ? Math.Max(0, budget.ProviderFirstResponseLatencyTotalMs)
                : 0;
            var providerStreamChunkCount = providerResponseCount > 0
                ? Math.Max(0, budget.ProviderStreamChunkCount)
                : 0;
            var providerStreamInterChunkLatencyCount = Math.Clamp(
                budget.ProviderStreamInterChunkLatencyCount,
                0,
                Math.Max(0, providerStreamChunkCount - 1));
            var providerStreamInterChunkLatencyTotalMs = providerStreamInterChunkLatencyCount > 0
                ? Math.Max(0, budget.ProviderStreamInterChunkLatencyTotalMs)
                : 0;

            return new CopilotAgentBudgetSnapshot
            {
                CompactionEnabled = budget.CompactionEnabled,
                ContextWindowTokens = Math.Max(0, budget.ContextWindowTokens),
                InputBudgetTokens = Math.Max(0, budget.InputBudgetTokens),
                RequestTokenBudget = Math.Max(0, budget.RequestTokenBudget),
                ConsumedTokens = Math.Max(0, budget.ConsumedTokens),
                ProviderCalls = providerCalls,
                PeakEstimatedInputTokens = Math.Max(0, budget.PeakEstimatedInputTokens),
                ProviderRetryCount = providerRetryCount,
                ProviderRateLimitRetryCount = Math.Clamp(
                    budget.ProviderRateLimitRetryCount,
                    0,
                    providerRetryCount),
                ProviderRetryDelayMs = providerRetryCount > 0
                    ? Math.Max(0, budget.ProviderRetryDelayMs)
                    : 0,
                ProviderFirstContentTimeoutCount = providerFirstContentTimeoutCount,
                ProviderStreamInactivityTimeoutCount =
                    providerStreamInactivityTimeoutCount,
                ProviderResponseCount = providerResponseCount,
                ProviderFirstResponseLatencyTotalMs = providerFirstResponseLatencyTotalMs,
                ProviderFirstResponseLatencyMaxMs = Math.Clamp(
                    budget.ProviderFirstResponseLatencyMaxMs,
                    0,
                    providerFirstResponseLatencyTotalMs),
                ProviderCallDurationTotalMs = providerCalls > 0
                    ? Math.Max(
                        providerFirstResponseLatencyTotalMs,
                        budget.ProviderCallDurationTotalMs)
                    : 0,
                ProviderStreamChunkCount = providerStreamChunkCount,
                ProviderStreamInterChunkLatencyCount = providerStreamInterChunkLatencyCount,
                ProviderStreamInterChunkLatencyTotalMs = providerStreamInterChunkLatencyTotalMs,
                ProviderStreamInterChunkLatencyMaxMs = Math.Clamp(
                    budget.ProviderStreamInterChunkLatencyMaxMs,
                    0,
                    providerStreamInterChunkLatencyTotalMs),
                ContextRecoveryCount = Math.Max(0, budget.ContextRecoveryCount),
                ContextRecoveryEstimatedInputTokensBefore = contextRecoveryEstimatedInputTokensBefore,
                ContextRecoveryEstimatedInputTokensAfter = Math.Clamp(
                    budget.ContextRecoveryEstimatedInputTokensAfter,
                    0,
                    contextRecoveryEstimatedInputTokensBefore),
                ReportedInputTokens = reportedInputTokens,
                ReportedOutputTokens = reportedOutputTokens,
                ReportedTotalTokens = reportedTotalTokens,
                ReportedCachedInputTokens = reportedInputTokens > 0
                    && budget.ReportedCachedInputTokens.HasValue
                    ? Math.Clamp(budget.ReportedCachedInputTokens.Value, 0, reportedInputTokens)
                    : null,
                UsedEstimatedUsage = budget.UsedEstimatedUsage,
                UsedDelegatedDirectAnswer = budget.UsedDelegatedDirectAnswer,
                BudgetExhausted = budget.BudgetExhausted,
                RequestTokenBudgetExhausted = budget.RequestTokenBudgetExhausted,
                MaxToolCalls = maxToolCalls,
                ToolCalls = toolCalls,
                ToolBudgetExhausted = budget.ToolBudgetExhausted,
                RegisteredToolCount = Math.Max(0, budget.RegisteredToolCount),
                AvailableToolCount = Math.Clamp(
                    budget.AvailableToolCount,
                    0,
                    Math.Max(0, budget.RegisteredToolCount)),
                AvailableToolDefinitionCharacters = Math.Max(0, budget.AvailableToolDefinitionCharacters),
                HarnessInstructionCharacters = Math.Max(0, budget.HarnessInstructionCharacters),
                NarrowEvidenceResultLimit = Math.Max(0, budget.NarrowEvidenceResultLimit),
                MaxAgentPasses = Math.Max(0, budget.MaxAgentPasses),
                TotalDurationMs = Math.Max(0, budget.TotalDurationMs),
                ElapsedMs = Math.Max(0, budget.ElapsedMs),
                TimeBudgetExhausted = budget.TimeBudgetExhausted,
            };
        }

        private static bool AgentRunBudgetsEqual(
            CopilotAgentBudgetSnapshot? left,
            CopilotAgentBudgetSnapshot? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;

            return left.CompactionEnabled == right.CompactionEnabled
                && left.ContextWindowTokens == right.ContextWindowTokens
                && left.InputBudgetTokens == right.InputBudgetTokens
                && left.RequestTokenBudget == right.RequestTokenBudget
                && left.ConsumedTokens == right.ConsumedTokens
                && left.ProviderCalls == right.ProviderCalls
                && left.PeakEstimatedInputTokens == right.PeakEstimatedInputTokens
                && left.ProviderRetryCount == right.ProviderRetryCount
                && left.ProviderRateLimitRetryCount == right.ProviderRateLimitRetryCount
                && left.ProviderRetryDelayMs == right.ProviderRetryDelayMs
                && left.ProviderFirstContentTimeoutCount == right.ProviderFirstContentTimeoutCount
                && left.ProviderStreamInactivityTimeoutCount == right.ProviderStreamInactivityTimeoutCount
                && left.ProviderResponseCount == right.ProviderResponseCount
                && left.ProviderFirstResponseLatencyTotalMs == right.ProviderFirstResponseLatencyTotalMs
                && left.ProviderFirstResponseLatencyMaxMs == right.ProviderFirstResponseLatencyMaxMs
                && left.ProviderCallDurationTotalMs == right.ProviderCallDurationTotalMs
                && left.ProviderStreamChunkCount == right.ProviderStreamChunkCount
                && left.ProviderStreamInterChunkLatencyCount == right.ProviderStreamInterChunkLatencyCount
                && left.ProviderStreamInterChunkLatencyTotalMs == right.ProviderStreamInterChunkLatencyTotalMs
                && left.ProviderStreamInterChunkLatencyMaxMs == right.ProviderStreamInterChunkLatencyMaxMs
                && left.ContextRecoveryCount == right.ContextRecoveryCount
                && left.ContextRecoveryEstimatedInputTokensBefore == right.ContextRecoveryEstimatedInputTokensBefore
                && left.ContextRecoveryEstimatedInputTokensAfter == right.ContextRecoveryEstimatedInputTokensAfter
                && left.ReportedInputTokens == right.ReportedInputTokens
                && left.ReportedOutputTokens == right.ReportedOutputTokens
                && left.ReportedTotalTokens == right.ReportedTotalTokens
                && left.ReportedCachedInputTokens == right.ReportedCachedInputTokens
                && left.UsedEstimatedUsage == right.UsedEstimatedUsage
                && left.UsedDelegatedDirectAnswer == right.UsedDelegatedDirectAnswer
                && left.BudgetExhausted == right.BudgetExhausted
                && left.RequestTokenBudgetExhausted == right.RequestTokenBudgetExhausted
                && left.MaxToolCalls == right.MaxToolCalls
                && left.ToolCalls == right.ToolCalls
                && left.ToolBudgetExhausted == right.ToolBudgetExhausted
                && left.RegisteredToolCount == right.RegisteredToolCount
                && left.AvailableToolCount == right.AvailableToolCount
                && left.AvailableToolDefinitionCharacters == right.AvailableToolDefinitionCharacters
                && left.HarnessInstructionCharacters == right.HarnessInstructionCharacters
                && left.NarrowEvidenceResultLimit == right.NarrowEvidenceResultLimit
                && left.MaxAgentPasses == right.MaxAgentPasses
                && left.TotalDurationMs == right.TotalDurationMs
                && left.ElapsedMs == right.ElapsedMs
                && left.TimeBudgetExhausted == right.TimeBudgetExhausted;
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

        internal static string BoundAssistantDelta(
            int existingLength,
            string? delta,
            string truncationMarker,
            out bool truncated)
        {
            truncated = false;
            var value = delta ?? string.Empty;
            if (value.Length == 0)
                return string.Empty;

            var payloadLimit = MaximumAssistantTextCharacters - truncationMarker.Length;
            var remaining = Math.Max(0, payloadLimit - existingLength);
            if (value.Length <= remaining)
                return value;

            truncated = true;
            var retainedLength = Math.Min(remaining, value.Length);
            if (retainedLength > 0
                && retainedLength < value.Length
                && char.IsHighSurrogate(value[retainedLength - 1])
                && char.IsLowSurrogate(value[retainedLength]))
            {
                retainedLength--;
            }

            return value[..retainedLength] + truncationMarker;
        }

        private static string TruncateAssistantText(string value, string truncationMarker)
        {
            var retainedLength = MaximumAssistantTextCharacters - truncationMarker.Length;
            if (retainedLength > 0
                && retainedLength < value.Length
                && char.IsHighSurrogate(value[retainedLength - 1])
                && char.IsLowSurrogate(value[retainedLength]))
            {
                retainedLength--;
            }

            return value[..retainedLength].TrimEnd() + truncationMarker;
        }
    }

    public sealed class CopilotConversationRecord : ViewModelBase
    {
        internal const int MaximumTitleCharacters = 120;

        public string Id
        {
            get => _id;
            set
            {
                if (SetProperty(ref _id, NormalizeText(value)))
                    OnPropertyChanged(nameof(HasBranchOrigin));
            }
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

        [JsonIgnore]
        public CopilotAgentAccessMode AccessMode => _accessContext.Mode;

        [JsonIgnore]
        public bool IsFullAccessPreparedForNextTask => _accessContext.IsPreparedForNextTask;

        [JsonIgnore]
        public string FullAccessTaskId => _accessContext.GrantedTaskId;

        [JsonIgnore]
        public string FullAccessWorkspacePath => _accessContext.WorkspacePath;

        [JsonIgnore]
        public DateTimeOffset? FullAccessExpiresAtUtc => _accessContext.ExpiresAtUtc;

        // AccessMode used to be persisted as an indefinite conversation setting. Read and
        // discard that legacy property so reopening the application always restores the
        // safe per-action confirmation posture.
        [JsonProperty(nameof(AccessMode))]
        private CopilotAgentAccessMode PersistedLegacyAccessMode
        {
            set => _legacyAccessModeLoaded = true;
        }
        private bool _legacyAccessModeLoaded;

        [JsonIgnore]
        internal CopilotAgentAccessContext AccessContext => _accessContext;
        private readonly CopilotAgentAccessContext _accessContext = new();

        internal void PrepareFullAccessGrant(
            string workspacePath,
            string? taskId,
            DateTimeOffset expiresAtUtc)
        {
            _accessContext.PrepareFullAccess(Id, workspacePath, taskId, expiresAtUtc);
            NotifyAccessGrantChanged();
        }

        internal bool BindFullAccessGrantToTask(string taskId, string workspacePath)
        {
            var beforeTaskId = FullAccessTaskId;
            var beforeMode = AccessMode;
            var bound = _accessContext.BindToTask(Id, taskId, workspacePath);
            if (beforeMode != AccessMode
                || !string.Equals(beforeTaskId, FullAccessTaskId, StringComparison.Ordinal))
            {
                NotifyAccessGrantChanged();
            }
            return bound;
        }

        internal bool RevokeFullAccessGrant(string? taskId = null)
        {
            if (!_accessContext.Revoke(taskId))
                return false;

            NotifyAccessGrantChanged();
            return true;
        }

        internal bool ExpireFullAccessGrantIfNeeded()
        {
            if (!_accessContext.ExpireIfNeeded())
                return false;

            NotifyAccessGrantChanged();
            return true;
        }

        private void NotifyAccessGrantChanged()
        {
            OnPropertyChanged(nameof(AccessMode));
            OnPropertyChanged(nameof(IsFullAccessPreparedForNextTask));
            OnPropertyChanged(nameof(FullAccessTaskId));
            OnPropertyChanged(nameof(FullAccessWorkspacePath));
            OnPropertyChanged(nameof(FullAccessExpiresAtUtc));
        }

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

        public int? LastUsageCachedInputTokens
        {
            get => _lastUsageCachedInputTokens;
            set => SetProperty(ref _lastUsageCachedInputTokens, value.HasValue ? Math.Max(0, value.Value) : null);
        }
        private int? _lastUsageCachedInputTokens;

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

        public CopilotConversationBranchOrigin? BranchOrigin
        {
            get => _branchOrigin;
            set
            {
                if (SetProperty(ref _branchOrigin, value))
                {
                    OnPropertyChanged(nameof(HasBranchOrigin));
                    OnPropertyChanged(nameof(BranchLabel));
                }
            }
        }
        private CopilotConversationBranchOrigin? _branchOrigin;

        public bool ShouldSerializeBranchOrigin() => BranchOrigin != null;

        public CopilotConversationGoal? Goal
        {
            get => _goal;
            set
            {
                if (SetProperty(ref _goal, value))
                {
                    OnPropertyChanged(nameof(HasGoal));
                    OnPropertyChanged(nameof(GoalDisplayText));
                    OnPropertyChanged(nameof(GoalToolTip));
                }
            }
        }
        private CopilotConversationGoal? _goal;

        public bool ShouldSerializeGoal() => Goal != null;

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
        public bool HasBranchOrigin => BranchOrigin?.IsStructurallyValid(Id) == true;

        [JsonIgnore]
        public string BranchLabel => HasBranchOrigin ? "分支" : string.Empty;

        [JsonIgnore]
        public bool HasGoal => Goal?.IsStructurallyValid() == true;

        [JsonIgnore]
        public string GoalDisplayText => Goal == null
            ? string.Empty
            : $"{Goal.State switch
            {
                CopilotConversationGoalState.Active => "持续目标",
                CopilotConversationGoalState.Achieved => "目标已达成",
                _ => "目标已暂停",
            }} · {BuildPreview(Goal.Objective, 120)}";

        [JsonIgnore]
        public string GoalToolTip => Goal == null
            ? string.Empty
            : $"{Goal.State switch
            {
                CopilotConversationGoalState.Active => "活动目标会绑定到后续新 Agent 任务，并在每轮后独立评估。",
                CopilotConversationGoalState.Achieved => "独立完成评估已确认该目标达成。",
                _ => "该目标已暂停，不会自动启动新任务。",
            }}"
                + Environment.NewLine
                + Goal.Objective
                + Environment.NewLine
                + $"{Goal.TurnCount:N0} 轮 · {Goal.EvaluationCount:N0} 次独立评估 · {Goal.TokensUsed:N0} Token"
                + (string.IsNullOrWhiteSpace(Goal.LastEvaluationReason)
                    ? string.Empty
                    : Environment.NewLine + "最近判断：" + Goal.LastEvaluationReason)
                + Environment.NewLine
                + "目标约束完成判定，但不授权写入、工具调用、审批复用或外部副作用。";

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
        public CopilotTokenUsage LastUsage => new(
            LastUsageInputTokens,
            LastUsageOutputTokens,
            LastUsageTotalTokens,
            LastUsageCachedInputTokens);

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
            if (_legacyAccessModeLoaded)
            {
                _legacyAccessModeLoaded = false;
                changed = true;
            }
            changed |= _accessContext.Revoke();

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
            if (BranchOrigin != null && !BranchOrigin.IsStructurallyValid(Id))
            {
                BranchOrigin = null;
                changed = true;
            }
            if (Goal != null && !Goal.IsStructurallyValid())
            {
                Goal = null;
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
            var lastAssistantMessage = Messages.LastOrDefault(message =>
                !message.IsUser
                && !message.WasResponseInterrupted);
            if (lastAssistantMessage != null
                && !lastAssistantMessage.ReportedUsage.HasAny
                && LastUsage.HasAny)
            {
                changed |= lastAssistantMessage.SetReportedUsage(LastUsage);
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

        internal bool MarkWorkspaceChangeSetRolledBack(string changeSetId)
        {
            if (string.IsNullOrWhiteSpace(changeSetId))
                return false;

            var changed = false;
            foreach (var trace in Messages
                .SelectMany(message => message.AgentTraceEntries ?? new ObservableCollection<CopilotAgentTraceEntry>())
                .Where(trace => trace != null))
            {
                changed |= trace.MarkWorkspaceChangeSetRolledBack(changeSetId);
            }

            return changed;
        }

        public void SetLastUsage(CopilotTokenUsage usage)
        {
            LastUsageInputTokens = usage.InputTokens;
            LastUsageOutputTokens = usage.OutputTokens;
            LastUsageTotalTokens = usage.EffectiveTotalTokens;
            LastUsageCachedInputTokens = usage.CachedInputTokens;
        }

        public void ClearLastUsage()
        {
            LastUsageInputTokens = 0;
            LastUsageOutputTokens = 0;
            LastUsageTotalTokens = 0;
            LastUsageCachedInputTokens = null;
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

        internal static bool TryNormalizeCustomTitle(string? title, out string normalizedTitle)
        {
            normalizedTitle = NormalizeText(title);
            return normalizedTitle.Length is > 0 and <= MaximumTitleCharacters;
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
                    OnPropertyChanged(nameof(IconGlyph));
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
        public string IconGlyph => Type switch
        {
            CopilotAttachmentType.File => "\uE8A5",
            CopilotAttachmentType.Image => "\uEB9F",
            CopilotAttachmentType.WebPage => "\uE774",
            _ => "\uE723",
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

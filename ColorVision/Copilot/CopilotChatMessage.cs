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

    public sealed partial class CopilotChatMessage : ViewModelBase
    {
        internal const int MaximumAssistantTextCharacters = 262_144;
        internal const string CompressedRequestContentPrefix = "cv-request-gzip-v1:";
        internal const string ResponseTruncationMarker = "\n\n...<response truncated by app>";
        internal const string ReasoningTruncationMarker = "\n...<reasoning truncated by app>";
        internal const string ResponseInterruptionModelMarker =
            "<assistant_response_interrupted>\n"
            + "The assistant turn ended before producing a complete response. Treat any retained text as partial context, not as a completed answer. "
            + "Do not infer that unfinished steps, tool calls, file changes, or verification succeeded. Re-check current evidence before continuing.\n"
            + "</assistant_response_interrupted>";
        private const string IncompleteAgentOutcomeModelMarkerPrefix =
            "<agent_turn_incomplete stop_reason=\"";
        private const string IncompleteAgentOutcomeModelMarkerSuffix =
            "\">\n"
            + "The agent task did not reach a completed outcome. Treat any retained answer as a terminal status or partial result, not as evidence that remaining tasks, file changes, or verification succeeded. "
            + "Re-check current evidence and the unresolved task state before continuing.\n"
            + "</agent_turn_incomplete>";
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
                    OnAgentTaskStateChanged();
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
        public string ModelContent
        {
            get
            {
                var content = IsContentDisplayOnly
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(RequestContent) ? Content : RequestContent;
                if (IsUser)
                    return content;
                if (WasResponseInterrupted)
                    return AppendModelMarker(content, ResponseInterruptionModelMarker);
                if (RequestMode != CopilotAgentMode.Chat
                    && AgentStopReason is not (CopilotAgentStopReason.None or CopilotAgentStopReason.Completed))
                {
                    var marker = IncompleteAgentOutcomeModelMarkerPrefix
                        + AgentStopReason
                        + IncompleteAgentOutcomeModelMarkerSuffix;
                    return AppendModelMarker(content, marker);
                }
                return content;
            }
        }

        private static string AppendModelMarker(string content, string marker) =>
            string.IsNullOrWhiteSpace(content)
                ? marker
                : content.TrimEnd() + "\n\n" + marker;

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

        public bool IsAgentRecoveryDismissed
        {
            get => _isAgentRecoveryDismissed;
            set
            {
                if (SetProperty(ref _isAgentRecoveryDismissed, value))
                    OnAgentTaskStateChanged();
            }
        }
        private bool _isAgentRecoveryDismissed;

        public bool ShouldSerializeIsAgentRecoveryDismissed() => IsAgentRecoveryDismissed;

        [JsonIgnore]
        public bool HasAgentTaskLedger => !IsUser && AgentTaskLedger.TotalCount > 0;

        [JsonIgnore]
        public bool HasAgentTaskState => !IsUser && (HasAgentTaskLedger || HasAgentBlockers || HasRecoverableAgentTasks || HasCompletedPlan);

        [JsonIgnore]
        public bool HasCompletedPlan => CopilotPlanHandoff.IsCompletedPlan(this);

        [JsonIgnore]
        public bool HasIncompleteAgentTasks => HasAgentTaskLedger && AgentTaskLedger.RemainingCount > 0;

        [JsonIgnore]
        public bool HasRecoverableFinalAnswer => !IsAgentRecoveryDismissed
            && !HasIncompleteAgentTasks
            && ((WasResponseInterrupted && AgentStopReason == CopilotAgentStopReason.Completed)
                || AgentStopReason == CopilotAgentStopReason.Interrupted
                || (AgentStopReason is (CopilotAgentStopReason.IncompleteOutput
                        or CopilotAgentStopReason.BudgetExhausted
                        or CopilotAgentStopReason.ProviderFailure)
                    && AgentBlockers.Any(blocker => blocker?.Kind == CopilotAgentBlockerKind.ProviderOutput)));

        [JsonIgnore]
        public bool HasRecoverableAgentTasks => !IsAgentRecoveryDismissed
            && ((!IsUser && AgentStopReason == CopilotAgentStopReason.Paused)
                || (HasIncompleteAgentTasks
                    && AgentStopReason is CopilotAgentStopReason.BudgetExhausted
                        or CopilotAgentStopReason.TaskPassLimit
                        or CopilotAgentStopReason.Paused
                        or CopilotAgentStopReason.ProviderFailure)
                || (HasIncompleteAgentTasks && AgentStopReason == CopilotAgentStopReason.Interrupted)
                || HasRecoverableFinalAnswer);

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
                {
                    return string.IsNullOrWhiteSpace(AgentRunCompactLabel)
                        ? CopilotUiText.ProcessingHeader
                        : $"{CopilotUiText.ProcessingHeader} · {AgentRunCompactLabel}";
                }

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
            OnPropertyChanged(nameof(IsAgentRecoveryDismissed));
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

    }

}

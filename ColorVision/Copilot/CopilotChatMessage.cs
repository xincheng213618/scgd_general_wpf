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

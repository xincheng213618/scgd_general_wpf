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
                : AddClamped(inputTokens, outputTokens);
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
                var normalized = CopilotAgentTaskLedgerSnapshot.CreateSnapshot(
                    value,
                    normalize: true);
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

    }

}

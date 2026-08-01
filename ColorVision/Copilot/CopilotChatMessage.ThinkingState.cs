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
    public sealed partial class CopilotChatMessage
    {
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

    }
}

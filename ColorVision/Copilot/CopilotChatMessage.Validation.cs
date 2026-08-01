#pragma warning disable CA1822
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatMessage
    {
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

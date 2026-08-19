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

        internal bool CompleteActiveAgentTracesAfterUnexpectedTurnEnd(
            string turnDescription,
            DateTimeOffset? completedAtUtc = null)
        {
            if (AgentTraceEntries == null || AgentTraceEntries.Count == 0)
                return false;

            var completedAt = completedAtUtc ?? DateTimeOffset.UtcNow;
            var description = string.IsNullOrWhiteSpace(turnDescription)
                ? "The Agent turn ended unexpectedly"
                : turnDescription.Trim();
            var changed = false;
            var hasUnknownToolOutcome = false;
            foreach (var entry in AgentTraceEntries.Where(entry => entry != null))
            {
                var sourceState = entry.State;
                var entryChanged = sourceState switch
                {
                    CopilotToolExecutionState.Running => entry.CompleteActiveExecution(
                        CopilotToolExecutionState.Interrupted,
                        CopilotToolFailureKind.Internal,
                        CopilotToolFailureCode.OutcomeUnknown,
                        $"{description} after this tool execution entered the running stage but before an authoritative terminal result was saved; its external outcome is unknown.",
                        completedAt),
                    CopilotToolExecutionState.Pending => entry.CompleteActiveExecution(
                        CopilotToolExecutionState.Interrupted,
                        CopilotToolFailureKind.Internal,
                        CopilotToolFailureCode.NotStarted,
                        $"{description} before this queued tool call entered the running stage; the tool was not started.",
                        completedAt),
                    CopilotToolExecutionState.AwaitingApproval => entry.CompleteActiveExecution(
                        CopilotToolExecutionState.Interrupted,
                        CopilotToolFailureKind.Authorization,
                        CopilotToolFailureCode.ApprovalInterrupted,
                        $"{description} while this tool call awaited approval; the protected operation was not started and requires a fresh approval if requested again.",
                        completedAt),
                    _ => false,
                };
                changed |= entryChanged;
                hasUnknownToolOutcome |= entryChanged
                    && sourceState == CopilotToolExecutionState.Running;
            }

            if (!changed)
                return false;

            RebuildExecutionContentFromAgentTrace();
            OnPropertyChanged(nameof(AgentRecoveryActionLabel));
            OnPropertyChanged(nameof(AgentRecoveryToolTip));
            return hasUnknownToolOutcome;
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

    }
}

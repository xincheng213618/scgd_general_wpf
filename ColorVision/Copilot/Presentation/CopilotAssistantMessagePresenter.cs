using System;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    public enum CopilotAgentEventPersistenceMode
    {
        None,
        Deferred,
        Immediate,
    }

    public readonly record struct CopilotAgentEventPresentationResult(
        bool IsHandled,
        CopilotAgentEventPersistenceMode PersistenceMode)
    {
        public static CopilotAgentEventPresentationResult NotHandled { get; } = new(false, CopilotAgentEventPersistenceMode.None);

        public static CopilotAgentEventPresentationResult Handled(CopilotAgentEventPersistenceMode persistenceMode = CopilotAgentEventPersistenceMode.None) =>
            new(true, persistenceMode);
    }

    public static class CopilotAssistantMessagePresenter
    {
        public static CopilotAgentEventPresentationResult ApplyReviewEntered(
            CopilotChatMessage assistantMessage,
            CopilotWorkspaceReviewTargetContext target)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);
            ArgumentNullException.ThrowIfNull(target);
            if (!target.IsStructurallyValid())
                throw new ArgumentException("Review target is invalid.", nameof(target));

            assistantMessage.MarkThinkingStarted();
            AppendExecutionTrace(assistantMessage, "Review started · " + FormatReviewTarget(target));
            assistantMessage.IsExecutionInProgress = true;
            assistantMessage.IsExecutionExpanded = true;
            return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Immediate);
        }

        public static CopilotAgentEventPresentationResult ApplyReviewExited(
            CopilotChatMessage assistantMessage,
            CopilotWorkspaceReviewTargetContext target,
            string reviewText,
            bool reviewTextTruncated)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);
            ArgumentNullException.ThrowIfNull(target);
            if (!target.IsStructurallyValid())
                throw new ArgumentException("Review target is invalid.", nameof(target));
            if (!CopilotTurnAnswerLifecycleState.IsValidSnapshot(reviewText, reviewTextTruncated))
                throw new ArgumentException("Final review text snapshot is invalid.", nameof(reviewText));

            SynchronizeFinalReviewText(assistantMessage, reviewText, reviewTextTruncated);
            AppendExecutionTrace(assistantMessage, "Review completed · " + FormatReviewTarget(target));
            return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Immediate);
        }

        internal static CopilotAgentEventPresentationResult ApplyWorkspaceDiffUpdated(
            CopilotChatMessage assistantMessage,
            CopilotTurnWorkspaceDiffSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!snapshot.IsStructurallyValid())
                throw new ArgumentException("Workspace diff snapshot is invalid.", nameof(snapshot));

            assistantMessage.ApplyWorkspaceDiff(snapshot);
            return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Immediate);
        }

        internal static CopilotAgentEventPresentationResult ApplyCodeReviewSnapshotUpdated(
            CopilotChatMessage assistantMessage,
            CopilotCodeReviewSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!snapshot.IsStructurallyValid())
                throw new ArgumentException("Code review snapshot is invalid.", nameof(snapshot));

            assistantMessage.ApplyCodeReviewSnapshot(snapshot);
            return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Immediate);
        }

        public static CopilotAgentEventPresentationResult ApplyAgentEvent(CopilotChatMessage assistantMessage, CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);
            ArgumentNullException.ThrowIfNull(agentEvent);

            switch (agentEvent.Type)
            {
                case CopilotAgentEventType.Status:
                    assistantMessage.BeginResponseTimeline();
                    assistantMessage.MarkThinkingStarted();
                    assistantMessage.IsExecutionInProgress = true;
                    assistantMessage.IsExecutionExpanded = true;
                    return CopilotAgentEventPresentationResult.Handled();
                case CopilotAgentEventType.RuntimeDiagnostic:
                    assistantMessage.MarkThinkingStarted();
                    AppendExecutionTrace(assistantMessage, CopilotAgentTraceEntry.Sanitize(agentEvent.Text));
                    assistantMessage.IsExecutionInProgress = true;
                    assistantMessage.IsExecutionExpanded = true;
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.BudgetUpdated:
                    if (agentEvent.Budget != null)
                        assistantMessage.AgentRunBudget = agentEvent.Budget;
                    return CopilotAgentEventPresentationResult.Handled();
                case CopilotAgentEventType.PlanUpdated:
                    if (agentEvent.TurnPlan?.IsStructurallyValid() != true)
                        throw new InvalidOperationException("Copilot plan update has no valid snapshot.");
                    assistantMessage.AgentTaskLedger = agentEvent.TurnPlan.ToTaskLedgerSnapshot();
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Immediate);
                case CopilotAgentEventType.ToolStarted:
                    ApplyToolStarted(assistantMessage, agentEvent);
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.ToolProgress:
                    ApplyToolProgress(assistantMessage, agentEvent);
                    return CopilotAgentEventPresentationResult.Handled();
                case CopilotAgentEventType.HookStarted:
                    ApplyHookStarted(assistantMessage, agentEvent);
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.HookCompleted:
                    ApplyHookCompleted(assistantMessage, agentEvent);
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.ToolResult:
                    ApplyToolResult(assistantMessage, agentEvent);
                    return CopilotAgentEventPresentationResult.Handled(
                        agentEvent.ToolExecution?.Access == CopilotToolAccess.Write
                            ? CopilotAgentEventPersistenceMode.Immediate
                            : CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.ReasoningDelta:
                    ApplyStreamDelta(assistantMessage, new CopilotStreamDelta(agentEvent.Text, string.Empty));
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.AnswerDelta:
                    ApplyStreamDelta(assistantMessage, new CopilotStreamDelta(string.Empty, agentEvent.Text), recordResponseTimeline: true);
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.AnswerReset:
                    assistantMessage.ResetResponseTimelineText();
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.SteeringRecovery:
                    AppendExecutionTrace(assistantMessage, CopilotAgentTraceEntry.Sanitize(agentEvent.Text));
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Immediate);
                case CopilotAgentEventType.UserQuestionRequested:
                    assistantMessage.MarkThinkingStarted();
                    assistantMessage.UserQuestion = agentEvent.UserQuestion;
                    assistantMessage.IsExecutionInProgress = true;
                    return CopilotAgentEventPresentationResult.Handled();
                case CopilotAgentEventType.UserQuestionResolved:
                    if (agentEvent.UserQuestion != null
                        && string.Equals(
                            assistantMessage.UserQuestion?.RequestId,
                            agentEvent.UserQuestion.RequestId,
                            StringComparison.Ordinal))
                    {
                        assistantMessage.UserQuestion = agentEvent.UserQuestion;
                    }
                    return CopilotAgentEventPresentationResult.Handled();
                case CopilotAgentEventType.Error:
                    AppendExecutionTrace(assistantMessage, CopilotAgentTraceEntry.Sanitize(agentEvent.Text));
                    CancelPendingUserQuestion(assistantMessage);
                    CompleteThinking(assistantMessage);
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Immediate);
                case CopilotAgentEventType.Completed:
                    assistantMessage.CompleteActiveAgentTracesAfterUnexpectedTurnEnd(
                        "The Agent turn completed unexpectedly");
                    CancelPendingUserQuestion(assistantMessage);
                    CompleteThinking(assistantMessage);
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Immediate);
                default:
                    return CopilotAgentEventPresentationResult.NotHandled;
            }
        }

        public static void ApplyStreamDelta(CopilotChatMessage assistantMessage, CopilotStreamDelta delta, bool recordResponseTimeline = false)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);

            if (delta.HasReasoning)
            {
                assistantMessage.MarkThinkingStarted();
                if (!assistantMessage.IsReasoningContentTruncated)
                {
                    var boundedReasoning = CopilotChatMessage.BoundAssistantDelta(
                        assistantMessage.ReasoningContent.Length,
                        delta.ReasoningContent,
                        CopilotChatMessage.ReasoningTruncationMarker,
                        out var reasoningTruncated);
                    assistantMessage.ReasoningContent += boundedReasoning;
                    assistantMessage.IsReasoningContentTruncated = reasoningTruncated;
                }
                assistantMessage.IsReasoningInProgress = true;
                assistantMessage.IsReasoningExpanded = true;
            }

            if (!delta.HasContent)
                return;

            assistantMessage.ClearDisplayOnlyContent();
            var isFirstContentChunk = string.IsNullOrWhiteSpace(assistantMessage.Content);
            if (assistantMessage.IsResponseContentTruncated)
            {
                assistantMessage.IsReasoningInProgress = false;
                return;
            }

            var boundedContent = CopilotChatMessage.BoundAssistantDelta(
                assistantMessage.Content.Length,
                delta.Content,
                CopilotChatMessage.ResponseTruncationMarker,
                out var contentTruncated);
            if (recordResponseTimeline)
                assistantMessage.AppendResponseTimelineText(boundedContent);
            else
                assistantMessage.Content += boundedContent;
            assistantMessage.IsResponseContentTruncated = contentTruncated;
            assistantMessage.IsReasoningInProgress = false;
            if (isFirstContentChunk && assistantMessage.HasReasoning)
            {
                assistantMessage.IsReasoningExpanded = false;
                assistantMessage.IsThinkingExpanded = false;
            }
        }

        public static void AppendExecutionTrace(CopilotChatMessage assistantMessage, string text)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (!string.IsNullOrWhiteSpace(assistantMessage.ExecutionContent))
                assistantMessage.ExecutionContent += Environment.NewLine + Environment.NewLine;

            assistantMessage.ExecutionContent += text.Trim();
        }

        public static void SetFallbackContent(CopilotChatMessage assistantMessage, string text)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);
            if (!string.IsNullOrWhiteSpace(assistantMessage.Content) || string.IsNullOrWhiteSpace(text))
                return;

            if (assistantMessage.UsesResponseTimeline)
                assistantMessage.AppendResponseTimelineText(text);
            else
                assistantMessage.Content = text;
            assistantMessage.IsContentDisplayOnly = true;
        }

        public static void FinalizeMessage(CopilotChatMessage assistantMessage)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);
            CompleteThinking(assistantMessage);
            if (!string.IsNullOrWhiteSpace(assistantMessage.Content))
                return;

            SetFallbackContent(assistantMessage, assistantMessage.HasReasoning || assistantMessage.HasExecutionTrace
                ? "No final answer was received; only execution trace or reasoning content is available."
                : "The API returned successfully, but no displayable text was found.");
        }

        private static void ApplyToolStarted(CopilotChatMessage assistantMessage, CopilotAgentEvent agentEvent)
        {
            assistantMessage.MarkThinkingStarted();
            var execution = agentEvent.ToolExecution;
            if (execution != null)
            {
                var trace = CopilotAgentTraceEntry.FromStarted(execution);
                CopyObservedHookRuns(FindTrace(assistantMessage, execution.CallId), trace);
                assistantMessage.UpsertAgentTrace(trace);
                assistantMessage.RecordResponseTimelineTool(execution.CallId);
            }
            else
            {
                AppendExecutionTrace(assistantMessage, CopilotAgentTraceEntry.Sanitize(agentEvent.Text));
            }

            assistantMessage.IsExecutionInProgress = true;
            assistantMessage.IsExecutionExpanded = true;
        }

        private static void ApplyHookStarted(CopilotChatMessage assistantMessage, CopilotAgentEvent agentEvent)
        {
            var execution = agentEvent.ToolExecution
                ?? throw new InvalidOperationException("Copilot hook start has no tool execution metadata.");
            var hook = agentEvent.ToolExecutionHook;
            if (hook?.IsStructurallyValid(requireCompleted: false) != true)
                throw new InvalidOperationException("Copilot hook start has no valid lifecycle metadata.");

            assistantMessage.MarkThinkingStarted();
            var trace = CopilotAgentTraceEntry.FromProgress(
                execution,
                $"Running {FormatHookPhase(hook.Phase)} hook {hook.SourceId}...");
            CopyObservedHookRuns(FindTrace(assistantMessage, execution.CallId), trace);
            assistantMessage.UpsertAgentTrace(trace);
            assistantMessage.RecordResponseTimelineTool(execution.CallId);
            assistantMessage.IsExecutionInProgress = true;
            assistantMessage.IsExecutionExpanded = true;
        }

        private static void ApplyHookCompleted(CopilotChatMessage assistantMessage, CopilotAgentEvent agentEvent)
        {
            var execution = agentEvent.ToolExecution
                ?? throw new InvalidOperationException("Copilot hook completion has no tool execution metadata.");
            var hook = agentEvent.ToolExecutionHook;
            if (hook?.IsStructurallyValid(requireCompleted: true) != true)
                throw new InvalidOperationException("Copilot hook completion has no valid lifecycle metadata.");

            assistantMessage.MarkThinkingStarted();
            var trace = CopilotAgentTraceEntry.FromStarted(execution);
            CopyObservedHookRuns(FindTrace(assistantMessage, execution.CallId), trace);
            UpsertObservedHookRun(trace, hook.Result!);
            assistantMessage.UpsertAgentTrace(trace);
            assistantMessage.RecordResponseTimelineTool(execution.CallId);
            assistantMessage.IsExecutionInProgress = true;
            assistantMessage.IsExecutionExpanded = true;
        }

        private static void ApplyToolResult(CopilotChatMessage assistantMessage, CopilotAgentEvent agentEvent)
        {
            assistantMessage.MarkThinkingStarted();
            if (agentEvent.ToolExecution != null)
            {
                assistantMessage.UpsertAgentTrace(CopilotAgentTraceEntry.FromResult(
                    agentEvent.ToolExecution,
                    agentEvent.ToolResult,
                    agentEvent.ToolExecutionHookRuns));
                assistantMessage.RecordResponseTimelineTool(agentEvent.ToolExecution.CallId);
            }
            else
            {
                AppendExecutionTrace(assistantMessage, BuildToolTraceText(agentEvent));
            }

            assistantMessage.IsExecutionInProgress = true;
            assistantMessage.IsExecutionExpanded = true;
        }

        private static void ApplyToolProgress(CopilotChatMessage assistantMessage, CopilotAgentEvent agentEvent)
        {
            assistantMessage.MarkThinkingStarted();
            if (agentEvent.ToolExecution != null)
            {
                var trace = CopilotAgentTraceEntry.FromProgress(
                    agentEvent.ToolExecution,
                    agentEvent.Text,
                    agentEvent.Progress);
                CopyObservedHookRuns(
                    FindTrace(assistantMessage, agentEvent.ToolExecution.CallId),
                    trace);
                assistantMessage.UpsertAgentTrace(trace);
            }
            assistantMessage.IsExecutionInProgress = true;
            assistantMessage.IsExecutionExpanded = true;
        }

        private static CopilotAgentTraceEntry? FindTrace(
            CopilotChatMessage assistantMessage,
            string callId)
        {
            return assistantMessage.AgentTraceEntries?.FirstOrDefault(trace =>
                trace != null
                && !string.IsNullOrWhiteSpace(callId)
                && string.Equals(trace.CallId, callId, StringComparison.Ordinal));
        }

        private static void CopyObservedHookRuns(
            CopilotAgentTraceEntry? source,
            CopilotAgentTraceEntry target)
        {
            if (source?.HookRuns == null)
                return;

            foreach (var hookRun in source.HookRuns)
            {
                if (target.HookRuns.Count >= CopilotAgentTraceEntry.MaxPersistedHookRuns)
                    break;
                if (hookRun?.IsStructurallyValid() == true)
                    target.HookRuns.Add(hookRun.CreateSnapshot());
            }
        }

        private static void UpsertObservedHookRun(
            CopilotAgentTraceEntry trace,
            CopilotToolExecutionHookRun hookRun)
        {
            var index = trace.HookRuns.FindIndex(item =>
                item != null
                && item.Phase == hookRun.Phase
                && string.Equals(item.SourceId, hookRun.SourceId, StringComparison.Ordinal));
            if (index >= 0)
            {
                trace.HookRuns[index] = hookRun.CreateSnapshot();
            }
            else if (trace.HookRuns.Count < CopilotAgentTraceEntry.MaxPersistedHookRuns)
            {
                trace.HookRuns.Add(hookRun.CreateSnapshot());
            }
        }

        private static string FormatHookPhase(CopilotToolExecutionHookPhase phase) => phase switch
        {
            CopilotToolExecutionHookPhase.BeforeExecute => "pre-execution",
            CopilotToolExecutionHookPhase.AfterExecute => "post-execution",
            CopilotToolExecutionHookPhase.PermissionRequest => "permission",
            _ => "tool",
        };

        private static void CompleteThinking(CopilotChatMessage assistantMessage)
        {
            assistantMessage.IsExecutionInProgress = false;
            assistantMessage.IsReasoningInProgress = false;
            assistantMessage.MarkThinkingCompleted();
        }

        private static void CancelPendingUserQuestion(CopilotChatMessage assistantMessage)
        {
            if (assistantMessage.UserQuestion?.IsPending == true)
            {
                assistantMessage.UserQuestion = assistantMessage.UserQuestion.Resolve(
                    CopilotUserQuestionResolution.Cancelled,
                    string.Empty);
            }
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
                builder.AppendLine().Append("Error: ").Append(CopilotUserFacingErrorFormatter.Sanitize(result.ErrorMessage));

            return builder.ToString().TrimEnd();
        }

        private static string FormatToolDuration(long durationMs) => durationMs < 1000
            ? $"{Math.Max(0, durationMs)} ms"
            : $"{durationMs / 1000d:0.#} s";

        private static string FormatReviewTarget(CopilotWorkspaceReviewTargetContext target) => target.Target switch
        {
            CopilotWorkspaceReviewTarget.BaseBranch => "base branch " + target.Revision,
            CopilotWorkspaceReviewTarget.Commit => "commit " + target.Revision,
            _ => "working tree (staged + unstaged)",
        };

        private static void SynchronizeFinalReviewText(
            CopilotChatMessage assistantMessage,
            string reviewText,
            bool reviewTextTruncated)
        {
            if (!string.Equals(assistantMessage.Content, reviewText, StringComparison.Ordinal))
            {
                if (assistantMessage.UsesResponseTimeline)
                {
                    assistantMessage.ResetResponseTimelineText();
                    assistantMessage.AppendResponseTimelineText(reviewText);
                }
                else
                {
                    assistantMessage.Content = reviewText;
                    assistantMessage.IsContentDisplayOnly = false;
                }
            }

            assistantMessage.IsResponseContentTruncated = reviewTextTruncated;
        }
    }
}

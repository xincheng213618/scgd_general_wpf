using System;
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
                case CopilotAgentEventType.ToolStarted:
                    ApplyToolStarted(assistantMessage, agentEvent);
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.ToolResult:
                    ApplyToolResult(assistantMessage, agentEvent);
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.ReasoningDelta:
                    ApplyStreamDelta(assistantMessage, new CopilotStreamDelta(agentEvent.Text, string.Empty));
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.AnswerDelta:
                    ApplyStreamDelta(assistantMessage, new CopilotStreamDelta(string.Empty, agentEvent.Text), recordResponseTimeline: true);
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.AnswerReset:
                    assistantMessage.ResetResponseTimelineText();
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Deferred);
                case CopilotAgentEventType.Error:
                    AppendExecutionTrace(assistantMessage, CopilotAgentTraceEntry.Sanitize(agentEvent.Text));
                    CompleteThinking(assistantMessage);
                    return CopilotAgentEventPresentationResult.Handled(CopilotAgentEventPersistenceMode.Immediate);
                case CopilotAgentEventType.Completed:
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
                assistantMessage.ReasoningContent += delta.ReasoningContent;
                assistantMessage.IsReasoningInProgress = true;
                assistantMessage.IsReasoningExpanded = true;
            }

            if (!delta.HasContent)
                return;

            assistantMessage.ClearDisplayOnlyContent();
            var isFirstContentChunk = string.IsNullOrWhiteSpace(assistantMessage.Content);
            if (recordResponseTimeline)
                assistantMessage.AppendResponseTimelineText(delta.Content);
            else
                assistantMessage.Content += delta.Content;
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
                assistantMessage.UpsertAgentTrace(CopilotAgentTraceEntry.FromStarted(execution));
                assistantMessage.RecordResponseTimelineTool(execution.CallId);
            }
            else
            {
                AppendExecutionTrace(assistantMessage, CopilotAgentTraceEntry.Sanitize(agentEvent.Text));
            }

            assistantMessage.IsExecutionInProgress = true;
            assistantMessage.IsExecutionExpanded = true;
        }

        private static void ApplyToolResult(CopilotChatMessage assistantMessage, CopilotAgentEvent agentEvent)
        {
            assistantMessage.MarkThinkingStarted();
            if (agentEvent.ToolExecution != null)
            {
                assistantMessage.UpsertAgentTrace(CopilotAgentTraceEntry.FromResult(agentEvent.ToolExecution, agentEvent.ToolResult));
                assistantMessage.RecordResponseTimelineTool(agentEvent.ToolExecution.CallId);
            }
            else
            {
                AppendExecutionTrace(assistantMessage, BuildToolTraceText(agentEvent));
            }

            assistantMessage.IsExecutionInProgress = true;
            assistantMessage.IsExecutionExpanded = true;
        }

        private static void CompleteThinking(CopilotChatMessage assistantMessage)
        {
            assistantMessage.IsExecutionInProgress = false;
            assistantMessage.IsReasoningInProgress = false;
            assistantMessage.MarkThinkingCompleted();
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
    }
}

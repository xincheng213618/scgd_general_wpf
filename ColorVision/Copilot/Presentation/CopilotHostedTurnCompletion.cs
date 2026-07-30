using System;

namespace ColorVision.Copilot
{
    public static class CopilotHostedTurnCompletion
    {
        public static void CompleteTerminalTurn(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            CopilotTokenUsage usage)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            ArgumentNullException.ThrowIfNull(assistantMessage);

            assistantMessage.CompleteActiveAgentTraces(
                CopilotToolExecutionState.Interrupted,
                CopilotToolFailureKind.Internal,
                "tool_terminal_event_missing",
                "The hosted turn completed before this tool call emitted an authoritative terminal result.");
            CopilotAssistantMessagePresenter.FinalizeMessage(assistantMessage);
            SetUsage(conversation, assistantMessage, usage);
        }

        public static void CompleteCancellation(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            CopilotAgentControlIntent controlIntent)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            ArgumentNullException.ThrowIfNull(assistantMessage);

            CompleteThinking(assistantMessage);
            assistantMessage.CompleteActiveAgentTraces(
                CopilotToolExecutionState.Cancelled,
                CopilotToolFailureKind.Cancelled,
                "tool_execution_cancelled",
                "The tool call was cancelled with the hosted Agent turn.");
            if (controlIntent == CopilotAgentControlIntent.Cancel)
            {
                conversation.AgentSessionCheckpoint = null;
                assistantMessage.AgentStopReason = CopilotAgentStopReason.Cancelled;
            }
            else if (controlIntent == CopilotAgentControlIntent.Pause)
            {
                assistantMessage.AgentStopReason = CopilotAgentStopReason.Paused;
            }

            assistantMessage.MarkResponseInterrupted(BuildCancellationInterruptionDetail(
                assistantMessage,
                controlIntent));

            CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, controlIntent == CopilotAgentControlIntent.Pause
                ? "Agent 任务已暂停；可从最近一次可用 checkpoint 继续。"
                : assistantMessage.RequestMode == CopilotAgentMode.Chat
                    ? "当前回答已停止。"
                    : "Agent 任务已取消；本轮新 checkpoint 已丢弃。");
            assistantMessage.ClearReportedUsage();
            conversation.ClearLastUsage();
        }

        public static void CompleteFailure(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            string? errorMessage,
            params string?[] sensitiveValues)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            ArgumentNullException.ThrowIfNull(assistantMessage);

            CompleteThinking(assistantMessage);
            assistantMessage.CompleteActiveAgentTraces(
                CopilotToolExecutionState.Interrupted,
                CopilotToolFailureKind.Internal,
                "tool_terminal_event_missing",
                "The hosted Agent turn failed before this tool call emitted an authoritative terminal result.");
            var normalizedError = CopilotUserFacingErrorFormatter.Sanitize(errorMessage, sensitiveValues);
            var hadVisibleContent = !string.IsNullOrWhiteSpace(assistantMessage.Content);
            assistantMessage.MarkResponseInterrupted(hadVisibleContent
                ? $"回复生成过程中发生错误；已保留现有内容，但回答可能不完整。错误：{normalizedError}"
                : $"回复生成过程中发生错误；本轮没有生成完整回答。错误：{normalizedError}");
            if (!hadVisibleContent)
                CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, $"请求失败：{normalizedError}");
            assistantMessage.ClearReportedUsage();
            conversation.ClearLastUsage();
        }

        public static void CompleteBeforeStartCancellation(CopilotChatMessage assistantMessage)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);

            CompleteThinking(assistantMessage);
            assistantMessage.CompleteActiveAgentTraces(
                CopilotToolExecutionState.Cancelled,
                CopilotToolFailureKind.Cancelled,
                "tool_execution_cancelled",
                "The queued Agent turn was cancelled before the tool call completed.");
            if (assistantMessage.RequestMode != CopilotAgentMode.Chat)
                assistantMessage.AgentStopReason = CopilotAgentStopReason.Cancelled;
            assistantMessage.MarkResponseInterrupted(
                "请求在调用模型或工具前被取消；本轮没有生成回答，也没有执行任务。");
            CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, assistantMessage.RequestMode == CopilotAgentMode.Chat
                ? "请求已取消，尚未调用模型。"
                : "排队的 Agent 任务已取消，未调用模型或工具。");
        }

        private static string BuildCancellationInterruptionDetail(
            CopilotChatMessage assistantMessage,
            CopilotAgentControlIntent controlIntent)
        {
            if (controlIntent == CopilotAgentControlIntent.Pause)
            {
                return "Agent 任务已暂停；已保留现有内容和最近一次可用 checkpoint，但当前轮次未完成。";
            }
            if (assistantMessage.RequestMode == CopilotAgentMode.Chat)
                return "回答已停止；已保留现有内容，但回答可能不完整。";
            if (controlIntent == CopilotAgentControlIntent.Cancel)
                return "Agent 任务已取消；已保留现有内容，但当前轮次未完成。";
            return "Agent 轮次已中断；已保留现有内容，但当前轮次未完成。";
        }

        private static void SetUsage(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            CopilotTokenUsage usage)
        {
            if (usage.HasAny)
            {
                assistantMessage.SetReportedUsage(usage);
                conversation.SetLastUsage(usage);
            }
            else
            {
                assistantMessage.ClearReportedUsage();
                conversation.ClearLastUsage();
            }
        }

        private static void CompleteThinking(CopilotChatMessage assistantMessage)
        {
            assistantMessage.IsExecutionInProgress = false;
            assistantMessage.IsReasoningInProgress = false;
            assistantMessage.MarkThinkingCompleted();
        }
    }
}

using System;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotTurnEventState(
        CopilotAgentMode Mode,
        bool ChatRequestPrepared,
        bool AgentCompleted,
        CopilotTurnResult? Completion)
    {
        public static CopilotTurnEventState Create(CopilotAgentMode mode)
        {
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));

            return new CopilotTurnEventState(mode, false, false, null);
        }
    }

    internal static class CopilotTurnEventReducer
    {
        public static CopilotTurnEventState Reduce(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            ArgumentNullException.ThrowIfNull(turnEvent);
            if (state.Completion != null)
                throw new InvalidOperationException("Copilot turn emitted an event after completion.");

            return turnEvent switch
            {
                CopilotTurnRequestPreparedEvent => ReduceRequestPrepared(state, turnEvent),
                CopilotTurnChatDeltaEvent => ReduceChatProgress(state, turnEvent),
                CopilotTurnProviderRetryEvent providerRetry => ReduceProviderRetry(state, providerRetry),
                CopilotTurnAgentEvent agent => ReduceAgentEvent(state, agent),
                CopilotTurnCompletedEvent completed => ReduceCompletion(state, completed),
                _ => throw new InvalidOperationException(
                    $"Unsupported Copilot turn event: {turnEvent.GetType().Name}."),
            };
        }

        public static CopilotTurnResult RequireCompletion(CopilotTurnEventState state) =>
            state.Completion
            ?? throw new InvalidOperationException("Copilot turn ended without a completion event.");

        private static CopilotTurnEventState ReduceRequestPrepared(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            RequireChatMode(state, turnEvent);
            if (state.ChatRequestPrepared)
                throw new InvalidOperationException("Copilot chat turn prepared its request more than once.");

            return state with { ChatRequestPrepared = true };
        }

        private static CopilotTurnEventState ReduceChatProgress(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            RequirePreparedChatRequest(state, turnEvent);
            return state;
        }

        private static CopilotTurnEventState ReduceProviderRetry(
            CopilotTurnEventState state,
            CopilotTurnProviderRetryEvent providerRetry)
        {
            RequirePreparedChatRequest(state, providerRetry);
            if (providerRetry.Retry == null)
                throw new InvalidOperationException("Copilot provider retry event has no retry metadata.");

            return state;
        }

        private static CopilotTurnEventState ReduceAgentEvent(
            CopilotTurnEventState state,
            CopilotTurnAgentEvent agent)
        {
            RequireAgentMode(state, agent);
            if (state.AgentCompleted)
                throw new InvalidOperationException("Copilot Agent emitted an event after its completed item.");
            if (agent.Event == null)
                throw new InvalidOperationException("Copilot Agent event has no payload.");

            return agent.Event.Type == CopilotAgentEventType.Completed
                ? state with { AgentCompleted = true }
                : state;
        }

        private static CopilotTurnEventState ReduceCompletion(
            CopilotTurnEventState state,
            CopilotTurnCompletedEvent completed)
        {
            if (completed.Result == null)
                throw new InvalidOperationException("Copilot completion event has no result.");
            if (completed.Result.Mode != state.Mode)
            {
                throw new InvalidOperationException(
                    $"Copilot turn completed as {completed.Result.Mode}, but {state.Mode} was requested.");
            }
            if (state.Mode == CopilotAgentMode.Chat && !state.ChatRequestPrepared)
                throw new InvalidOperationException("Copilot chat turn completed before its request was prepared.");
            if (state.Mode != CopilotAgentMode.Chat && !state.AgentCompleted)
                throw new InvalidOperationException("Copilot Agent turn completed before its completed item was emitted.");

            return state with { Completion = completed.Result };
        }

        private static void RequirePreparedChatRequest(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            RequireChatMode(state, turnEvent);
            if (!state.ChatRequestPrepared)
            {
                throw new InvalidOperationException(
                    $"Copilot chat turn emitted {turnEvent.GetType().Name} before its request was prepared.");
            }
        }

        private static void RequireChatMode(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            if (state.Mode != CopilotAgentMode.Chat)
            {
                throw new InvalidOperationException(
                    $"Copilot {state.Mode} turn cannot emit {turnEvent.GetType().Name}.");
            }
        }

        private static void RequireAgentMode(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            if (state.Mode == CopilotAgentMode.Chat)
            {
                throw new InvalidOperationException(
                    $"Copilot chat turn cannot emit {turnEvent.GetType().Name}.");
            }
        }
    }
}

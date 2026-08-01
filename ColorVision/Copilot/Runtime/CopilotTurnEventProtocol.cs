using System;

namespace ColorVision.Copilot
{
    internal sealed class CopilotTurnEventProtocol
    {
        private readonly CopilotAgentMode _mode;
        private bool _chatRequestPrepared;
        private bool _agentCompleted;
        private CopilotTurnResult? _completion;

        public CopilotTurnEventProtocol(CopilotAgentMode mode)
        {
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            _mode = mode;
        }

        public void Observe(CopilotTurnEvent turnEvent)
        {
            ArgumentNullException.ThrowIfNull(turnEvent);
            if (_completion != null)
                throw new InvalidOperationException("Copilot turn emitted an event after completion.");

            switch (turnEvent)
            {
                case CopilotTurnRequestPreparedEvent:
                    RequireChatMode(turnEvent);
                    if (_chatRequestPrepared)
                        throw new InvalidOperationException("Copilot chat turn prepared its request more than once.");
                    _chatRequestPrepared = true;
                    break;
                case CopilotTurnChatDeltaEvent:
                    RequirePreparedChatRequest(turnEvent);
                    break;
                case CopilotTurnProviderRetryEvent providerRetry:
                    RequirePreparedChatRequest(turnEvent);
                    if (providerRetry.Retry == null)
                        throw new InvalidOperationException("Copilot provider retry event has no retry metadata.");
                    break;
                case CopilotTurnAgentEvent agent:
                    RequireAgentMode(turnEvent);
                    if (_agentCompleted)
                        throw new InvalidOperationException("Copilot Agent emitted an event after its completed item.");
                    if (agent.Event == null)
                        throw new InvalidOperationException("Copilot Agent event has no payload.");
                    if (agent.Event.Type == CopilotAgentEventType.Completed)
                        _agentCompleted = true;
                    break;
                case CopilotTurnCompletedEvent completed:
                    if (completed.Result == null)
                        throw new InvalidOperationException("Copilot completion event has no result.");
                    if (completed.Result.Mode != _mode)
                    {
                        throw new InvalidOperationException(
                            $"Copilot turn completed as {completed.Result.Mode}, but {_mode} was requested.");
                    }
                    if (_mode == CopilotAgentMode.Chat && !_chatRequestPrepared)
                        throw new InvalidOperationException("Copilot chat turn completed before its request was prepared.");
                    if (_mode != CopilotAgentMode.Chat && !_agentCompleted)
                        throw new InvalidOperationException("Copilot Agent turn completed before its completed item was emitted.");
                    _completion = completed.Result;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported Copilot turn event: {turnEvent.GetType().Name}.");
            }
        }

        public CopilotTurnResult RequireCompletion()
        {
            return _completion
                ?? throw new InvalidOperationException("Copilot turn ended without a completion event.");
        }

        private void RequirePreparedChatRequest(CopilotTurnEvent turnEvent)
        {
            RequireChatMode(turnEvent);
            if (!_chatRequestPrepared)
            {
                throw new InvalidOperationException(
                    $"Copilot chat turn emitted {turnEvent.GetType().Name} before its request was prepared.");
            }
        }

        private void RequireChatMode(CopilotTurnEvent turnEvent)
        {
            if (_mode != CopilotAgentMode.Chat)
            {
                throw new InvalidOperationException(
                    $"Copilot {_mode} turn cannot emit {turnEvent.GetType().Name}.");
            }
        }

        private void RequireAgentMode(CopilotTurnEvent turnEvent)
        {
            if (_mode == CopilotAgentMode.Chat)
            {
                throw new InvalidOperationException(
                    $"Copilot chat turn cannot emit {turnEvent.GetType().Name}.");
            }
        }
    }
}

namespace ColorVision.Copilot
{
    internal sealed class CopilotTurnEventProtocol
    {
        private CopilotTurnEventState _state;

        public CopilotTurnEventProtocol(
            CopilotAgentMode mode,
            string turnId = CopilotTurnStartedEvent.DefaultTurnId)
        {
            _state = CopilotTurnEventState.Create(mode, turnId);
        }

        public void Observe(CopilotTurnEvent turnEvent) =>
            _state = CopilotTurnEventReducer.Reduce(_state, turnEvent);

        public CopilotTurnStatus? TerminalStatus => _state.TerminalStatus;

        public CopilotTurnResult RequireCompletion() =>
            CopilotTurnEventReducer.RequireCompletion(_state);
    }
}

namespace ColorVision.Copilot
{
    internal sealed class CopilotTurnEventProtocol
    {
        private CopilotTurnEventState _state;

        public CopilotTurnEventProtocol(CopilotAgentMode mode)
        {
            _state = CopilotTurnEventState.Create(mode);
        }

        public void Observe(CopilotTurnEvent turnEvent) =>
            _state = CopilotTurnEventReducer.Reduce(_state, turnEvent);

        public CopilotTurnResult RequireCompletion() =>
            CopilotTurnEventReducer.RequireCompletion(_state);
    }
}

namespace ColorVision.Copilot
{
    internal enum CopilotHostedRunPrimaryAction
    {
        None,
        Stop,
        Cancel,
    }

    internal readonly record struct CopilotHostedRunInteraction(
        bool AcceptsNewInput,
        CopilotHostedRunPrimaryAction PrimaryAction);

    internal static class CopilotHostedRunInteractionPolicy
    {
        internal static CopilotHostedRunInteraction Evaluate(CopilotHostedRunState state) => state switch
        {
            CopilotHostedRunState.Queued => new(false, CopilotHostedRunPrimaryAction.Cancel),
            CopilotHostedRunState.Running => new(true, CopilotHostedRunPrimaryAction.Stop),
            CopilotHostedRunState.PauseRequested => new(false, CopilotHostedRunPrimaryAction.Cancel),
            CopilotHostedRunState.CancelRequested => new(false, CopilotHostedRunPrimaryAction.None),
            _ => new(false, CopilotHostedRunPrimaryAction.None),
        };
    }
}

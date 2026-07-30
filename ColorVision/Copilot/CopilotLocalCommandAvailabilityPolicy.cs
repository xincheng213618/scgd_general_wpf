namespace ColorVision.Copilot
{
    public enum CopilotLocalCommandComposerContext
    {
        Idle,
        ActiveRun,
        AwaitingUserAnswer,
        QueuedRun,
    }

    public static class CopilotLocalCommandAvailabilityPolicy
    {
        public static bool CanShowSuggestions(CopilotLocalCommandComposerContext context)
        {
            return context is CopilotLocalCommandComposerContext.Idle
                or CopilotLocalCommandComposerContext.ActiveRun;
        }

        public static bool CanSuggest(
            CopilotLocalCommand command,
            CopilotLocalCommandComposerContext context)
        {
            return CanShowSuggestions(context)
                && (context != CopilotLocalCommandComposerContext.ActiveRun
                    || command.AvailableWhileAgentRuns);
        }

        public static bool CanExecute(
            CopilotLocalCommand command,
            CopilotLocalCommandComposerContext context)
        {
            return context == CopilotLocalCommandComposerContext.Idle
                || context == CopilotLocalCommandComposerContext.ActiveRun
                    && command.AvailableWhileAgentRuns;
        }
    }
}

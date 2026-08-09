using System;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotTurnProviderRetryLifecycleState(
        CopilotProviderRetryInfo? Latest)
    {
        public static CopilotTurnProviderRetryLifecycleState Empty => new(null);

        public CopilotTurnProviderRetryLifecycleState Observe(CopilotProviderRetryInfo retry)
        {
            CopilotProviderRetryProtocol.Validate(retry);
            if (Latest != null
                && (retry.FailedAttempt != Latest.NextAttempt
                    || retry.MaximumAttempts != Latest.MaximumAttempts))
            {
                throw new InvalidOperationException(
                    "Copilot chat provider retry attempts did not advance in sequence.");
            }

            return new CopilotTurnProviderRetryLifecycleState(retry);
        }
    }
}

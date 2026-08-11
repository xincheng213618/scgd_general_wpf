using System;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotTurnProviderConnectionRecoveryLifecycleState(
        CopilotProviderConnectionRecoveryInfo? Latest)
    {
        public static CopilotTurnProviderConnectionRecoveryLifecycleState Empty => new(null);

        public CopilotTurnProviderConnectionRecoveryLifecycleState Observe(
            CopilotProviderConnectionRecoveryInfo recovery)
        {
            CopilotProviderConnectionRecoveryProtocol.Validate(recovery);
            if (Latest != null)
            {
                var expectedAttempt = Latest.RecoveryAttempt == int.MaxValue
                    ? int.MaxValue
                    : Latest.RecoveryAttempt + 1;
                if (recovery.RecoveryAttempt != expectedAttempt)
                {
                    throw new InvalidOperationException(
                        "Copilot chat provider connection-recovery attempts did not advance in sequence.");
                }
            }

            return new CopilotTurnProviderConnectionRecoveryLifecycleState(recovery);
        }
    }
}

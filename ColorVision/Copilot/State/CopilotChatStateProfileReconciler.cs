using System;

namespace ColorVision.Copilot
{
    public static class CopilotChatStateProfileReconciler
    {
        public static CopilotProfileConfig? Apply(CopilotChatState state, CopilotConfig config, string? requestedProfileId)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(config);

            var profile = config.FindProfile(requestedProfileId)
                ?? config.FindProfile(state.ActiveProfileId)
                ?? config.GetPreferredDefaultProfile();
            if (profile != null)
                state.ActiveProfileId = profile.Id;

            state.EnsureInitialized(config);
            return config.FindProfile(state.ActiveProfileId) ?? profile;
        }
    }
}

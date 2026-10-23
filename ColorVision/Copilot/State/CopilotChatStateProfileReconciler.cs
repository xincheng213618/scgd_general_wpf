using System;

namespace ColorVision.Copilot
{
    public static class CopilotChatStateProfileReconciler
    {
        public static CopilotProfileConfig? Apply(CopilotChatState state, CopilotConfig config, string? requestedProfileId)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(config);

            CopilotProfileConfig? FindAvailable(string? id) => config.FindProfile(id) is { IsAvailable: true } candidate ? candidate : null;
            var profile = FindAvailable(requestedProfileId)
                ?? FindAvailable(state.ActiveProfileId)
                ?? config.GetPreferredDefaultProfile();
            state.ActiveProfileId = profile?.Id ?? string.Empty;

            state.EnsureInitialized(config);
            return FindAvailable(state.ActiveProfileId) ?? profile;
        }
    }
}

using System;

namespace ColorVision.Copilot
{
    public enum CopilotResponsePersonality
    {
        None,
        Friendly,
        Pragmatic,
    }

    internal static class CopilotResponsePersonalitySelection
    {
        public static bool TryParse(string? value, out CopilotResponsePersonality personality)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "none":
                    personality = CopilotResponsePersonality.None;
                    return true;
                case "friendly":
                    personality = CopilotResponsePersonality.Friendly;
                    return true;
                case "pragmatic":
                    personality = CopilotResponsePersonality.Pragmatic;
                    return true;
                default:
                    personality = CopilotResponsePersonality.None;
                    return false;
            }
        }

        public static string GetCommandToken(CopilotResponsePersonality personality)
        {
            return Normalize(personality) switch
            {
                CopilotResponsePersonality.Friendly => "friendly",
                CopilotResponsePersonality.Pragmatic => "pragmatic",
                _ => "none",
            };
        }

        public static string GetDisplayName(CopilotResponsePersonality personality)
        {
            return Normalize(personality) switch
            {
                CopilotResponsePersonality.Friendly => "友好",
                CopilotResponsePersonality.Pragmatic => "务实",
                _ => "无",
            };
        }

        public static CopilotResponsePersonality Normalize(CopilotResponsePersonality personality)
        {
            return Enum.IsDefined(personality) ? personality : CopilotResponsePersonality.None;
        }
    }
}

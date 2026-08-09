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
        internal sealed record Resolution(
            CopilotResponsePersonality Personality,
            string SourceLabel);

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

        public static Resolution Resolve(
            CopilotConversationRecord? conversation,
            CopilotProjectInstructionDiscoveryOptions? codexConfigOptions)
        {
            if (codexConfigOptions?.ConfiguredPersonalityEnabled == false)
            {
                string sourceLabel = codexConfigOptions.PersonalityEnabledSourceLabel.Length == 0
                    ? "Codex config.toml features.personality"
                    : codexConfigOptions.PersonalityEnabledSourceLabel;
                return new Resolution(
                    CopilotResponsePersonality.None,
                    sourceLabel + "（关闭；不注入 personality）");
            }

            if (conversation != null
                && (conversation.HasResponsePersonalityOverride
                    || conversation.ResponsePersonality != CopilotResponsePersonality.None))
            {
                return new Resolution(
                    Normalize(conversation.ResponsePersonality),
                    "会话覆盖");
            }

            if (codexConfigOptions?.HasPersonalityOverride == true)
            {
                return new Resolution(
                    Normalize(codexConfigOptions.ConfiguredPersonality),
                    codexConfigOptions.PersonalitySourceLabel.Length == 0
                        ? "Codex config.toml personality"
                        : codexConfigOptions.PersonalitySourceLabel);
            }

            return new Resolution(
                CopilotResponsePersonality.Pragmatic,
                "Codex features.personality 稳定功能默认值");
        }
    }
}

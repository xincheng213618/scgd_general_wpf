using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotPromptSuggestionProfileSelection
    {
        internal const string CurrentProfileId = "@current";

        public static CopilotProfileConfig? Resolve(
            IEnumerable<CopilotProfileConfig>? profiles,
            CopilotProfileConfig? currentProfile,
            string? storedProfileId)
        {
            var normalized = (storedProfileId ?? string.Empty).Trim();
            if (string.Equals(normalized, CurrentProfileId, StringComparison.Ordinal))
            {
                return currentProfile?.IsConfigured == true
                    ? currentProfile
                    : null;
            }

            if (normalized.Length == 0)
                return null;

            return profiles?.FirstOrDefault(profile =>
                profile?.IsConfigured == true
                && string.Equals(profile.Id, normalized, StringComparison.Ordinal));
        }

        public static string Describe(
            IEnumerable<CopilotProfileConfig>? profiles,
            CopilotProfileConfig? currentProfile,
            string? storedProfileId)
        {
            var normalized = (storedProfileId ?? string.Empty).Trim();
            if (normalized.Length == 0)
                return "未选择（受控暂停）";

            if (string.Equals(normalized, CurrentProfileId, StringComparison.Ordinal))
            {
                return currentProfile?.IsConfigured == true
                    ? $"当前 Profile：{FormatProfile(currentProfile)}"
                    : "当前 Profile 不可用（受控暂停）";
            }

            var profile = profiles?.FirstOrDefault(item =>
                item != null
                && string.Equals(item.Id, normalized, StringComparison.Ordinal));
            if (profile == null)
                return "固定 Profile 已不存在（受控暂停）";
            return profile.IsConfigured
                ? $"固定 Profile：{FormatProfile(profile)}"
                : $"固定 Profile：{profile.DisplayLabel}（配置不完整，受控暂停）";
        }

        public static string FormatUsage(
            CopilotProfileConfig profile,
            CopilotTokenUsage usage)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var label = FormatProfile(profile);
            return usage.HasAny
                ? $"{label} · 输入 {CopilotTokenUsage.FormatCount(usage.InputTokens)} / 输出 {CopilotTokenUsage.FormatCount(usage.OutputTokens)}"
                : label;
        }

        public static string FormatStoredSelection(string? storedProfileId)
        {
            var normalized = (storedProfileId ?? string.Empty).Trim();
            if (normalized.Length == 0)
                return "未选择";
            return string.Equals(normalized, CurrentProfileId, StringComparison.Ordinal)
                ? "当前 Profile"
                : "固定 Profile";
        }

        private static string FormatProfile(CopilotProfileConfig profile)
        {
            return string.Equals(profile.DisplayLabel, profile.Model, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(profile.Model)
                    ? profile.DisplayLabel
                    : $"{profile.DisplayLabel} · {profile.Model}";
        }
    }
}

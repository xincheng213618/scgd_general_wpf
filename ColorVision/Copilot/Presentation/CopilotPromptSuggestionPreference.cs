using System;

namespace ColorVision.Copilot
{
    internal static class CopilotPromptSuggestionPreference
    {
        internal const string Usage =
            "用法：/suggestions [on|off]。省略参数时切换当前本地历史补全状态。";

        internal static bool TryResolve(
            string? arguments,
            bool currentlyEnabled,
            out bool enabled)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                enabled = !currentlyEnabled;
                return true;
            }

            if (string.Equals(normalized, "on", StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                return true;
            }

            if (string.Equals(normalized, "off", StringComparison.OrdinalIgnoreCase))
            {
                enabled = false;
                return true;
            }

            enabled = currentlyEnabled;
            return false;
        }
    }
}

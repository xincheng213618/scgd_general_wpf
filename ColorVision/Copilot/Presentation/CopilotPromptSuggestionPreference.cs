using System;

namespace ColorVision.Copilot
{
    internal static class CopilotPromptSuggestionPreference
    {
        internal const string Usage =
            "用法：/suggestions [on|off]。管理当前设备上的本地历史补全，不调用模型。";

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

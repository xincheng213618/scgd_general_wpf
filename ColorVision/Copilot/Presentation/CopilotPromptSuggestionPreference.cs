using System;

namespace ColorVision.Copilot
{
    internal static class CopilotPromptSuggestionPreference
    {
        internal const string Usage =
            "用法：/suggestions [on|off|predict-on|predict-off]。on/off 管理本地历史补全；predict-on/predict-off 管理轮次结束后的工具禁用模型预测。";

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

        internal static bool TryResolvePredicted(
            string? arguments,
            bool currentlyEnabled,
            out bool enabled)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (string.Equals(normalized, "predict-on", StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                return true;
            }

            if (string.Equals(normalized, "predict-off", StringComparison.OrdinalIgnoreCase))
            {
                enabled = false;
                return true;
            }

            enabled = currentlyEnabled;
            return false;
        }
    }
}

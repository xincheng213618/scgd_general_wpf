using System;

namespace ColorVision.Copilot
{
    internal static class CopilotMessageTimestampPreference
    {
        internal const string Usage =
            "用法：/timestamps [on|off]。省略参数时切换当前显示状态。";

        internal static bool TryResolve(
            string? arguments,
            bool currentlyVisible,
            out bool visible)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                visible = !currentlyVisible;
                return true;
            }

            if (string.Equals(normalized, "on", StringComparison.OrdinalIgnoreCase))
            {
                visible = true;
                return true;
            }

            if (string.Equals(normalized, "off", StringComparison.OrdinalIgnoreCase))
            {
                visible = false;
                return true;
            }

            visible = currentlyVisible;
            return false;
        }
    }
}

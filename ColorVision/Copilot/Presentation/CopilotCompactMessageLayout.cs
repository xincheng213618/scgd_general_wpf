using System;
using System.Windows;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotCompactMessageLayoutMetrics(
        Thickness MessageListPadding,
        Thickness MessageItemMargin,
        Thickness UserMessagePadding,
        Thickness AssistantActionsMargin);

    internal static class CopilotCompactMessageLayout
    {
        internal const string Usage =
            "用法：/compact-mode [on|off]。省略参数时切换当前显示密度。";

        private static readonly CopilotCompactMessageLayoutMetrics Standard = new(
            new Thickness(16, 12, 16, 12),
            new Thickness(0, 0, 0, 12),
            new Thickness(10, 5, 10, 5),
            new Thickness(0, 10, 0, 0));

        private static readonly CopilotCompactMessageLayoutMetrics Compact = new(
            new Thickness(12, 7, 12, 7),
            new Thickness(0, 0, 0, 6),
            new Thickness(8, 3, 8, 3),
            new Thickness(0, 5, 0, 0));

        internal static CopilotCompactMessageLayoutMetrics Resolve(bool useCompactLayout) =>
            useCompactLayout ? Compact : Standard;

        internal static bool TryResolvePreference(
            string? arguments,
            bool currentlyCompact,
            out bool useCompactLayout)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                useCompactLayout = !currentlyCompact;
                return true;
            }

            if (string.Equals(normalized, "on", StringComparison.OrdinalIgnoreCase))
            {
                useCompactLayout = true;
                return true;
            }

            if (string.Equals(normalized, "off", StringComparison.OrdinalIgnoreCase))
            {
                useCompactLayout = false;
                return true;
            }

            useCompactLayout = currentlyCompact;
            return false;
        }
    }
}

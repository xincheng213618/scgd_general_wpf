using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    internal static class CopilotUsageCommand
    {
        public const string Usage = "/usage [session|daily|weekly|cumulative]";

        public static string Format(
            CopilotConversationRecord? currentConversation,
            IEnumerable<CopilotConversationRecord>? conversations,
            DateTimeOffset now,
            string? arguments,
            CopilotProviderRateLimitSnapshot? providerRateLimits)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0
                || string.Equals(normalized, "session", StringComparison.OrdinalIgnoreCase))
            {
                return CopilotConversationUsageDiagnostics.Format(
                    currentConversation,
                    providerRateLimits);
            }

            if (!TryResolveStatisticsWindow(
                normalized,
                out var window,
                out var detailMode,
                out var viewName))
            {
                return "/usage 参数无效。可用 /usage、/usage session、/usage daily、/usage weekly 或 /usage cumulative。";
            }

            var snapshot = CopilotConversationStatistics.Capture(
                conversations,
                now,
                window);
            return CopilotConversationStatistics.Format(
                snapshot,
                $"/usage {viewName}",
                detailMode);
        }

        private static bool TryResolveStatisticsWindow(
            string arguments,
            out CopilotConversationStatisticsWindow window,
            out CopilotConversationStatisticsDetailMode detailMode,
            out string viewName)
        {
            if (string.Equals(arguments, "daily", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments, "7", StringComparison.OrdinalIgnoreCase))
            {
                window = CopilotConversationStatisticsWindow.SevenDays;
                detailMode = CopilotConversationStatisticsDetailMode.Daily;
                viewName = "daily";
                return true;
            }
            if (string.Equals(arguments, "weekly", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments, "30", StringComparison.OrdinalIgnoreCase))
            {
                window = CopilotConversationStatisticsWindow.ThirtyDays;
                detailMode = CopilotConversationStatisticsDetailMode.Weekly;
                viewName = "weekly";
                return true;
            }
            if (string.Equals(arguments, "cumulative", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments, "all", StringComparison.OrdinalIgnoreCase))
            {
                window = CopilotConversationStatisticsWindow.All;
                detailMode = CopilotConversationStatisticsDetailMode.Cumulative;
                viewName = "cumulative";
                return true;
            }

            window = default;
            detailMode = default;
            viewName = string.Empty;
            return false;
        }
    }
}

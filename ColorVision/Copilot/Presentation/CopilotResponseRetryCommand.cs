using System;

namespace ColorVision.Copilot
{
    internal static class CopilotResponseRetryCommand
    {
        internal static bool TryParse(
            string? arguments,
            out bool refreshExternalContext)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            refreshExternalContext = string.Equals(
                normalized,
                "refresh",
                StringComparison.OrdinalIgnoreCase);
            return normalized.Length == 0 || refreshExternalContext;
        }
    }
}

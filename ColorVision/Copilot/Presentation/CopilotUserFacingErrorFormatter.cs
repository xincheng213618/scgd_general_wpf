using ColorVision.Copilot.Mcp;
using System;
using System.Text;

namespace ColorVision.Copilot
{
    public static class CopilotUserFacingErrorFormatter
    {
        public const int MaximumMessageLength = 600;

        private const int MaximumSourceLength = 16_000;

        public static string Sanitize(string? message, params string?[] sensitiveValues)
        {
            var text = message ?? string.Empty;
            foreach (var sensitiveValue in sensitiveValues ?? Array.Empty<string?>())
            {
                var secret = sensitiveValue?.Trim();
                if (!string.IsNullOrEmpty(secret) && secret.Length >= 4)
                    text = text.Replace(secret, "<redacted>", StringComparison.Ordinal);
            }

            if (text.Length > MaximumSourceLength)
                text = text[..MaximumSourceLength];

            text = CopilotMcpAuditLogger.RedactText(text);
            var builder = new StringBuilder(text.Length);
            var pendingSpace = false;
            foreach (var character in text)
            {
                if (char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                    builder.Append(' ');
                builder.Append(character);
                pendingSpace = false;
            }

            var normalized = builder.ToString().Trim();
            if (normalized.Length == 0)
                normalized = "Unknown error.";

            return normalized.Length <= MaximumMessageLength
                ? normalized
                : normalized[..(MaximumMessageLength - 3)] + "...";
        }
    }
}

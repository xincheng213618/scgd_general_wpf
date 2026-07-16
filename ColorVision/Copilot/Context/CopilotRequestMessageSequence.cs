using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    internal static class CopilotRequestMessageSequence
    {
        private const string MergeSeparator = "\n\n";

        public static CopilotRequestMessage[] Normalize(IEnumerable<CopilotRequestMessage>? messages)
        {
            var normalized = new List<CopilotRequestMessage>();
            foreach (var message in messages ?? Array.Empty<CopilotRequestMessage>())
            {
                var role = message.Role?.Trim().ToLowerInvariant() switch
                {
                    "user" => "user",
                    "assistant" => "assistant",
                    _ => string.Empty,
                };
                var content = (message.Content ?? string.Empty).Trim();
                if (role.Length == 0 || content.Length == 0)
                    continue;

                if (normalized.Count > 0 && string.Equals(normalized[^1].Role, role, StringComparison.Ordinal))
                {
                    normalized[^1] = new CopilotRequestMessage(
                        role,
                        normalized[^1].Content.TrimEnd() + MergeSeparator + content);
                }
                else
                {
                    normalized.Add(new CopilotRequestMessage(role, content));
                }
            }

            return normalized.ToArray();
        }
    }
}

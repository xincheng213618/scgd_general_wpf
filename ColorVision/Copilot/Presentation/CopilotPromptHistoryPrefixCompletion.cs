using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotPromptHistoryPrefixCompletion(
        string FullText,
        string Suffix);

    internal static class CopilotPromptHistoryPrefixCompletionResolver
    {
        public static bool TryResolve(
            IEnumerable<CopilotChatMessage>? messages,
            string? input,
            out CopilotPromptHistoryPrefixCompletion completion)
        {
            completion = default;
            var prefix = input ?? string.Empty;
            if (prefix.Length == 0
                || char.IsWhiteSpace(prefix[0])
                || prefix[0] is '/' or '$' or '@')
            {
                return false;
            }

            var match = (messages ?? [])
                .Reverse()
                .Where(message => message?.IsUser == true)
                .Select(message => message.Content ?? string.Empty)
                .FirstOrDefault(content =>
                    content.Length > prefix.Length
                    && content.StartsWith(prefix, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(match))
                return false;

            completion = new CopilotPromptHistoryPrefixCompletion(
                match,
                match[prefix.Length..]);
            return true;
        }
    }
}

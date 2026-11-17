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

            var match = FindLatestMatch(messages, prefix);
            if (match == null)
                return false;

            completion = new CopilotPromptHistoryPrefixCompletion(
                match,
                match[prefix.Length..]);
            return true;
        }

        private static string? FindLatestMatch(IEnumerable<CopilotChatMessage>? messages, string prefix)
        {
            if (messages is IReadOnlyList<CopilotChatMessage> indexed)
            {
                for (int index = indexed.Count - 1; index >= 0; index--)
                {
                    if (GetMatch(indexed[index], prefix) is { } match)
                        return match;
                }

                return null;
            }

            foreach (var message in (messages ?? []).Reverse())
            {
                if (GetMatch(message, prefix) is { } match)
                    return match;
            }

            return null;
        }

        private static string? GetMatch(CopilotChatMessage? message, string prefix)
        {
            if (message is { IsUser: true, Content: { } content }
                && content.Length > prefix.Length
                && content.StartsWith(prefix, StringComparison.Ordinal))
            {
                return content;
            }

            return null;
        }
    }
}

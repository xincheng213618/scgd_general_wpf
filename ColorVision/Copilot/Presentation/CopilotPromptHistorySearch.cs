using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed record CopilotPromptHistorySearchItem(
        string Text,
        string Preview);

    internal static class CopilotPromptHistorySearch
    {
        public const int MaximumResults = 12;
        public const int MaximumPreviewCharacters = 140;
        public const int MaximumQueryCharacters = 256;

        public static IReadOnlyList<CopilotPromptHistorySearchItem> Search(
            IEnumerable<CopilotChatMessage>? messages,
            string? query)
        {
            var normalizedQuery = Normalize(query, MaximumQueryCharacters);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var candidates = (messages ?? Array.Empty<CopilotChatMessage>())
                .Select((message, index) => (message, index))
                .Where(item => item.message?.IsUser == true
                    && !string.IsNullOrWhiteSpace(item.message.Content))
                .Reverse()
                .Select(item =>
                {
                    var text = item.message.Content.Trim();
                    var searchable = Normalize(text, int.MaxValue);
                    return new
                    {
                        Text = text,
                        Searchable = searchable,
                        Score = Score(searchable, normalizedQuery),
                        item.index,
                    };
                })
                .Where(item => seen.Add(item.Text) && item.Score >= 0)
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.index)
                .Take(MaximumResults)
                .Select(item => new CopilotPromptHistorySearchItem(
                    item.Text,
                    BuildPreview(item.Searchable)))
                .ToArray();
            return candidates;
        }

        private static int Score(string candidate, string query)
        {
            if (query.Length == 0)
                return 0;
            if (string.Equals(candidate, query, StringComparison.OrdinalIgnoreCase))
                return 100_000;
            if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 90_000 - Math.Min(candidate.Length - query.Length, 10_000);

            var exactIndex = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (exactIndex >= 0)
                return 80_000 - Math.Min(exactIndex, 10_000);

            var terms = query.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var termPositions = terms
                .Select(term => candidate.IndexOf(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (termPositions.Length > 1 && termPositions.All(position => position >= 0))
                return 60_000 - Math.Min(termPositions.Sum(), 10_000);

            return TryScoreSubsequence(candidate, query, out var subsequenceScore)
                ? subsequenceScore
                : -1;
        }

        private static bool TryScoreSubsequence(
            string candidate,
            string query,
            out int score)
        {
            score = -1;
            var queryIndex = 0;
            var firstMatch = -1;
            var lastMatch = -1;
            for (var index = 0; index < candidate.Length && queryIndex < query.Length; index++)
            {
                if (char.ToUpperInvariant(candidate[index]) != char.ToUpperInvariant(query[queryIndex]))
                    continue;

                if (firstMatch < 0)
                    firstMatch = index;
                lastMatch = index;
                queryIndex++;
            }

            if (queryIndex != query.Length)
                return false;

            var span = lastMatch - firstMatch + 1;
            score = 40_000
                - Math.Min(firstMatch, 10_000)
                - Math.Min(span - query.Length, 10_000);
            return true;
        }

        private static string BuildPreview(string normalized)
        {
            if (normalized.Length <= MaximumPreviewCharacters)
                return normalized;

            var retainedLength = MaximumPreviewCharacters;
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength].TrimEnd() + "…";
        }

        private static string Normalize(string? value, int maximumCharacters)
        {
            var normalized = string.Join(
                " ",
                (value ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (normalized.Length <= maximumCharacters)
                return normalized;

            var retainedLength = maximumCharacters;
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength];
        }
    }
}

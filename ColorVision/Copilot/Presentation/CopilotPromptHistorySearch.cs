using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColorVision.Copilot
{
    internal enum CopilotPromptHistorySearchScope
    {
        CurrentConversation,
        AllConversations,
    }

    public sealed record CopilotPromptHistorySearchItem(
        string Text,
        string Preview,
        string SourceSummary = "")
    {
        public bool HasSourceSummary => !string.IsNullOrWhiteSpace(SourceSummary);
    }

    internal static class CopilotPromptHistorySearch
    {
        public const int MaximumResults = 12;
        public const int MaximumPreviewCharacters = 140;
        public const int MaximumQueryCharacters = 256;
        public const int MaximumSourceTitleCharacters = 80;

        public static IReadOnlyList<CopilotPromptHistorySearchItem> Search(
            IEnumerable<CopilotChatMessage>? messages,
            string? query)
        {
            var entries = (messages ?? Array.Empty<CopilotChatMessage>())
                .Where(message => message?.IsUser == true && !string.IsNullOrWhiteSpace(message.Content))
                .Select(message => new SearchEntry(message, ConversationTitle: null));
            return SearchEntries(entries, query);
        }

        public static IReadOnlyList<CopilotPromptHistorySearchItem> SearchAll(
            IEnumerable<CopilotConversationRecord>? conversations,
            string? query)
        {
            var entries = (conversations ?? Array.Empty<CopilotConversationRecord>())
                .Where(conversation => conversation != null)
                .SelectMany(conversation => (conversation.Messages ?? [])
                    .Where(message => message?.IsUser == true && !string.IsNullOrWhiteSpace(message.Content))
                    .Select(message => new SearchEntry(message, conversation.Title ?? string.Empty)))
                .OrderBy(entry => entry.Message.CreatedAt);
            return SearchEntries(entries, query);
        }

        private static CopilotPromptHistorySearchItem[] SearchEntries(
            IEnumerable<SearchEntry> entries,
            string? query)
        {
            var normalizedQuery = Normalize(query, MaximumQueryCharacters);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var newestEntries = entries
                .Select((entry, index) => (Entry: entry, Index: index))
                .Reverse()
                .Select(item => (item.Entry, item.Index, Text: item.Entry.Message.Content.Trim()))
                .Where(item => seen.Add(item.Text));
            if (normalizedQuery.Length == 0)
            {
                return newestEntries
                    .Take(MaximumResults)
                    .Select(item => CreateResult(item.Text, Normalize(item.Text, int.MaxValue), item.Entry))
                    .ToArray();
            }

            var terms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var foldedQuery = string.Create(normalizedQuery.Length, normalizedQuery, static (destination, text) =>
            {
                for (int i = 0; i < destination.Length; i++)
                    destination[i] = char.ToUpperInvariant(text[i]);
            });

            return newestEntries
                .Select(item =>
                {
                    var searchable = Normalize(item.Text, int.MaxValue);
                    return new
                    {
                        item.Text,
                        Searchable = searchable,
                        Score = Score(searchable, normalizedQuery, terms, foldedQuery),
                        item.Entry,
                        item.Index,
                    };
                })
                .Where(item => item.Score >= 0)
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Index)
                .Take(MaximumResults)
                .Select(item => CreateResult(item.Text, item.Searchable, item.Entry))
                .ToArray();
        }

        private static CopilotPromptHistorySearchItem CreateResult(string text, string searchable, SearchEntry entry) =>
            new(text, BuildPreview(searchable), entry.ConversationTitle == null
                ? string.Empty
                : BuildSourceSummary(entry.ConversationTitle, entry.Message.CreatedAt));

        private static string BuildSourceSummary(string? conversationTitle, DateTime createdAt)
        {
            var title = Normalize(conversationTitle, MaximumSourceTitleCharacters);
            if (title.Length == 0)
                title = CopilotUiText.NewConversationTitle;

            var time = createdAt == default
                ? string.Empty
                : createdAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            return time.Length == 0 ? title : $"{title} · {time}";
        }

        private static int Score(string candidate, string query, string[] terms, string foldedQuery)
        {
            int index = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (index == 0)
                return candidate.Length == query.Length ? 100_000 : 90_000 - Math.Min(candidate.Length - query.Length, 10_000);
            if (index > 0)
                return 80_000 - Math.Min(index, 10_000);

            if (terms.Length > 1)
            {
                long sum = 0;
                foreach (string term in terms)
                {
                    int position = candidate.IndexOf(term, StringComparison.OrdinalIgnoreCase);
                    if (position < 0)
                    {
                        sum = -1;
                        break;
                    }

                    sum += position;
                }

                if (sum >= 0)
                    return 60_000 - Math.Min(checked((int)sum), 10_000);
            }

            return TryScoreSubsequence(candidate, foldedQuery, out var subsequenceScore)
                ? subsequenceScore
                : -1;
        }

        private static bool TryScoreSubsequence(
            string candidate,
            string foldedQuery,
            out int score)
        {
            score = -1;
            var queryIndex = 0;
            var firstMatch = -1;
            var lastMatch = -1;
            for (var index = 0; index < candidate.Length && queryIndex < foldedQuery.Length; index++)
            {
                if (char.ToUpperInvariant(candidate[index]) != foldedQuery[queryIndex])
                    continue;

                if (firstMatch < 0)
                    firstMatch = index;
                lastMatch = index;
                queryIndex++;
            }

            if (queryIndex != foldedQuery.Length)
                return false;

            var span = lastMatch - firstMatch + 1;
            score = 40_000
                - Math.Min(firstMatch, 10_000)
                - Math.Min(span - foldedQuery.Length, 10_000);
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
            var input = value.AsSpan().Trim();
            if (input.IsEmpty)
                return string.Empty;

            int length = 0;
            bool pendingSpace = false;
            bool collapseWhitespace = false;
            foreach (char character in input)
            {
                if (char.IsWhiteSpace(character))
                {
                    collapseWhitespace |= pendingSpace || character != ' ';
                    pendingSpace = true;
                    continue;
                }

                length += pendingSpace ? 2 : 1;
                pendingSpace = false;
            }

            string normalized = collapseWhitespace
                ? string.Create(length, value!, static (destination, source) =>
                {
                    int index = 0;
                    foreach (char character in source.AsSpan().Trim())
                    {
                        if (!char.IsWhiteSpace(character))
                            destination[index++] = character;
                        else if (destination[index - 1] != ' ')
                            destination[index++] = ' ';
                    }
                })
                : input.Length == value!.Length ? value : input.ToString();
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

        private sealed record SearchEntry(
            CopilotChatMessage Message,
            string? ConversationTitle);
    }
}

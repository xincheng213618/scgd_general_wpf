using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed class CopilotConversationFindNavigator
    {
        public const int MaximumQueryCharacters = 256;

        private CopilotChatMessage[] _matches = [];
        private int _selectedIndex = -1;

        public IReadOnlyList<CopilotChatMessage> Matches => _matches;

        public int SelectedIndex => _selectedIndex;

        public CopilotChatMessage? Current =>
            _selectedIndex >= 0 && _selectedIndex < _matches.Length
                ? _matches[_selectedIndex]
                : null;

        public void Refresh(
            IEnumerable<CopilotChatMessage>? messages,
            string? query)
        {
            var previous = Current;
            var normalized = NormalizeQuery(query);
            _matches = normalized.Length == 0
                ? []
                : (messages ?? Array.Empty<CopilotChatMessage>())
                    .Where(message => message != null && MatchesMessage(message, normalized))
                    .ToArray();
            if (_matches.Length == 0)
            {
                _selectedIndex = -1;
                return;
            }

            var previousIndex = previous == null
                ? -1
                : Array.IndexOf(_matches, previous);
            _selectedIndex = previousIndex >= 0 ? previousIndex : 0;
        }

        public bool Move(bool previous)
        {
            if (_matches.Length == 0)
                return false;

            _selectedIndex = previous
                ? (_selectedIndex - 1 + _matches.Length) % _matches.Length
                : (_selectedIndex + 1) % _matches.Length;
            return true;
        }

        public static string NormalizeQuery(string? query)
        {
            var normalized = (query ?? string.Empty).Trim();
            if (normalized.Length <= MaximumQueryCharacters)
                return normalized;

            var retainedLength = MaximumQueryCharacters;
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength];
        }

        private static bool MatchesMessage(
            CopilotChatMessage message,
            string query)
        {
            return Contains(message.Content, query)
                || Contains(message.ReasoningContent, query)
                || Contains(message.ExecutionContent, query)
                || Contains(message.AssistantName, query)
                || message.Attachments.Any(attachment =>
                    Contains(attachment?.Title, query)
                    || Contains(attachment?.DisplayLabel, query)
                    || Contains(attachment?.Source, query))
                || message.AgentTraceEntries.Any(trace =>
                    Contains(trace?.ToolName, query)
                    || Contains(trace?.ArgumentSummary, query)
                    || Contains(trace?.ResultSummary, query)
                    || Contains(trace?.ErrorMessage, query));
        }

        private static bool Contains(string? text, string query)
        {
            return !string.IsNullOrWhiteSpace(text)
                && text.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}

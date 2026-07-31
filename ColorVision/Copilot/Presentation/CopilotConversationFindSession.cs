using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    internal sealed class CopilotConversationFindSession
    {
        private readonly CopilotConversationFindNavigator _navigator = new();

        public bool IsOpen { get; private set; }

        public string Query { get; private set; } = string.Empty;

        public bool HasQuery => Query.Length > 0;

        public bool HasMatches => IsOpen && _navigator.Matches.Count > 0;

        public string StatusText
        {
            get
            {
                if (!HasQuery)
                    return "输入关键词";
                if (_navigator.Matches.Count == 0)
                    return "0 项";

                return $"{_navigator.SelectedIndex + 1} / {_navigator.Matches.Count}";
            }
        }

        public CopilotChatMessage? Current => IsOpen ? _navigator.Current : null;

        public bool Open(
            IEnumerable<CopilotChatMessage>? messages,
            string? query)
        {
            var wasOpen = IsOpen;
            IsOpen = true;
            Query = CopilotConversationFindNavigator.NormalizeQuery(query);
            RefreshCore(messages);
            return !wasOpen;
        }

        public bool SetQuery(
            IEnumerable<CopilotChatMessage>? messages,
            string? query)
        {
            var normalized = CopilotConversationFindNavigator.NormalizeQuery(query);
            if (string.Equals(Query, normalized, StringComparison.Ordinal))
                return false;

            Query = normalized;
            if (IsOpen)
                RefreshCore(messages);
            return true;
        }

        public bool Refresh(IEnumerable<CopilotChatMessage>? messages)
        {
            if (!IsOpen)
                return false;

            RefreshCore(messages);
            return true;
        }

        public bool Move(
            IEnumerable<CopilotChatMessage>? messages,
            bool previous)
        {
            if (!IsOpen || !_navigator.Move(previous))
                return false;

            ApplyHighlights(messages);
            return true;
        }

        public bool Close(IEnumerable<CopilotChatMessage>? messages)
        {
            if (!IsOpen)
                return false;

            ClearHighlights(messages);
            _navigator.Refresh([], string.Empty);
            IsOpen = false;
            return true;
        }

        public static void ClearHighlights(IEnumerable<CopilotChatMessage>? messages)
        {
            foreach (var message in messages ?? Array.Empty<CopilotChatMessage>())
                message?.SetConversationFindState(isMatch: false, isCurrent: false);
        }

        private void RefreshCore(IEnumerable<CopilotChatMessage>? messages)
        {
            _navigator.Refresh(messages, Query);
            ApplyHighlights(messages);
        }

        private void ApplyHighlights(IEnumerable<CopilotChatMessage>? messages)
        {
            var matches = new HashSet<CopilotChatMessage>(_navigator.Matches);
            var current = _navigator.Current;
            foreach (var message in messages ?? Array.Empty<CopilotChatMessage>())
            {
                message?.SetConversationFindState(
                    matches.Contains(message),
                    ReferenceEquals(message, current));
            }
        }
    }
}

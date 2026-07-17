using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed class CopilotPromptHistoryNavigator
    {
        private string _conversationId = string.Empty;
        private string _draft = string.Empty;
        private string[] _entries = Array.Empty<string>();
        private int _index;

        public bool IsActive => _entries.Length > 0;

        public bool TryNavigate(
            CopilotConversationRecord? conversation,
            string currentText,
            bool previous,
            out string text)
        {
            text = currentText ?? string.Empty;
            if (conversation == null)
                return false;

            if (_entries.Length == 0 || !string.Equals(_conversationId, conversation.Id, StringComparison.Ordinal))
            {
                if (!previous || !Initialize(conversation, text))
                    return false;
            }

            if (previous)
            {
                if (_index > 0)
                    _index--;

                text = _entries[_index];
                return true;
            }

            if (_index >= _entries.Length)
                return false;

            _index++;
            if (_index < _entries.Length)
            {
                text = _entries[_index];
                return true;
            }

            text = _draft;
            Reset();
            return true;
        }

        public bool TryCancel(out string draft)
        {
            draft = _draft;
            if (!IsActive)
                return false;

            Reset();
            return true;
        }

        public void Reset()
        {
            _conversationId = string.Empty;
            _draft = string.Empty;
            _entries = Array.Empty<string>();
            _index = 0;
        }

        private bool Initialize(CopilotConversationRecord conversation, string draft)
        {
            var entries = new List<string>();
            foreach (var message in conversation.Messages.Where(message => message.IsUser && !string.IsNullOrWhiteSpace(message.Content)))
            {
                var content = message.Content.Trim();
                if (entries.Count == 0 || !string.Equals(entries[^1], content, StringComparison.Ordinal))
                    entries.Add(content);
            }
            if (entries.Count == 0)
                return false;

            _conversationId = conversation.Id;
            _draft = draft;
            _entries = entries.ToArray();
            _index = _entries.Length;
            return true;
        }
    }
}

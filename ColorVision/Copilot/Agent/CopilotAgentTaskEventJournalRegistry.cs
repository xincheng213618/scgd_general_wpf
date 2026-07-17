using System;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentTaskEventJournalContext
    {
        public string ConversationId { get; init; } = string.Empty;

        public CopilotAgentTaskEventJournalSnapshot Journal { get; init; } = new();

        public DateTimeOffset PublishedAtUtc { get; init; }

        public bool IsStructurallyValid()
        {
            return !string.IsNullOrWhiteSpace(ConversationId)
                && ConversationId.Length <= 160
                && PublishedAtUtc != default
                && Journal?.Events?.Count > 0
                && Journal.IsStructurallyValid();
        }
    }

    public static class CopilotAgentTaskEventJournalRegistry
    {
        private static readonly object SyncRoot = new();
        private static CopilotAgentTaskEventJournalContext? _current;

        public static CopilotAgentTaskEventJournalContext? Current
        {
            get
            {
                lock (SyncRoot)
                    return _current;
            }
        }

        public static bool Publish(string conversationId, CopilotAgentTaskEventJournalSnapshot journal)
        {
            var context = new CopilotAgentTaskEventJournalContext
            {
                ConversationId = (conversationId ?? string.Empty).Trim(),
                Journal = journal ?? new CopilotAgentTaskEventJournalSnapshot(),
                PublishedAtUtc = DateTimeOffset.UtcNow,
            };
            if (!context.IsStructurallyValid())
                return false;

            lock (SyncRoot)
                _current = context;
            return true;
        }

        public static void Clear()
        {
            lock (SyncRoot)
                _current = null;
        }
    }
}

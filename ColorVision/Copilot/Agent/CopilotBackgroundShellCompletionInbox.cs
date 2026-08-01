using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Copilot
{
    internal sealed record CopilotDeferredBackgroundShellCompletion(
        CopilotBackgroundShellCommandSnapshot Snapshot,
        DateTimeOffset CapturedAtUtc);

    internal sealed class CopilotBackgroundShellCompletionDeliveryLease :
        IDisposable
    {
        private CopilotBackgroundShellCompletionInbox? _owner;
        private readonly string _conversationId;

        internal CopilotBackgroundShellCompletionDeliveryLease(
            CopilotBackgroundShellCompletionInbox? owner,
            string conversationId,
            IReadOnlyList<CopilotDeferredBackgroundShellCompletion>
                completions)
        {
            _owner = owner;
            _conversationId = conversationId;
            Completions = completions;
        }

        public IReadOnlyList<CopilotDeferredBackgroundShellCompletion>
            Completions
        {
            get;
        }

        public void Commit()
        {
            Interlocked.Exchange(ref _owner, null);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)
                ?.ReturnDelivery(_conversationId, Completions);
        }
    }

    internal sealed class CopilotBackgroundShellCompletionInbox
    {
        public const int MaximumPendingCompletionsPerConversation =
            CopilotBackgroundShellCommandRegistry.MaximumActivePerConversation;

        public const int MaximumPendingConversations = 16;

        public static readonly TimeSpan Retention = TimeSpan.FromHours(1);

        private readonly object _syncRoot = new();
        private readonly Dictionary<string, ConversationInbox> _conversations =
            new(StringComparer.Ordinal);
        private readonly Func<DateTimeOffset> _utcNow;

        public CopilotBackgroundShellCompletionInbox()
            : this(() => DateTimeOffset.UtcNow)
        {
        }

        internal CopilotBackgroundShellCompletionInbox(
            Func<DateTimeOffset> utcNow)
        {
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        internal int PendingCompletionCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _conversations.Values.Sum(item =>
                        item.Completions.Count);
                }
            }
        }

        public bool TryEnqueue(
            CopilotBackgroundShellCommandSnapshot? snapshot)
        {
            var conversationId = (snapshot?.ConversationId ?? string.Empty)
                .Trim();
            if (conversationId.Length == 0
                || !CopilotBackgroundShellCommandAgentEvent.TryCreateMessage(
                    snapshot,
                    conversationId,
                    out _))
            {
                return false;
            }

            var now = _utcNow();
            lock (_syncRoot)
            {
                PruneExpired(now);
                var conversation = GetOrCreateConversation(
                    conversationId,
                    now);
                var existingIndex = conversation.Completions.FindIndex(item =>
                    string.Equals(
                        item.Snapshot.Id,
                        snapshot!.Id,
                        StringComparison.Ordinal));
                if (existingIndex >= 0)
                    conversation.Completions.RemoveAt(existingIndex);
                else if (conversation.Completions.Count
                    >= MaximumPendingCompletionsPerConversation)
                    conversation.Completions.RemoveAt(0);

                conversation.Completions.Add(
                    new CopilotDeferredBackgroundShellCompletion(
                        snapshot!,
                        now));
                conversation.LastUpdatedAtUtc = now;
                return true;
            }
        }

        public CopilotBackgroundShellCompletionDeliveryLease BeginDelivery(
            string? conversationId)
        {
            var normalizedConversationId =
                (conversationId ?? string.Empty).Trim();
            if (normalizedConversationId.Length == 0)
                return EmptyDelivery(string.Empty);

            lock (_syncRoot)
            {
                PruneExpired(_utcNow());
                if (!_conversations.Remove(
                    normalizedConversationId,
                    out var conversation))
                {
                    return EmptyDelivery(normalizedConversationId);
                }

                return new CopilotBackgroundShellCompletionDeliveryLease(
                    this,
                    normalizedConversationId,
                    conversation.Completions.ToArray());
            }
        }

        internal void ReturnDelivery(
            string conversationId,
            IReadOnlyList<CopilotDeferredBackgroundShellCompletion>
                completions)
        {
            if (completions.Count == 0)
                return;

            var now = _utcNow();
            var oldestRetainedAtUtc = now - Retention;
            var returnedCompletions = completions
                .Where(item => item.CapturedAtUtc >= oldestRetainedAtUtc)
                .ToArray();
            if (returnedCompletions.Length == 0)
                return;

            lock (_syncRoot)
            {
                PruneExpired(now);
                var conversation = GetOrCreateConversation(
                    conversationId,
                    now);
                var olderUnmergedCompletions = returnedCompletions
                    .Where(returned => !conversation.Completions.Any(current =>
                        string.Equals(
                            current.Snapshot.Id,
                            returned.Snapshot.Id,
                            StringComparison.Ordinal)))
                    .ToArray();
                if (olderUnmergedCompletions.Length > 0)
                {
                    conversation.Completions.InsertRange(
                        0,
                        olderUnmergedCompletions);
                }
                while (conversation.Completions.Count
                    > MaximumPendingCompletionsPerConversation)
                {
                    conversation.Completions.RemoveAt(0);
                }

                conversation.LastUpdatedAtUtc = now;
            }
        }

        private ConversationInbox GetOrCreateConversation(
            string conversationId,
            DateTimeOffset now)
        {
            if (_conversations.TryGetValue(
                conversationId,
                out var conversation))
            {
                return conversation;
            }

            if (_conversations.Count >= MaximumPendingConversations)
            {
                var oldestConversation = _conversations
                    .OrderBy(item => item.Value.LastUpdatedAtUtc)
                    .First();
                _conversations.Remove(oldestConversation.Key);
            }
            conversation = new ConversationInbox(now);
            _conversations.Add(conversationId, conversation);
            return conversation;
        }

        private static CopilotBackgroundShellCompletionDeliveryLease
            EmptyDelivery(string conversationId)
        {
            return new CopilotBackgroundShellCompletionDeliveryLease(
                owner: null,
                conversationId,
                Array.Empty<CopilotDeferredBackgroundShellCompletion>());
        }

        private void PruneExpired(DateTimeOffset now)
        {
            var oldestRetainedAtUtc = now - Retention;
            foreach (var conversation in _conversations.ToArray())
            {
                conversation.Value.Completions.RemoveAll(item =>
                    item.CapturedAtUtc < oldestRetainedAtUtc);
                if (conversation.Value.Completions.Count == 0)
                    _conversations.Remove(conversation.Key);
            }
        }

        private sealed class ConversationInbox(DateTimeOffset createdAtUtc)
        {
            public List<CopilotDeferredBackgroundShellCompletion> Completions
            {
                get;
            } = new();

            public DateTimeOffset LastUpdatedAtUtc { get; set; } =
                createdAtUtc;
        }
    }
}

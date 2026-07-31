using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed record CopilotDeferredBackgroundShellOutputEvent(
        CopilotBackgroundShellOutputMonitorEventArgs EventArgs,
        DateTimeOffset FirstCapturedAtUtc,
        DateTimeOffset LastCapturedAtUtc,
        int EventBatches,
        int DroppedEventBatches);

    internal sealed class CopilotBackgroundShellOutputEventInbox
    {
        public const int MaximumPendingEventsPerConversation = 8;

        public const int MaximumPendingConversations = 16;

        public static readonly TimeSpan Retention = TimeSpan.FromHours(1);

        private readonly object _syncRoot = new();
        private readonly Dictionary<string, ConversationInbox> _conversations =
            new(StringComparer.Ordinal);
        private readonly Func<DateTimeOffset> _utcNow;

        public CopilotBackgroundShellOutputEventInbox()
            : this(() => DateTimeOffset.UtcNow)
        {
        }

        internal CopilotBackgroundShellOutputEventInbox(
            Func<DateTimeOffset> utcNow)
        {
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        internal int PendingEventCount
        {
            get
            {
                lock (_syncRoot)
                    return _conversations.Values.Sum(item => item.Events.Count);
            }
        }

        public bool TryEnqueue(
            CopilotBackgroundShellOutputMonitorEventArgs? eventArgs)
        {
            var conversationId =
                (eventArgs?.Monitor.ConversationId ?? string.Empty).Trim();
            if (conversationId.Length == 0
                || !CopilotBackgroundShellCommandAgentEvent
                    .TryCreateOutputMessage(
                        eventArgs,
                        conversationId,
                        out _))
            {
                return false;
            }

            var now = _utcNow();
            lock (_syncRoot)
            {
                PruneExpired(now);
                if (!_conversations.TryGetValue(
                        conversationId,
                        out var conversation))
                {
                    if (_conversations.Count
                        >= MaximumPendingConversations)
                    {
                        var oldestConversation = _conversations
                            .OrderBy(item => item.Value.LastUpdatedAtUtc)
                            .First();
                        _conversations.Remove(oldestConversation.Key);
                    }

                    conversation = new ConversationInbox(now);
                    _conversations.Add(conversationId, conversation);
                }

                var monitorId = eventArgs!.Monitor.Id;
                var existingIndex = conversation.Events.FindIndex(item =>
                    string.Equals(
                        item.EventArgs.Monitor.Id,
                        monitorId,
                        StringComparison.Ordinal));
                if (existingIndex >= 0)
                {
                    var existing = conversation.Events[existingIndex];
                    conversation.Events.RemoveAt(existingIndex);
                    conversation.Events.Add(
                        existing with
                        {
                            EventArgs = MergeEventArgs(
                                existing.EventArgs,
                                eventArgs),
                            LastCapturedAtUtc = now,
                            EventBatches = existing.EventBatches + 1,
                        });
                }
                else
                {
                    if (conversation.Events.Count
                        >= MaximumPendingEventsPerConversation)
                    {
                        conversation.Events.RemoveAt(0);
                        conversation.DroppedEventBatches++;
                    }

                    conversation.Events.Add(
                        new CopilotDeferredBackgroundShellOutputEvent(
                            eventArgs,
                            now,
                            now,
                            EventBatches: 1,
                            DroppedEventBatches: 0));
                }

                conversation.LastUpdatedAtUtc = now;
                return true;
            }
        }

        public IReadOnlyList<CopilotDeferredBackgroundShellOutputEvent> Drain(
            string? conversationId)
        {
            var normalizedConversationId =
                (conversationId ?? string.Empty).Trim();
            if (normalizedConversationId.Length == 0)
                return Array.Empty<CopilotDeferredBackgroundShellOutputEvent>();

            lock (_syncRoot)
            {
                PruneExpired(_utcNow());
                if (!_conversations.Remove(
                        normalizedConversationId,
                        out var conversation))
                {
                    return Array.Empty<CopilotDeferredBackgroundShellOutputEvent>();
                }

                var events = conversation.Events.ToArray();
                if (events.Length > 0
                    && conversation.DroppedEventBatches > 0)
                {
                    events[0] = events[0] with
                    {
                        DroppedEventBatches =
                            conversation.DroppedEventBatches,
                    };
                }
                return events;
            }
        }

        private static CopilotBackgroundShellOutputMonitorEventArgs
            MergeEventArgs(
                CopilotBackgroundShellOutputMonitorEventArgs older,
                CopilotBackgroundShellOutputMonitorEventArgs newer)
        {
            var suppressedEvents = (long)older.SuppressedEvents
                + newer.SuppressedEvents;
            return new CopilotBackgroundShellOutputMonitorEventArgs(
                newer.Monitor,
                newer.Content,
                suppressedEvents > int.MaxValue
                    ? int.MaxValue
                    : (int)suppressedEvents);
        }

        private void PruneExpired(DateTimeOffset now)
        {
            var oldestRetainedAtUtc = now - Retention;
            foreach (var conversation in _conversations.ToArray())
            {
                conversation.Value.Events.RemoveAll(item =>
                    item.LastCapturedAtUtc < oldestRetainedAtUtc);
                if (conversation.Value.Events.Count == 0)
                    _conversations.Remove(conversation.Key);
            }
        }

        private sealed class ConversationInbox(DateTimeOffset createdAtUtc)
        {
            public List<CopilotDeferredBackgroundShellOutputEvent> Events
            {
                get;
            } = new();

            public DateTimeOffset LastUpdatedAtUtc { get; set; } =
                createdAtUtc;

            public int DroppedEventBatches { get; set; }
        }
    }
}

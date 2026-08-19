using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Copilot
{
    internal sealed record CopilotDeferredBackgroundShellOutputEvent(
        CopilotBackgroundShellOutputMonitorEventArgs EventArgs,
        string DeliveryId,
        DateTimeOffset FirstCapturedAtUtc,
        DateTimeOffset LastCapturedAtUtc,
        int EventBatches,
        int DroppedEventBatches);

    internal sealed class CopilotBackgroundShellOutputDeliveryLease :
        IDisposable
    {
        private CopilotBackgroundShellOutputEventInbox? _owner;
        private readonly string _conversationId;

        internal CopilotBackgroundShellOutputDeliveryLease(
            CopilotBackgroundShellOutputEventInbox? owner,
            string conversationId,
            IReadOnlyList<CopilotDeferredBackgroundShellOutputEvent> events)
        {
            _owner = owner;
            _conversationId = conversationId;
            Events = Array.AsReadOnly((events ?? throw new ArgumentNullException(nameof(events))).ToArray());
        }

        public IReadOnlyList<CopilotDeferredBackgroundShellOutputEvent>
            Events
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
                ?.ReturnDelivery(_conversationId, Events);
        }
    }

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
                            EventBatches = SaturatingAdd(
                                existing.EventBatches,
                                1),
                        });
                }
                else
                {
                    if (conversation.Events.Count
                        >= MaximumPendingEventsPerConversation)
                    {
                        conversation.DroppedEventBatches =
                            SaturatingAdd(
                                conversation.DroppedEventBatches,
                                conversation.Events[0].EventBatches);
                        conversation.Events.RemoveAt(0);
                    }

                    conversation.Events.Add(
                        new CopilotDeferredBackgroundShellOutputEvent(
                            eventArgs,
                            CreateDeliveryId(),
                            now,
                            now,
                            EventBatches: 1,
                            DroppedEventBatches: 0));
                }

                conversation.LastUpdatedAtUtc = now;
                return true;
            }
        }

        public CopilotBackgroundShellOutputDeliveryLease BeginDelivery(
            string? conversationId)
        {
            var normalizedConversationId =
                (conversationId ?? string.Empty).Trim();
            if (normalizedConversationId.Length == 0)
            {
                return new CopilotBackgroundShellOutputDeliveryLease(
                    owner: null,
                    string.Empty,
                    Array.Empty<CopilotDeferredBackgroundShellOutputEvent>());
            }

            lock (_syncRoot)
            {
                PruneExpired(_utcNow());
                if (!_conversations.Remove(
                        normalizedConversationId,
                        out var conversation))
                {
                    return new CopilotBackgroundShellOutputDeliveryLease(
                        owner: null,
                        normalizedConversationId,
                        Array.Empty<CopilotDeferredBackgroundShellOutputEvent>());
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
                return new CopilotBackgroundShellOutputDeliveryLease(
                    this,
                    normalizedConversationId,
                    events);
            }
        }

        internal void ReturnDelivery(
            string conversationId,
            IReadOnlyList<CopilotDeferredBackgroundShellOutputEvent> events)
        {
            if (events.Count == 0)
                return;

            var now = _utcNow();
            var oldestRetainedAtUtc = now - Retention;
            var returnedEvents = events
                .Where(item =>
                    item.LastCapturedAtUtc >= oldestRetainedAtUtc)
                .ToArray();
            if (returnedEvents.Length == 0)
                return;

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
                            .OrderBy(item =>
                                item.Value.LastUpdatedAtUtc)
                            .First();
                        _conversations.Remove(oldestConversation.Key);
                    }
                    conversation = new ConversationInbox(now);
                    _conversations.Add(conversationId, conversation);
                }

                var droppedEventBatches = returnedEvents.Aggregate(
                    conversation.DroppedEventBatches,
                    (total, item) => SaturatingAdd(
                        total,
                        item.DroppedEventBatches));
                var returnedWithoutDropCounts = returnedEvents
                    .Select(item => item with
                    {
                        DroppedEventBatches = 0,
                    })
                    .ToArray();
                var olderUnmergedEvents =
                    new List<CopilotDeferredBackgroundShellOutputEvent>();
                foreach (var returnedEvent in returnedWithoutDropCounts)
                {
                    var newerIndex = conversation.Events.FindIndex(item =>
                        string.Equals(
                            item.EventArgs.Monitor.Id,
                            returnedEvent.EventArgs.Monitor.Id,
                            StringComparison.Ordinal));
                    if (newerIndex < 0)
                    {
                        olderUnmergedEvents.Add(returnedEvent);
                        continue;
                    }

                    conversation.Events[newerIndex] = MergeDeferredEvents(
                        returnedEvent,
                        conversation.Events[newerIndex]);
                }

                if (olderUnmergedEvents.Count > 0)
                {
                    conversation.Events.InsertRange(
                        0,
                        olderUnmergedEvents);
                }
                while (conversation.Events.Count
                    > MaximumPendingEventsPerConversation)
                {
                    droppedEventBatches = SaturatingAdd(
                        droppedEventBatches,
                        conversation.Events[0].EventBatches);
                    conversation.Events.RemoveAt(0);
                }

                conversation.DroppedEventBatches = droppedEventBatches;
                conversation.LastUpdatedAtUtc = now;
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

        private static CopilotDeferredBackgroundShellOutputEvent
            MergeDeferredEvents(
                CopilotDeferredBackgroundShellOutputEvent older,
                CopilotDeferredBackgroundShellOutputEvent newer)
        {
            return newer with
            {
                EventArgs = MergeEventArgs(
                    older.EventArgs,
                    newer.EventArgs),
                FirstCapturedAtUtc =
                    older.FirstCapturedAtUtc
                        < newer.FirstCapturedAtUtc
                        ? older.FirstCapturedAtUtc
                        : newer.FirstCapturedAtUtc,
                LastCapturedAtUtc =
                    older.LastCapturedAtUtc
                        > newer.LastCapturedAtUtc
                        ? older.LastCapturedAtUtc
                        : newer.LastCapturedAtUtc,
                EventBatches = SaturatingAdd(
                    older.EventBatches,
                    newer.EventBatches),
                DroppedEventBatches = 0,
            };
        }

        private static string CreateDeliveryId()
        {
            return $"background-output:{Guid.NewGuid():N}";
        }

        private static int SaturatingAdd(int left, int right)
        {
            var sum = (long)Math.Max(0, left) + Math.Max(0, right);
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
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

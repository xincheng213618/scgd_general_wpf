using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotBackgroundShellOutputEventInboxTests
{
    [Fact]
    public void CoalescesEachMonitorToItsNewestBoundedEvent()
    {
        var now = DateTimeOffset.Parse("2026-07-31T01:00:00Z");
        var inbox = new CopilotBackgroundShellOutputEventInbox(() => now);

        Assert.True(inbox.TryEnqueue(CreateEvent(
            "conversation",
            "monitor:one",
            "old output",
            suppressedEvents: 2)));
        now = now.AddSeconds(5);
        Assert.True(inbox.TryEnqueue(CreateEvent(
            "conversation",
            "monitor:one",
            "new output",
            suppressedEvents: 3)));

        using var delivery = inbox.BeginDelivery("conversation");
        var deferredEvent = Assert.Single(delivery.Events);
        delivery.Commit();
        Assert.Equal("new output", deferredEvent.EventArgs.Content);
        Assert.Equal(5, deferredEvent.EventArgs.SuppressedEvents);
        Assert.Equal(2, deferredEvent.EventBatches);
        Assert.Equal(
            DateTimeOffset.Parse("2026-07-31T01:00:00Z"),
            deferredEvent.FirstCapturedAtUtc);
        Assert.Equal(now, deferredEvent.LastCapturedAtUtc);
        Assert.Equal(0, deferredEvent.DroppedEventBatches);
        Assert.Equal(0, inbox.PendingEventCount);
    }

    [Fact]
    public void DrainsOnlyTheExactConversationAndOnlyOnce()
    {
        var inbox = new CopilotBackgroundShellOutputEventInbox();
        Assert.True(inbox.TryEnqueue(CreateEvent(
            "conversation:one",
            "monitor:one",
            "one")));
        Assert.True(inbox.TryEnqueue(CreateEvent(
            "conversation:two",
            "monitor:two",
            "two")));

        using var firstDelivery = inbox.BeginDelivery("conversation:one");
        var firstConversation = Assert.Single(firstDelivery.Events);
        firstDelivery.Commit();

        Assert.Equal(
            "conversation:one",
            firstConversation.EventArgs.Monitor.ConversationId);
        using var emptyDelivery = inbox.BeginDelivery("conversation:one");
        Assert.Empty(emptyDelivery.Events);
        using var secondDelivery = inbox.BeginDelivery("conversation:two");
        Assert.Single(secondDelivery.Events);
        secondDelivery.Commit();
    }

    [Fact]
    public void ConcurrentEventsForOneMonitorCoalesceWithoutLoss()
    {
        var inbox = new CopilotBackgroundShellOutputEventInbox();

        Parallel.For(
            0,
            100,
            index => inbox.TryEnqueue(CreateEvent(
                "conversation",
                "monitor:one",
                $"output {index}")));

        using var delivery = inbox.BeginDelivery("conversation");
        var deferredEvent = Assert.Single(delivery.Events);
        delivery.Commit();
        Assert.Equal(100, deferredEvent.EventBatches);
        Assert.StartsWith(
            "output ",
            deferredEvent.EventArgs.Content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DropsOldestMonitorWhenConversationCapacityIsReached()
    {
        var inbox = new CopilotBackgroundShellOutputEventInbox();
        for (var index = 0;
             index <= CopilotBackgroundShellOutputEventInbox
                 .MaximumPendingEventsPerConversation;
             index++)
        {
            Assert.True(inbox.TryEnqueue(CreateEvent(
                "conversation",
                $"monitor:{index}",
                $"output {index}")));
        }

        using var delivery = inbox.BeginDelivery("conversation");
        var deferredEvents = delivery.Events;
        delivery.Commit();

        Assert.Equal(
            CopilotBackgroundShellOutputEventInbox
                .MaximumPendingEventsPerConversation,
            deferredEvents.Count);
        Assert.DoesNotContain(
            deferredEvents,
            item => item.EventArgs.Monitor.Id == "monitor:0");
        Assert.Equal(1, deferredEvents[0].DroppedEventBatches);
    }

    [Fact]
    public void ExpiresUndeliveredOutputInsteadOfReplayingStaleContent()
    {
        var now = DateTimeOffset.Parse("2026-07-31T01:00:00Z");
        var inbox = new CopilotBackgroundShellOutputEventInbox(() => now);
        Assert.True(inbox.TryEnqueue(CreateEvent(
            "conversation",
            "monitor:one",
            "stale output")));

        now += CopilotBackgroundShellOutputEventInbox.Retention
            + TimeSpan.FromTicks(1);

        using var delivery = inbox.BeginDelivery("conversation");
        Assert.Empty(delivery.Events);
        Assert.Equal(0, inbox.PendingEventCount);
    }

    [Fact]
    public void UncommittedDeliveryReturnsTheSameStableDeliveryId()
    {
        var inbox = new CopilotBackgroundShellOutputEventInbox();
        Assert.True(inbox.TryEnqueue(CreateEvent(
            "conversation",
            "monitor:one",
            "retry output")));

        string deliveryId;
        using (var firstDelivery = inbox.BeginDelivery("conversation"))
        {
            deliveryId = Assert.Single(firstDelivery.Events).DeliveryId;
        }

        using var retriedDelivery = inbox.BeginDelivery("conversation");
        Assert.Equal(
            deliveryId,
            Assert.Single(retriedDelivery.Events).DeliveryId);
        retriedDelivery.Commit();
    }

    [Fact]
    public void ReturnedDeliveryMergesWithoutOverwritingNewerOutput()
    {
        var now = DateTimeOffset.Parse("2026-07-31T01:00:00Z");
        var inbox = new CopilotBackgroundShellOutputEventInbox(() => now);
        Assert.True(inbox.TryEnqueue(CreateEvent(
            "conversation",
            "monitor:one",
            "old output",
            suppressedEvents: 2)));
        var olderDelivery = inbox.BeginDelivery("conversation");
        var olderDeliveryId =
            Assert.Single(olderDelivery.Events).DeliveryId;

        now = now.AddSeconds(5);
        Assert.True(inbox.TryEnqueue(CreateEvent(
            "conversation",
            "monitor:one",
            "new output",
            suppressedEvents: 3)));
        olderDelivery.Dispose();

        using var mergedDelivery = inbox.BeginDelivery("conversation");
        var mergedEvent = Assert.Single(mergedDelivery.Events);
        mergedDelivery.Commit();
        Assert.Equal("new output", mergedEvent.EventArgs.Content);
        Assert.Equal(5, mergedEvent.EventArgs.SuppressedEvents);
        Assert.Equal(2, mergedEvent.EventBatches);
        Assert.NotEqual(olderDeliveryId, mergedEvent.DeliveryId);
        Assert.Equal(
            DateTimeOffset.Parse("2026-07-31T01:00:00Z"),
            mergedEvent.FirstCapturedAtUtc);
        Assert.Equal(now, mergedEvent.LastCapturedAtUtc);
    }

    [Fact]
    public void RejectsInactiveOrEmptyOutputEvents()
    {
        var inbox = new CopilotBackgroundShellOutputEventInbox();

        Assert.False(inbox.TryEnqueue(CreateEvent(
            "conversation",
            "monitor:inactive",
            "output",
            CopilotBackgroundShellOutputMonitorState.Completed)));
        Assert.False(inbox.TryEnqueue(CreateEvent(
            "conversation",
            "monitor:empty",
            "")));
        Assert.Equal(0, inbox.PendingEventCount);
    }

    private static CopilotBackgroundShellOutputMonitorEventArgs CreateEvent(
        string conversationId,
        string monitorId,
        string content,
        CopilotBackgroundShellOutputMonitorState state =
            CopilotBackgroundShellOutputMonitorState.Running,
        int suppressedEvents = 0)
    {
        var monitor = new CopilotBackgroundShellOutputMonitorSnapshot(
            monitorId,
            conversationId,
            $"background:{monitorId}",
            CopilotBackgroundShellOutputStream.StandardOutput,
            "test monitor",
            DateTimeOffset.Parse("2026-07-31T00:00:00Z"),
            DateTimeOffset.Parse("2026-07-31T02:00:00Z"),
            state,
            PublishedEvents: 1,
            SuppressedEvents: suppressedEvents);
        return new CopilotBackgroundShellOutputMonitorEventArgs(
            monitor,
            content,
            suppressedEvents);
    }
}

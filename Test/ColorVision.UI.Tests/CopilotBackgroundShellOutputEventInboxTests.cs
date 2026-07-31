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

        var deferredEvent = Assert.Single(inbox.Drain("conversation"));
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

        var firstConversation = Assert.Single(
            inbox.Drain("conversation:one"));

        Assert.Equal(
            "conversation:one",
            firstConversation.EventArgs.Monitor.ConversationId);
        Assert.Empty(inbox.Drain("conversation:one"));
        Assert.Single(inbox.Drain("conversation:two"));
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

        var deferredEvent = Assert.Single(inbox.Drain("conversation"));
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

        var deferredEvents = inbox.Drain("conversation");

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

        Assert.Empty(inbox.Drain("conversation"));
        Assert.Equal(0, inbox.PendingEventCount);
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

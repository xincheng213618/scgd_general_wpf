using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAgentTaskEventJournalRegistryTests
{
    [Fact]
    public void PublishDetachesTheRegisteredJournalFromMutableCallerCollections()
    {
        var occurredAtUtc = new DateTimeOffset(2026, 8, 19, 9, 30, 0, TimeSpan.Zero);
        var runId = "run:0123456789abcdef0123456789abcdef";
        var relatedIds = new List<string> { "task:original" };
        var events = new List<CopilotAgentTaskEvent>
        {
            new()
            {
                Sequence = 1,
                Id = CopilotAgentTaskEventIds.CreateEventId(
                    1,
                    runId,
                    CopilotAgentTaskEventType.RunStarted,
                    occurredAtUtc),
                Type = CopilotAgentTaskEventType.RunStarted,
                OccurredAtUtc = occurredAtUtc,
                RunId = runId,
                SubjectId = runId,
                RelatedIds = relatedIds,
                State = "started",
                Summary = "Run started.",
            },
        };
        var source = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = events,
        };

        try
        {
            Assert.True(CopilotAgentTaskEventJournalRegistry.Publish(
                "conversation-1",
                source));

            relatedIds[0] = "task:changed";
            events.Clear();

            var published = Assert.IsType<CopilotAgentTaskEventJournalContext>(
                CopilotAgentTaskEventJournalRegistry.Current);
            var publishedEvent = Assert.Single(published.Journal.Events);
            Assert.Equal("task:original", Assert.Single(publishedEvent.RelatedIds));
            Assert.True(published.Journal.IsDetachedSnapshot);
        }
        finally
        {
            CopilotAgentTaskEventJournalRegistry.Clear();
        }
    }
}

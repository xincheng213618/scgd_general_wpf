using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotBackgroundShellCompletionInboxTests
{
    [Fact]
    public void UncommittedDeliveryReturnsCompletionAndCommitRemovesIt()
    {
        var now = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
        var inbox = new CopilotBackgroundShellCompletionInbox(() => now);
        Assert.True(inbox.TryEnqueue(CreateCompletion("background:one")));

        using (var firstDelivery = inbox.BeginDelivery("conversation"))
        {
            Assert.Equal(
                "background:one",
                Assert.Single(firstDelivery.Completions).Snapshot.Id);
        }

        Assert.Equal(1, inbox.PendingCompletionCount);
        using (var retriedDelivery = inbox.BeginDelivery("conversation"))
        {
            Assert.Equal(
                "background:one",
                Assert.Single(retriedDelivery.Completions).Snapshot.Id);
            retriedDelivery.Commit();
        }

        Assert.Equal(0, inbox.PendingCompletionCount);
        Assert.Empty(inbox.BeginDelivery("conversation").Completions);
    }

    [Fact]
    public void DuplicateCompletionKeepsLatestTerminalSnapshot()
    {
        var now = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
        var inbox = new CopilotBackgroundShellCompletionInbox(() => now);
        Assert.True(inbox.TryEnqueue(CreateCompletion(
            "background:one",
            CopilotBackgroundShellCommandState.Failed,
            exitCode: 1)));
        now = now.AddMinutes(1);
        Assert.True(inbox.TryEnqueue(CreateCompletion(
            "background:one",
            CopilotBackgroundShellCommandState.Completed,
            exitCode: 0)));

        using var delivery = inbox.BeginDelivery("conversation");
        var completion = Assert.Single(delivery.Completions);
        Assert.Equal(CopilotBackgroundShellCommandState.Completed,
            completion.Snapshot.State);
        Assert.Equal(0, completion.Snapshot.ExitCode);
        Assert.Equal(now, completion.CapturedAtUtc);
    }

    [Fact]
    public void InboxBoundsCommandsAndConversationsByDroppingOldest()
    {
        var now = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
        var commandInbox =
            new CopilotBackgroundShellCompletionInbox(() => now);
        for (var index = 0;
            index <= CopilotBackgroundShellCompletionInbox
                .MaximumPendingCompletionsPerConversation;
            index++)
        {
            Assert.True(commandInbox.TryEnqueue(CreateCompletion(
                $"background:{index}")));
            now = now.AddSeconds(1);
        }

        using var commandDelivery =
            commandInbox.BeginDelivery("conversation");
        Assert.Equal(
            CopilotBackgroundShellCompletionInbox
                .MaximumPendingCompletionsPerConversation,
            commandDelivery.Completions.Count);
        Assert.DoesNotContain(commandDelivery.Completions, item =>
            item.Snapshot.Id == "background:0");

        var conversationInbox =
            new CopilotBackgroundShellCompletionInbox(() => now);
        for (var index = 0;
            index <= CopilotBackgroundShellCompletionInbox
                .MaximumPendingConversations;
            index++)
        {
            Assert.True(conversationInbox.TryEnqueue(CreateCompletion(
                $"background:{index}",
                conversationId: $"conversation:{index}")));
            now = now.AddSeconds(1);
        }

        Assert.Equal(
            CopilotBackgroundShellCompletionInbox
                .MaximumPendingConversations,
            conversationInbox.PendingCompletionCount);
        Assert.Empty(
            conversationInbox.BeginDelivery("conversation:0").Completions);
        Assert.Single(
            conversationInbox.BeginDelivery(
                $"conversation:{CopilotBackgroundShellCompletionInbox.MaximumPendingConversations}")
                .Completions);
    }

    [Fact]
    public void ExpiredCompletionIsNotDeliveredOrReturned()
    {
        var now = DateTimeOffset.Parse("2026-07-31T00:00:00Z");
        var inbox = new CopilotBackgroundShellCompletionInbox(() => now);
        Assert.True(inbox.TryEnqueue(CreateCompletion("background:one")));

        now += CopilotBackgroundShellCompletionInbox.Retention
            + TimeSpan.FromTicks(1);

        Assert.Empty(inbox.BeginDelivery("conversation").Completions);
        Assert.Equal(0, inbox.PendingCompletionCount);
    }

    private static CopilotBackgroundShellCommandSnapshot CreateCompletion(
        string backgroundId,
        CopilotBackgroundShellCommandState state =
            CopilotBackgroundShellCommandState.Completed,
        int? exitCode = 0,
        string conversationId = "conversation")
    {
        return new CopilotBackgroundShellCommandSnapshot(
            backgroundId,
            conversationId,
            "task",
            CopilotShellKind.PowerShell,
            @"C:\workspace",
            "background command",
            new string('a', 64),
            DateTimeOffset.Parse("2026-07-31T00:00:00Z"),
            DateTimeOffset.Parse("2026-07-31T00:00:01Z"),
            42,
            true,
            state,
            exitCode,
            "output",
            string.Empty);
    }
}

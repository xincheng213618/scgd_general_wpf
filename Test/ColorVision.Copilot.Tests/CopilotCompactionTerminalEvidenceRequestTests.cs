using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCompactionTerminalEvidenceRequestTests
{
    private static readonly CopilotConversationHistoryLimits GenerousLimits = new(32, 64_000, 32_000);

    [Theory]
    [InlineData(CopilotAgentStopReason.Paused)]
    [InlineData(CopilotAgentStopReason.Blocked)]
    public void OutboundRequestIncludesExactTerminalEvidenceHiddenByAnOlderSummary(CopilotAgentStopReason stopReason)
    {
        var conversation = CreatePreviouslyCompactedConversation(stopReason);
        var originalSummary = conversation.Compaction!.Summary;
        var summarySource = CopilotConversationCompactionContext.CreateSummaryMessage(conversation.Compaction);
        var compactRequest = CopilotConversationCompactionPrompt.BuildRequest(string.Empty);
        var plan = CopilotConversationCompactionPlanner.Create(conversation, GenerousLimits, compactRequest);

        // This is the same outbound assembly used by CompactConversationAsync.
        var outbound = plan.SourceMessages.Append(new CopilotRequestMessage("user", compactRequest)).ToArray();
        var marker = CopilotConversationCompactionTerminalEvidence.FormatAgentMarker(stopReason);

        Assert.Contains(stopReason, plan.TerminalEvidence.IncompleteAgentStopReasons);
        Assert.DoesNotContain(marker, compactRequest, StringComparison.Ordinal);
        Assert.DoesNotContain(marker, CopilotConversationCompactionPrompt.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains(outbound, message => message.Content.Contains(marker, StringComparison.Ordinal));
        Assert.Contains(plan.SourceMessages, message =>
            message.Content.Contains(CopilotConversationCompactionTerminalEvidence.ResponseInterruptedMarker, StringComparison.Ordinal));
        Assert.Contains(summarySource, plan.SourceMessages);
        Assert.Equal(originalSummary, conversation.Compaction.Summary);
        Assert.Throws<InvalidOperationException>(() => plan.TerminalEvidence.EnsurePreserved("Earlier work was partial."));
    }

    [Theory]
    [InlineData("messages")]
    [InlineData("weight")]
    public void RequiredEvidenceMustFitAlongsideTheSummaryAndOneCompleteTurn(string constrainedBudget)
    {
        var conversation = CreatePreviouslyCompactedConversation(CopilotAgentStopReason.Paused);
        var compactRequest = CopilotConversationCompactionPrompt.BuildRequest(string.Empty);
        var sourceWeight = GetReplacedSourceWeight(conversation);
        var limits = constrainedBudget == "messages"
            // Summary, one user/assistant pair, and the request already fill these slots.
            ? new CopilotConversationHistoryLimits(4, 64_000, 32_000)
            : new CopilotConversationHistoryLimits(32,
                checked((int)(sourceWeight + CopilotTokenEstimator.EstimateTextWeight(compactRequest))),
                32_000);

        Assert.Throws<InvalidOperationException>(() =>
            CopilotConversationCompactionPlanner.Create(conversation, limits, compactRequest));
    }

    [Fact]
    public void HostEvidenceDoesNotInflateTheReplacedSourceOrItsCharacterCounters()
    {
        var conversation = CreatePreviouslyCompactedConversation(CopilotAgentStopReason.Paused);
        var compactRequest = CopilotConversationCompactionPrompt.BuildRequest(string.Empty);
        var plan = CopilotConversationCompactionPlanner.Create(conversation, GenerousLimits, compactRequest);
        var expectedSourceWeight = GetReplacedSourceWeight(conversation);
        var originalNewCharacters = conversation.Messages[2].ModelContent.Length + conversation.Messages[3].ModelContent.Length;

        Assert.Equal(expectedSourceWeight, plan.SourceEstimatedWeight);
        Assert.Equal(2, plan.NewSourceMessageCount);
        Assert.Equal(4, plan.TotalSourceMessageCount);
        Assert.Equal(originalNewCharacters, plan.NewSourceCharacters);
        Assert.Equal(conversation.Compaction!.SourceCharacters + originalNewCharacters, plan.TotalSourceCharacters);
        Assert.Same(conversation.Messages[3], plan.BoundaryMessage);
        plan.EnsureSourceStillCurrent(conversation);

        var outbound = plan.SourceMessages.Append(new CopilotRequestMessage("user", compactRequest)).ToArray();
        Assert.InRange(outbound.Length, 1, GenerousLimits.MaximumMessages);
        Assert.InRange(outbound.Sum(message => (long)CopilotTokenEstimator.EstimateTextWeight(message.Content)),
            1, GenerousLimits.MaximumCharacters);
        Assert.Throws<InvalidOperationException>(() => plan.EnsureSummaryShrinks(new string('x', checked((int)expectedSourceWeight))));
    }

    [Fact]
    public void ExistingSummaryWithAllMarkersNeedsNoAdditionalSourceMessage()
    {
        var conversation = CreatePreviouslyCompactedConversation(CopilotAgentStopReason.Paused);
        conversation.Compaction!.Summary += "\n"
            + CopilotConversationCompactionTerminalEvidence.ResponseInterruptedMarker + "\n"
            + CopilotConversationCompactionTerminalEvidence.FormatAgentMarker(CopilotAgentStopReason.Paused);
        var compactRequest = CopilotConversationCompactionPrompt.BuildRequest(string.Empty);
        var limits = new CopilotConversationHistoryLimits(4, 64_000, 32_000);

        var plan = CopilotConversationCompactionPlanner.Create(conversation, limits, compactRequest);

        Assert.Equal(3, plan.SourceMessages.Length);
        Assert.Equal(CopilotConversationCompactionContext.CreateSummaryMessage(conversation.Compaction), plan.SourceMessages[0]);
        Assert.Equal(GetReplacedSourceWeight(conversation), plan.SourceEstimatedWeight);
        plan.TerminalEvidence.EnsurePreserved(conversation.Compaction.Summary);
    }

    [Fact]
    public void FirstCompactionUsesTerminalMarkersAlreadyPresentInOriginalMessages()
    {
        var conversation = CreatePreviouslyCompactedConversation(CopilotAgentStopReason.Paused);
        conversation.Compaction = null;
        var compactRequest = CopilotConversationCompactionPrompt.BuildRequest(string.Empty);
        var limits = new CopilotConversationHistoryLimits(3, 64_000, 32_000);

        var plan = CopilotConversationCompactionPlanner.Create(conversation, limits, compactRequest);

        Assert.Equal(2, plan.SourceMessages.Length);
        Assert.Same(conversation.Messages[1], plan.BoundaryMessage);
        Assert.Equal(plan.SourceMessages.Sum(message => (long)CopilotTokenEstimator.EstimateTextWeight(message.Content)),
            plan.SourceEstimatedWeight);
        plan.TerminalEvidence.EnsurePreserved(plan.SourceMessages[1].Content);
    }

    private static long GetReplacedSourceWeight(CopilotConversationRecord conversation) =>
        (long)CopilotTokenEstimator.EstimateTextWeight(CopilotConversationCompactionContext.CreateSummaryMessage(conversation.Compaction!).Content)
        + CopilotTokenEstimator.EstimateTextWeight(conversation.Messages[2].ModelContent)
        + CopilotTokenEstimator.EstimateTextWeight(conversation.Messages[3].ModelContent);

    private static CopilotConversationRecord CreatePreviouslyCompactedConversation(CopilotAgentStopReason stopReason)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Inspect the earlier task."));
        var interrupted = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial original findings.")
        {
            RequestMode = CopilotAgentMode.Code,
            AgentStopReason = stopReason,
        };
        interrupted.MarkResponseInterrupted("The provider interrupted the old turn.");
        conversation.Messages.Add(interrupted);
        conversation.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "Earlier findings. " + new string('a', 1_200),
            ThroughMessageId = interrupted.Id,
            SourceMessageCount = 2,
            SourceCharacters = 4_000,
        };
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Inspect more evidence. " + new string('b', 400)));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Additional findings. " + new string('c', 400)));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Keep this recent question."));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Keep this recent answer."));
        return conversation;
    }
}

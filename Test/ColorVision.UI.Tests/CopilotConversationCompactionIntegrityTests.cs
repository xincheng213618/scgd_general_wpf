using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationCompactionIntegrityTests
{
    [Fact]
    public void PlannerCapturesTerminalEvidenceFromOriginalAssistantTurns()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var interrupted = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial answer.");
        interrupted.MarkResponseInterrupted("provider disconnected");
        var paused = new CopilotChatMessage(CopilotChatRole.Assistant, "One task remains.")
        {
            RequestMode = CopilotAgentMode.Auto,
            AgentStopReason = CopilotAgentStopReason.Paused,
        };
        var completed = new CopilotChatMessage(CopilotChatRole.Assistant, "Later work completed.")
        {
            RequestMode = CopilotAgentMode.Auto,
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "First request"));
        conversation.Messages.Add(interrupted);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Continue"));
        conversation.Messages.Add(paused);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Finish"));
        conversation.Messages.Add(completed);

        var plan = CopilotConversationCompactionPlanner.Create(
            conversation,
            new CopilotConversationHistoryLimits(32, 64_000, 32_000),
            CopilotConversationCompactionPrompt.BuildRequest(string.Empty));

        Assert.Same(paused, plan.BoundaryMessage);
        Assert.True(plan.TerminalEvidence.HasResponseInterruption);
        Assert.Equal(
            [CopilotAgentStopReason.Paused],
            plan.TerminalEvidence.IncompleteAgentStopReasons);
        Assert.Contains(
            plan.SourceMessages,
            message => message.Content.Contains(
                CopilotConversationCompactionTerminalEvidence.ResponseInterruptedMarker,
                StringComparison.Ordinal));
        Assert.Contains(
            plan.SourceMessages,
            message => message.Content.Contains(
                CopilotConversationCompactionTerminalEvidence.FormatAgentMarker(
                    CopilotAgentStopReason.Paused),
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecompactionRecoversEvidenceOmittedByThePreviousSummary()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var interrupted = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial answer.");
        interrupted.MarkResponseInterrupted("provider disconnected");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "First request"));
        conversation.Messages.Add(interrupted);
        conversation.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "Legacy summary that omitted the interruption boundary.",
            ThroughMessageId = interrupted.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            SourceMessageCount = 2,
            SourceCharacters = 64,
        };
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Second request"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Second answer."));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Third request"));
        var blocked = new CopilotChatMessage(CopilotChatRole.Assistant, "Waiting on evidence.")
        {
            RequestMode = CopilotAgentMode.Code,
            AgentStopReason = CopilotAgentStopReason.Blocked,
        };
        conversation.Messages.Add(blocked);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Fourth request"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Fourth answer."));

        var plan = CopilotConversationCompactionPlanner.Create(
            conversation,
            new CopilotConversationHistoryLimits(32, 64_000, 32_000),
            CopilotConversationCompactionPrompt.BuildRequest(string.Empty));

        Assert.Same(blocked, plan.BoundaryMessage);
        Assert.True(plan.TerminalEvidence.HasResponseInterruption);
        Assert.Equal(
            [CopilotAgentStopReason.Blocked],
            plan.TerminalEvidence.IncompleteAgentStopReasons);
        Assert.DoesNotContain(
            plan.SourceMessages,
            message => message.Content.Contains(
                CopilotConversationCompactionTerminalEvidence.ResponseInterruptedMarker,
                StringComparison.Ordinal));
        Assert.False(plan.TerminalEvidence.IsPreservedBy(
            CopilotConversationCompactionTerminalEvidence.FormatAgentMarker(
                CopilotAgentStopReason.Blocked)));
    }

    [Fact]
    public void TerminalEvidenceRejectsSummaryThatDropsARequiredMarker()
    {
        var interrupted = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial answer.");
        interrupted.MarkResponseInterrupted("provider disconnected");
        var blocked = new CopilotChatMessage(CopilotChatRole.Assistant, "Waiting on evidence.")
        {
            RequestMode = CopilotAgentMode.Code,
            AgentStopReason = CopilotAgentStopReason.Blocked,
        };
        var evidence = CopilotConversationCompactionTerminalEvidence.Capture(
            [interrupted, blocked]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            evidence.EnsurePreserved(
                "The earlier response was interrupted. "
                + CopilotConversationCompactionTerminalEvidence.ResponseInterruptedMarker));

        Assert.Contains(
            CopilotConversationCompactionTerminalEvidence.FormatAgentMarker(
                CopilotAgentStopReason.Blocked),
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TerminalEvidenceAcceptsEveryDistinctRequiredMarker()
    {
        var interrupted = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial answer.");
        interrupted.MarkResponseInterrupted("provider disconnected");
        var blocked = new CopilotChatMessage(CopilotChatRole.Assistant, "Waiting on evidence.")
        {
            RequestMode = CopilotAgentMode.Code,
            AgentStopReason = CopilotAgentStopReason.Blocked,
        };
        var evidence = CopilotConversationCompactionTerminalEvidence.Capture(
            [interrupted, blocked]);
        var summary = "Partial earlier turn "
            + CopilotConversationCompactionTerminalEvidence.ResponseInterruptedMarker
            + "\nBlocked Agent turn "
            + CopilotConversationCompactionTerminalEvidence.FormatAgentMarker(
                CopilotAgentStopReason.Blocked);

        Assert.True(evidence.IsPreservedBy(summary));
        evidence.EnsurePreserved(summary);
    }

    [Fact]
    public void PromptPlacesTerminalIntegrityAfterOptionalUserFocus()
    {
        var request = CopilotConversationCompactionPrompt.BuildRequest(
            "Focus on the renderer changes.");

        Assert.Contains("Focus on the renderer changes.", request, StringComparison.Ordinal);
        Assert.Contains("<assistant_response_interrupted>", request, StringComparison.Ordinal);
        Assert.Contains(
            "<agent_turn_incomplete stop_reason=\"...\">",
            request,
            StringComparison.Ordinal);
        Assert.True(
            request.IndexOf("Terminal-state integrity:", StringComparison.Ordinal)
            > request.IndexOf("Focus on the renderer changes.", StringComparison.Ordinal));
        Assert.Contains("<assistant_response_interrupted>", CopilotConversationCompactionPrompt.SystemPrompt, StringComparison.Ordinal);
    }
}

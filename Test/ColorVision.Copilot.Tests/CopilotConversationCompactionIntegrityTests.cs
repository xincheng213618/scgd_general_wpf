using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

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
    public void SummaryMustActuallyShrinkTheReplacedContext()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, new string('a', 2_000)));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, new string('b', 2_000)));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Recent request"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Recent answer"));
        var plan = CopilotConversationCompactionPlanner.Create(
            conversation,
            new CopilotConversationHistoryLimits(32, 64_000, 32_000),
            CopilotConversationCompactionPrompt.BuildRequest(string.Empty));

        plan.EnsureSummaryShrinks("Earlier work established the relevant bounded context.");
        var exception = Assert.Throws<InvalidOperationException>(() =>
            plan.EnsureSummaryShrinks(new string('x', 5_000)));

        Assert.Contains("没有缩小上下文", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CommitGuardRejectsAChangedSourcePrefix()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var firstRequest = new CopilotChatMessage(
            CopilotChatRole.User,
            "Inspect the original source.");
        conversation.Messages.Add(firstRequest);
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "Original findings."));
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.User,
            "Keep this recent request."));
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "Keep this recent answer."));
        var plan = CopilotConversationCompactionPlanner.Create(
            conversation,
            new CopilotConversationHistoryLimits(32, 64_000, 32_000),
            CopilotConversationCompactionPrompt.BuildRequest(string.Empty));

        firstRequest.Content = "The source changed while compaction was running.";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            plan.EnsureSourceStillCurrent(conversation));
        Assert.Contains("源对话", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CommitGuardAllowsMessagesAppendedAfterTheBoundary()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.User,
            "Inspect the original source."));
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "Original findings."));
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.User,
            "Keep this recent request."));
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "Keep this recent answer."));
        var plan = CopilotConversationCompactionPlanner.Create(
            conversation,
            new CopilotConversationHistoryLimits(32, 64_000, 32_000),
            CopilotConversationCompactionPrompt.BuildRequest(string.Empty));

        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.User,
            "This arrived after the planned boundary."));

        plan.EnsureSourceStillCurrent(conversation);
    }

    [Fact]
    public void CommitGuardRejectsAChangedExistingSummary()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.User,
            "Earlier request."));
        var earlierAnswer = new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "Earlier answer.");
        conversation.Messages.Add(earlierAnswer);
        conversation.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "Original earlier summary.",
            ThroughMessageId = earlierAnswer.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            SourceMessageCount = 2,
            SourceCharacters = 32,
        };
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.User,
            "Next request."));
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "Next answer."));
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.User,
            "Keep this recent request."));
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "Keep this recent answer."));
        var plan = CopilotConversationCompactionPlanner.Create(
            conversation,
            new CopilotConversationHistoryLimits(32, 64_000, 32_000),
            CopilotConversationCompactionPrompt.BuildRequest(string.Empty));

        conversation.Compaction.Summary = "A different summary was committed concurrently.";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            plan.EnsureSourceStillCurrent(conversation));
        Assert.Contains("已有摘要", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InterruptedAgentTurnPreservesBothTerminalMarkers()
    {
        var paused = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial Agent answer.")
        {
            RequestMode = CopilotAgentMode.Auto,
            AgentStopReason = CopilotAgentStopReason.Paused,
        };
        paused.MarkResponseInterrupted("paused after checkpoint");
        var agentMarker = CopilotConversationCompactionTerminalEvidence.FormatAgentMarker(
            CopilotAgentStopReason.Paused);

        Assert.Contains(
            CopilotConversationCompactionTerminalEvidence.ResponseInterruptedMarker,
            paused.ModelContent,
            StringComparison.Ordinal);
        Assert.Contains(agentMarker, paused.ModelContent, StringComparison.Ordinal);

        var evidence = CopilotConversationCompactionTerminalEvidence.Capture([paused]);

        Assert.True(evidence.HasResponseInterruption);
        Assert.Equal([CopilotAgentStopReason.Paused], evidence.IncompleteAgentStopReasons);
        Assert.Throws<InvalidOperationException>(() => evidence.EnsurePreserved(
            CopilotConversationCompactionTerminalEvidence.ResponseInterruptedMarker));
        evidence.EnsurePreserved(
            CopilotConversationCompactionTerminalEvidence.ResponseInterruptedMarker
            + Environment.NewLine
            + agentMarker);
    }

    [Fact]
    public void EmptyCompactPromptUsesTheDefaultBodyAndKeepsHostIntegrity()
    {
        var request = CopilotConversationCompactionPrompt.BuildRequest(null, string.Empty);

        Assert.StartsWith("Create a continuation summary", request, StringComparison.Ordinal);
        Assert.Contains("ColorVision host integrity requirements", request, StringComparison.Ordinal);
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

    [Fact]
    public void AutomaticFocusCombinesMandatoryAndConfiguredPriorities()
    {
        var focus = CopilotConversationCompactionPrompt.BuildAutomaticFocus(
            "Preserve exact device identifiers and unresolved hardware verification.");
        var request = CopilotConversationCompactionPrompt.BuildRequest(focus);

        Assert.Contains("任务目标", focus, StringComparison.Ordinal);
        Assert.Contains("用户配置的长期压缩重点", focus, StringComparison.Ordinal);
        Assert.Contains("exact device identifiers", focus, StringComparison.Ordinal);
        Assert.Contains("Terminal-state integrity:", request, StringComparison.Ordinal);
        Assert.True(
            request.IndexOf("Terminal-state integrity:", StringComparison.Ordinal)
            > request.IndexOf("exact device identifiers", StringComparison.Ordinal));
    }
}

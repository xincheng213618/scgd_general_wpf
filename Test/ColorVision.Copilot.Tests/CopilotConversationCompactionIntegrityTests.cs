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
        Assert.Contains(
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequestBuilderHonorsExactTerminalEvidenceBudgetAndRejectsSmallerBudget(bool fitsExactly)
    {
        var limits = CopilotConversationRequestBuilder.ResolveHistoryLimits(
            CopilotAgentTokenBudget.MinimumContextWindowTokens, CopilotProfileConfig.DefaultMaxTokens) with
        {
            MaximumContentCharacters = checked((int)CopilotTokenEstimator.EstimateTextWeight(
                CopilotChatMessage.ResponseInterruptionModelMarker)) - (fitsExactly ? 0 : 1),
        };
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Earlier question."));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant,
            new string('a', limits.MaximumContentCharacters + 1));
        assistant.MarkResponseInterrupted();
        conversation.Messages.Add(assistant);
        var originalModelContent = assistant.ModelContent;

        var snapshot = CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation);
        if (fitsExactly)
        {
            var history = CopilotConversationRequestBuilder.BuildChatHistory(snapshot,
                "Next question.", attachments: null, limits, includeAttachmentContext: false);
            var retained = Assert.Single(history, message => message.Role == "assistant");
            Assert.Equal(CopilotChatMessage.ResponseInterruptionModelMarker, retained.Content);
            Assert.Equal(limits.MaximumContentCharacters,
                CopilotTokenEstimator.EstimateTextWeight(retained.Content));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => CopilotConversationRequestBuilder.BuildChatHistory(snapshot,
                "Next question.", attachments: null, limits, includeAttachmentContext: false));
        }

        Assert.True(assistant.WasResponseInterrupted);
        Assert.Equal(originalModelContent, assistant.ModelContent);
        Assert.Equal(CopilotAgentStopReason.None, assistant.AgentStopReason);
    }

    [Fact]
    public void AgentFirstPromptKeepsTerminalEvidenceWithoutReplayingPriorUserAttachmentContext()
    {
        var limits = CopilotConversationRequestBuilder.ResolveHistoryLimits(
            CopilotAgentTokenBudget.MinimumContextWindowTokens, CopilotProfileConfig.DefaultMaxTokens);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        const string priorAttachment = "Previously injected attachment context.";
        var user = new CopilotChatMessage(CopilotChatRole.User, "Earlier question.")
        {
            RequestContent = "Earlier question.\n" + priorAttachment,
        };
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant,
            new string('a', limits.MaximumContentCharacters + 1))
        {
            RequestMode = CopilotAgentMode.Auto,
            AgentStopReason = CopilotAgentStopReason.Paused,
        };
        assistant.MarkResponseInterrupted();
        conversation.Messages.Add(user);
        conversation.Messages.Add(assistant);
        var originalModelContent = assistant.ModelContent;
        var terminalSuffix = originalModelContent[assistant.Content.Length..];
        var visibleHistory = CopilotConversationRequestBuilder.BuildVisibleHistory(
            CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation), limits);
        var request = new CopilotAgentRequest
        {
            UserText = "Continue from the known state.",
            Mode = CopilotAgentMode.Code,
            Profile = new CopilotProfileConfig { MaxTokens = CopilotProfileConfig.DefaultMaxTokens },
            History = visibleHistory,
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                ContextWindowTokens = CopilotAgentTokenBudget.MinimumContextWindowTokens,
            },
        };

        var prepared = new CopilotAgentContextBuilder().BuildHarnessMessages(
            request, Array.Empty<CopilotAgentStepRecord>(), minimalDelegatedFinalization: false);

        Assert.Equal(user.Content, prepared.Messages[0].Content);
        Assert.DoesNotContain(prepared.Messages,
            message => message.Content.Contains(priorAttachment, StringComparison.Ordinal));
        var retained = Assert.Single(prepared.Messages, message => message.Role == "assistant");
        Assert.EndsWith(terminalSuffix, retained.Content, StringComparison.Ordinal);
        Assert.True(CopilotTokenEstimator.EstimateTextWeight(retained.Content) <= limits.MaximumContentCharacters);
        Assert.Equal(originalModelContent, assistant.ModelContent);
        Assert.True(assistant.WasResponseInterrupted);
        Assert.Equal(CopilotAgentStopReason.Paused, assistant.AgentStopReason);
        Assert.Equal("Earlier question.\n" + priorAttachment, user.RequestContent);
    }

}

using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAssistantMessagePresenterTests
{
    [Fact]
    public void StatusReasoningAndAnswerEventsBuildResponseTimelineWithDeferredStreamPersistence()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);

        var statusResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(message, CopilotAgentEvent.Status("Working"));
        var reasoningResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(message, CopilotAgentEvent.ReasoningDelta("Inspecting context."));
        var answerResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(message, CopilotAgentEvent.AnswerDelta("The inspection is complete."));

        Assert.True(statusResult.IsHandled);
        Assert.Equal(CopilotAgentEventPersistenceMode.None, statusResult.PersistenceMode);
        Assert.Equal(CopilotAgentEventPersistenceMode.Deferred, reasoningResult.PersistenceMode);
        Assert.Equal(CopilotAgentEventPersistenceMode.Deferred, answerResult.PersistenceMode);
        Assert.True(message.UsesResponseTimeline);
        Assert.Equal("Inspecting context.", message.ReasoningContent);
        Assert.Equal("The inspection is complete.", message.Content);
        Assert.False(message.IsReasoningInProgress);
        Assert.False(message.IsReasoningExpanded);
        Assert.True(message.IsExecutionInProgress);
        Assert.Equal("The inspection is complete.", Assert.Single(message.VisibleResponseTimelineItems).Markdown);
    }

    [Fact]
    public void ToolEventsUpsertOneTracePreserveTimelineOrderAndRequestDeferredPersistence()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        var startedExecution = CreateExecution(CopilotToolExecutionState.Running);
        var completedExecution = CreateExecution(CopilotToolExecutionState.Completed, completed: true);

        var startedResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(message, CopilotAgentEvent.ToolStarted(startedExecution));
        var completedResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(message, CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "InspectTcpPort",
                Success = true,
                Summary = "Port 6666 is available.",
            },
            completedExecution));

        Assert.Equal(CopilotAgentEventPersistenceMode.Deferred, startedResult.PersistenceMode);
        Assert.Equal(CopilotAgentEventPersistenceMode.Deferred, completedResult.PersistenceMode);
        var trace = Assert.Single(message.AgentTraceEntries);
        Assert.Equal(CopilotToolExecutionState.Completed, trace.State);
        Assert.Equal("Port 6666 is available.", trace.ResultSummary);
        Assert.True(message.IsExecutionInProgress);
        var timelineGroup = Assert.Single(message.VisibleResponseTimelineItems).ToolGroup;
        Assert.NotNull(timelineGroup);
        Assert.Single(timelineGroup!.Entries);
        Assert.Equal("InspectTcpPort", timelineGroup.Entries[0].ToolName);
    }

    [Fact]
    public void LegacyToolStartedEventWithoutExecutionKeepsVisibleSanitizedTrace()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        var agentEvent = new CopilotAgentEvent
        {
            Type = CopilotAgentEventType.ToolStarted,
            Text = "Legacy tool started.",
        };

        var result = CopilotAssistantMessagePresenter.ApplyAgentEvent(message, agentEvent);

        Assert.Equal(CopilotAgentEventPersistenceMode.Deferred, result.PersistenceMode);
        Assert.Contains("Legacy tool started", message.ExecutionContent, StringComparison.Ordinal);
        Assert.True(message.IsExecutionInProgress);
    }

    [Fact]
    public void AnswerResetPreservesToolTimelineAndRequestsDeferredPersistence()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        CopilotAssistantMessagePresenter.ApplyAgentEvent(message, CopilotAgentEvent.Status("Working"));
        CopilotAssistantMessagePresenter.ApplyAgentEvent(message, CopilotAgentEvent.AnswerDelta("Unsupported draft."));
        CopilotAssistantMessagePresenter.ApplyAgentEvent(message, CopilotAgentEvent.ToolStarted(CreateExecution(CopilotToolExecutionState.Running)));

        var resetResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(message, CopilotAgentEvent.AnswerReset());
        CopilotAssistantMessagePresenter.ApplyAgentEvent(message, CopilotAgentEvent.AnswerDelta("Verified answer."));

        Assert.Equal(CopilotAgentEventPersistenceMode.Deferred, resetResult.PersistenceMode);
        Assert.DoesNotContain("Unsupported draft", message.Content, StringComparison.Ordinal);
        Assert.Equal("Verified answer.", message.Content);
        Assert.Equal(2, message.VisibleResponseTimelineItems.Count);
        Assert.True(message.VisibleResponseTimelineItems[0].IsToolGroup);
        Assert.Equal("Verified answer.", message.VisibleResponseTimelineItems[1].Markdown);
    }

    [Fact]
    public void TerminalEventsRequestImmediatePersistenceAndCheckpointEventsRemainHostOwned()
    {
        var errorMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            IsExecutionInProgress = true,
            IsReasoningInProgress = true,
        };
        errorMessage.MarkThinkingStarted();

        var errorResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(errorMessage, CopilotAgentEvent.Error("Tool execution failed."));
        var completedResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(errorMessage, CopilotAgentEvent.Completed());
        var checkpointResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(errorMessage, CopilotAgentEvent.CheckpointReady());

        Assert.Equal(CopilotAgentEventPersistenceMode.Immediate, errorResult.PersistenceMode);
        Assert.Equal(CopilotAgentEventPersistenceMode.Immediate, completedResult.PersistenceMode);
        Assert.False(errorMessage.IsExecutionInProgress);
        Assert.False(errorMessage.IsReasoningInProgress);
        Assert.False(errorMessage.IsThinkingInProgress);
        Assert.Contains("Tool execution failed", errorMessage.ExecutionContent, StringComparison.Ordinal);
        Assert.False(checkpointResult.IsHandled);
        Assert.Equal(CopilotAgentEventPersistenceMode.None, checkpointResult.PersistenceMode);
    }

    [Fact]
    public void FinalizeMessageUsesTimelineFallbackAndCompletesThinking()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        message.BeginResponseTimeline();
        message.MarkThinkingStarted();
        CopilotAssistantMessagePresenter.AppendExecutionTrace(message, "Runtime diagnostic.");

        CopilotAssistantMessagePresenter.FinalizeMessage(message);

        Assert.False(message.IsThinkingInProgress);
        Assert.Equal("No final answer was received; only execution trace or reasoning content is available.", message.Content);
        Assert.Equal(message.Content, Assert.Single(message.VisibleResponseTimelineItems).Markdown);
    }

    private static CopilotToolExecutionInfo CreateExecution(CopilotToolExecutionState state, bool completed = false) => new()
    {
        CallId = "call-1",
        Round = 1,
        RuntimeName = "agent-framework",
        ToolName = "InspectTcpPort",
        Access = CopilotToolAccess.ReadOnly,
        RiskLevel = CopilotToolRiskLevel.Low,
        ApprovalMode = CopilotToolApprovalMode.Never,
        Idempotency = CopilotToolIdempotency.Idempotent,
        ConcurrencyMode = CopilotToolConcurrencyMode.SharedRead,
        State = state,
        StartedAtUtc = DateTimeOffset.UtcNow,
        CompletedAtUtc = completed ? DateTimeOffset.UtcNow : null,
        DurationMs = completed ? 42 : 0,
    };
}

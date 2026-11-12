namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnEventReducerTests
{
    [Fact]
    public void ReduceReturnsNewStateWithoutMutatingThePreviousState()
    {
        var initial = CreateStartedState(CopilotAgentMode.Chat);
        var prepared = new CopilotTurnRequestPreparedEvent(
            new CopilotPreparedTurnRequest("prepared", true));

        var next = CopilotTurnEventReducer.Reduce(initial, prepared);

        Assert.False(initial.ChatRequestPrepared);
        Assert.Null(initial.Completion);
        Assert.True(next.ChatRequestPrepared);
        Assert.Null(next.Completion);
    }

    [Fact]
    public void ReplayTraceProducesCompletionStateForChatTurn()
    {
        var result = CreateChatResult();
        var trace = new CopilotTurnEvent[]
        {
            new CopilotTurnRequestPreparedEvent(new CopilotPreparedTurnRequest("prepared", false)),
            new CopilotTurnChatDeltaEvent(new CopilotStreamDelta(string.Empty, "partial")),
            new CopilotTurnProviderRetryEvent(
                new CopilotProviderRetryInfo(1, 2, 3, TimeSpan.Zero, "timeout", null)),
            new CopilotTurnCompletedEvent(result),
        };

        var state = Replay(CopilotTurnEventState.Create(CopilotAgentMode.Chat), trace);

        Assert.True(state.ChatRequestPrepared);
        Assert.Same(result, CopilotTurnEventReducer.RequireCompletion(state));
    }

    [Fact]
    public void ReplayTraceProducesCompletionStateForAgentTurn()
    {
        var result = CopilotTurnResult.FromAgent(
            CopilotAgentMode.Auto,
            CopilotTokenUsage.Empty,
            new CopilotAgentRunResult());
        var trace = new CopilotTurnEvent[]
        {
            new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("partial")),
            new CopilotTurnAgentEvent(CopilotAgentEvent.Completed()),
            CreatePlanEvent(result.AgentRunResult!.TaskLedger),
            new CopilotTurnCompletedEvent(result),
        };

        var state = Replay(CopilotTurnEventState.Create(CopilotAgentMode.Auto), trace);

        Assert.True(state.AgentCompleted);
        Assert.Same(result, CopilotTurnEventReducer.RequireCompletion(state));
    }

    [Fact]
    public void ReplayTraceProducesCompletionStateForReviewTurn()
    {
        var result = CopilotTurnResult.FromAgent(
            CopilotAgentMode.Review,
            CopilotTokenUsage.Empty,
            new CopilotAgentRunResult());
        var trace = new CopilotTurnEvent[]
        {
            new CopilotTurnReviewEnteredEvent(new CopilotWorkspaceReviewTargetContext
            {
                Target = CopilotWorkspaceReviewTarget.BaseBranch,
                Revision = "origin/develop",
            }),
            new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("finding")),
            new CopilotTurnAgentEvent(CopilotAgentEvent.Completed()),
            CreatePlanEvent(result.AgentRunResult!.TaskLedger),
            new CopilotTurnReviewExitedEvent(new CopilotWorkspaceReviewTargetContext
            {
                Target = CopilotWorkspaceReviewTarget.BaseBranch,
                Revision = "origin/develop",
            }, "finding", false),
            new CopilotTurnCompletedEvent(result),
        };

        var state = Replay(CopilotTurnEventState.Create(CopilotAgentMode.Review), trace);

        Assert.True(state.ReviewEntered);
        Assert.True(state.AgentCompleted);
        Assert.True(state.ReviewExited);
        Assert.Same(result, CopilotTurnEventReducer.RequireCompletion(state));
    }

    [Fact]
    public void ReviewRejectsExitWithDifferentTarget()
    {
        var state = Replay(
            CopilotTurnEventState.Create(CopilotAgentMode.Review),
            [
                new CopilotTurnReviewEnteredEvent(new CopilotWorkspaceReviewTargetContext
                {
                    Target = CopilotWorkspaceReviewTarget.BaseBranch,
                    Revision = "origin/develop",
                }),
                new CopilotTurnAgentEvent(CopilotAgentEvent.Completed()),
            ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(
                state,
                new CopilotTurnReviewExitedEvent(new CopilotWorkspaceReviewTargetContext
                {
                    Target = CopilotWorkspaceReviewTarget.BaseBranch,
                    Revision = "origin/main",
                }, "finding", false)));

        Assert.Contains("different target", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatePersistenceBarrierWaitsForConsumerCommit()
    {
        var emitted = new List<CopilotTurnEvent>();
        var sink = new CopilotTurnEventSink(emitted.Add);

        var waitTask = sink.RequestStatePersistenceBarrierAsync(
            CancellationToken.None);

        var barrier = Assert.IsType<CopilotTurnStatePersistenceBarrierEvent>(
            Assert.Single(emitted));
        Assert.False(waitTask.IsCompleted);
        var state = CopilotTurnEventReducer.Reduce(
            CopilotTurnEventReducer.Reduce(
                CopilotTurnEventState.Create(CopilotAgentMode.Auto),
                new CopilotTurnStartedEvent(CopilotAgentMode.Auto)),
            barrier);
        Assert.True(state.Started);

        Assert.True(barrier.TryCommit());
        await waitTask;
    }

    [Fact]
    public void ReviewPresenterRecordsTargetAndDoesNotReopenCompletedExecution()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        var entered = CopilotAssistantMessagePresenter.ApplyReviewEntered(
            assistant,
            new CopilotWorkspaceReviewTargetContext
            {
                Target = CopilotWorkspaceReviewTarget.Commit,
                Revision = "abcdef1",
            });

        Assert.Equal(CopilotAgentEventPersistenceMode.Immediate, entered.PersistenceMode);
        Assert.True(assistant.IsExecutionInProgress);
        Assert.Contains("Review started · commit abcdef1", assistant.ExecutionContent, StringComparison.Ordinal);

        CopilotAssistantMessagePresenter.ApplyAgentEvent(
            assistant,
            CopilotAgentEvent.AnswerDelta("partial streamed finding"));
        Assert.Equal("partial streamed finding", assistant.Content);
        CopilotAssistantMessagePresenter.ApplyAgentEvent(assistant, CopilotAgentEvent.Completed());
        var exited = CopilotAssistantMessagePresenter.ApplyReviewExited(
            assistant,
            new CopilotWorkspaceReviewTargetContext
            {
                Target = CopilotWorkspaceReviewTarget.Commit,
                Revision = "abcdef1",
            },
            "authoritative finding",
            false);

        Assert.Equal(CopilotAgentEventPersistenceMode.Immediate, exited.PersistenceMode);
        Assert.False(assistant.IsExecutionInProgress);
        Assert.Equal("authoritative finding", assistant.Content);
        Assert.EndsWith("Review completed · commit abcdef1", assistant.ExecutionContent, StringComparison.Ordinal);
    }

    private static CopilotTurnEventState Replay(
        CopilotTurnEventState state,
        IEnumerable<CopilotTurnEvent> trace)
    {
        if (!state.Started)
        {
            state = CopilotTurnEventReducer.Reduce(
                state,
                new CopilotTurnStartedEvent(state.TurnId, state.Mode));
        }
        foreach (var turnEvent in trace)
            state = CopilotTurnEventReducer.Reduce(state, turnEvent);
        return state;
    }

    private static CopilotTurnEventState CreateStartedState(CopilotAgentMode mode) =>
        CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(mode),
            new CopilotTurnStartedEvent(mode));

    private static CopilotTurnResult CreateChatResult()
    {
        return CopilotTurnResult.FromChat(
            CopilotTokenUsage.Empty,
            "prepared",
            chatAttachmentContextCaptured: false,
            new CopilotChatStreamResult(
                CopilotTokenUsage.Empty,
                CopilotChatFinishKind.Complete,
                "stop"));
    }

    private static CopilotTurnPlanUpdatedEvent CreatePlanEvent(CopilotAgentTaskLedgerSnapshot taskLedger) =>
        new(CopilotTurnPlanSnapshot.FromTaskLedger(taskLedger));
}

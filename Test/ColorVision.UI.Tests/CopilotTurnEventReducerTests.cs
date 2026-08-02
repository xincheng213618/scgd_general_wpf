using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTurnEventReducerTests
{
    [Fact]
    public void ReduceReturnsNewStateWithoutMutatingThePreviousState()
    {
        var initial = CopilotTurnEventState.Create(CopilotAgentMode.Chat);
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
            new CopilotTurnRequestPreparedEvent(new CopilotPreparedTurnRequest("prepared", true)),
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
            new CopilotTurnCompletedEvent(result),
        };

        var state = Replay(CopilotTurnEventState.Create(CopilotAgentMode.Auto), trace);

        Assert.True(state.AgentCompleted);
        Assert.Same(result, CopilotTurnEventReducer.RequireCompletion(state));
    }

    private static CopilotTurnEventState Replay(
        CopilotTurnEventState state,
        IEnumerable<CopilotTurnEvent> trace)
    {
        foreach (var turnEvent in trace)
            state = CopilotTurnEventReducer.Reduce(state, turnEvent);
        return state;
    }

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
}

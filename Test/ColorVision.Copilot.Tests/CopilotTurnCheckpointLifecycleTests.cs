using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnCheckpointLifecycleTests
{
    private static readonly DateTimeOffset InitialUpdate =
        new(2026, 8, 8, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReducerAcceptsPublishedReadyAndUpdatedCheckpointLifecycle()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(
                CreateCheckpoint(InitialUpdate),
                CreateTaskLedger()));
        state = Observe(state, CopilotAgentEvent.CheckpointReady());
        state = Observe(
            state,
            CopilotAgentEvent.CheckpointUpdated(
                CreateCheckpoint(InitialUpdate.AddSeconds(1), serializedSessionJson: "{\"round\":2}"),
                CreateTaskLedger(completed: true)));
        state = Observe(state, CopilotAgentEvent.Completed());

        Assert.True(state.CheckpointLifecycle.Ready);
        Assert.True(state.AgentCompleted);
    }

    [Fact]
    public void ReducerRejectsCheckpointReadyBeforeFirstUpdate()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(CreateStartedState(), CopilotAgentEvent.CheckpointReady()));

        Assert.Contains("before publishing one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsDuplicateCheckpointReady()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(CreateCheckpoint(InitialUpdate), CreateTaskLedger()));
        state = Observe(state, CopilotAgentEvent.CheckpointReady());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, CopilotAgentEvent.CheckpointReady()));

        Assert.Contains("more than once", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsInvalidCheckpointUpdate()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                CreateStartedState(),
                CopilotAgentEvent.CheckpointUpdated(
                    CreateCheckpoint(default),
                    CreateTaskLedger())));

        Assert.Contains("invalid checkpoint update", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsInvalidCheckpointTaskLedgerWithoutNormalizingIt()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                CreateStartedState(),
                CopilotAgentEvent.CheckpointUpdated(
                    CreateCheckpoint(InitialUpdate),
                    new CopilotAgentTaskLedgerSnapshot
                    {
                        Mode = "unexpected",
                        Items =
                        [
                            new CopilotAgentTaskItem { Id = -1, Title = "  invalid  " },
                        ],
                    })));

        Assert.Contains("invalid task ledger", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsCheckpointIdentityDrift()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(CreateCheckpoint(InitialUpdate), CreateTaskLedger()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.CheckpointUpdated(
                    CreateCheckpoint(InitialUpdate.AddSeconds(1), profileKey: "different-profile"),
                    CreateTaskLedger())));

        Assert.Contains("identity changed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsCheckpointTimestampRegression()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(
                CreateCheckpoint(InitialUpdate.AddSeconds(1)),
                CreateTaskLedger()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.CheckpointUpdated(
                    CreateCheckpoint(InitialUpdate),
                    CreateTaskLedger())));

        Assert.Contains("moved backwards", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsAgentCompletionBeforePublishedCheckpointIsReady()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(CreateCheckpoint(InitialUpdate), CreateTaskLedger()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, CopilotAgentEvent.Completed()));

        Assert.Contains("before its published checkpoint became ready", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsFinalCheckpointWithDifferentIdentity()
    {
        var ledger = CreateTaskLedger();
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(CreateCheckpoint(InitialUpdate), ledger));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnPlanUpdatedEvent(CopilotTurnPlanSnapshot.FromTaskLedger(ledger)));
        state = Observe(state, CopilotAgentEvent.CheckpointReady());
        state = Observe(state, CopilotAgentEvent.Completed());
        var result = CopilotTurnResult.FromAgent(
            CopilotAgentMode.Auto,
            CopilotTokenUsage.Empty,
            new CopilotAgentRunResult
            {
                TaskLedger = ledger,
                SessionCheckpoint = CreateCheckpoint(
                    InitialUpdate.AddSeconds(1),
                    profileKey: "different-profile"),
            });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(state, new CopilotTurnCompletedEvent(result)));

        Assert.Contains("identity changed", exception.Message, StringComparison.Ordinal);
    }

    private static CopilotTurnEventState CreateStartedState() =>
        CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Auto),
            new CopilotTurnStartedEvent(CopilotAgentMode.Auto));

    private static CopilotTurnEventState Observe(
        CopilotTurnEventState state,
        CopilotAgentEvent agentEvent) =>
        CopilotTurnEventReducer.Reduce(state, new CopilotTurnAgentEvent(agentEvent));

    private static CopilotAgentTaskLedgerSnapshot CreateTaskLedger(bool completed = false) => new()
    {
        Mode = "execute",
        Items =
        [
            new CopilotAgentTaskItem
            {
                Id = 1,
                Title = "Persist resumable state",
                Description = "Capture the current Agent session.",
                IsComplete = completed,
            },
        ],
    };

    private static CopilotAgentSessionCheckpoint CreateCheckpoint(
        DateTimeOffset updatedAtUtc,
        string profileKey = "test-profile",
        string serializedSessionJson = "{}") => new()
        {
            ProfileKey = profileKey,
            SerializedSessionJson = serializedSessionJson,
            UpdatedAtUtc = updatedAtUtc,
        };
}

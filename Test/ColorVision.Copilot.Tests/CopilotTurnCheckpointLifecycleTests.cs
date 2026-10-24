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
    public void ReducerRejectsLaterCheckpointWhoseJournalRegresses()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var regressingJournal = journal.Snapshot();
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(
                CreateCheckpoint(InitialUpdate, taskEventJournal: journal.Snapshot()),
                CreateTaskLedger()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.CheckpointUpdated(
                    CreateCheckpoint(
                        InitialUpdate.AddSeconds(1),
                        taskEventJournal: regressingJournal),
                    CreateTaskLedger())));

        Assert.Contains("monotonically", exception.Message, StringComparison.Ordinal);
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
        string serializedSessionJson = "{}",
        CopilotAgentTaskEventJournalSnapshot? taskEventJournal = null,
        string projectInstructionSurfaceFingerprint = "",
        string taskIntentText = "") => new()
        {
            ProfileKey = profileKey,
            SerializedSessionJson = serializedSessionJson,
            ProjectInstructionSurfaceVersion = projectInstructionSurfaceFingerprint.Length == 0
                ? 0
                : CopilotAgentSessionCheckpoint.CurrentProjectInstructionSurfaceVersion,
            ProjectInstructionSurfaceFingerprint = projectInstructionSurfaceFingerprint,
            TaskIntentText = taskIntentText,
            TaskEventJournal = taskEventJournal ?? new CopilotAgentTaskEventJournalSnapshot(),
            UpdatedAtUtc = updatedAtUtc,
        };
}

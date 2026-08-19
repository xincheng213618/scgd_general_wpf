using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnCheckpointLifecycleTests
{
    [Fact]
    public void CheckpointEventOwnsTheSessionCheckpointCollections()
    {
        var toolNames = new List<string> { "FirstTool" };
        var checkpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = "{}",
            ToolSurfaceVersion = CopilotAgentSessionCheckpoint.CurrentToolSurfaceVersion,
            AvailableToolNames = toolNames,
            TaskEventJournal = new CopilotAgentTaskEventJournalSnapshot(),
        };

        var agentEvent = CopilotAgentEvent.CheckpointUpdated(
            checkpoint,
            CreateTaskLedger());
        toolNames[0] = "RewrittenTool";

        var captured = Assert.IsType<CopilotAgentSessionCheckpoint>(
            agentEvent.SessionCheckpoint);
        Assert.NotSame(checkpoint, captured);
        Assert.Equal("FirstTool", Assert.Single(captured.AvailableToolNames));
        var capturedToolNames = Assert.IsAssignableFrom<IList<string>>(
            captured.AvailableToolNames);
        Assert.Throws<NotSupportedException>(() =>
            capturedToolNames[0] = "RewrittenTool");
    }

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
    public void ReducerRejectsProjectInstructionIdentityDrift()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(
                CreateCheckpoint(
                    InitialUpdate,
                    projectInstructionSurfaceFingerprint: new string('a', 64)),
                CreateTaskLedger()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.CheckpointUpdated(
                    CreateCheckpoint(
                        InitialUpdate.AddSeconds(1),
                        projectInstructionSurfaceFingerprint: new string('b', 64)),
                    CreateTaskLedger())));

        Assert.Contains("identity changed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsTaskIntentIdentityDrift()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(
                CreateCheckpoint(InitialUpdate, taskIntentText: "Inspect the active flow."),
                CreateTaskLedger()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.CheckpointUpdated(
                    CreateCheckpoint(
                        InitialUpdate.AddSeconds(1),
                        taskIntentText: "Change the active flow."),
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

    [Fact]
    public void ReducerRejectsFinalCheckpointWhoseJournalRegresses()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var regressingJournal = journal.Snapshot();
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var ledger = CreateTaskLedger();
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(
                CreateCheckpoint(InitialUpdate, taskEventJournal: journal.Snapshot()),
                ledger));
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
                    taskEventJournal: regressingJournal),
            });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(state, new CopilotTurnCompletedEvent(result)));

        Assert.Contains("monotonically", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerAcceptsForwardJournalWindowAfterCapacityEviction()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var ledger = CreateTaskLedger();
        for (var index = 0; index < CopilotAgentTaskEventJournal.MaxEvents - 1; index++)
            journal.RecordTaskLedger(ledger, $"checkpoint-{index}");
        var fullWindow = journal.Snapshot();
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(
                CreateCheckpoint(InitialUpdate, taskEventJournal: fullWindow),
                ledger));

        journal.RecordTaskLedger(ledger, "checkpoint-after-capacity");
        state = Observe(
            state,
            CopilotAgentEvent.CheckpointUpdated(
                CreateCheckpoint(
                    InitialUpdate.AddSeconds(1),
                    taskEventJournal: journal.Snapshot()),
                ledger));

        Assert.Equal(CopilotAgentTaskEventJournal.MaxEvents, state.CheckpointLifecycle.LatestCheckpoint!.TaskEventJournal.Events.Count);
        Assert.DoesNotContain(
            state.CheckpointLifecycle.LatestCheckpoint.TaskEventJournal.Events,
            item => item.Sequence == 2);
    }

    [Fact]
    public void ReducerRejectsRewrittenRetainedEventAfterCapacityEviction()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var ledger = CreateTaskLedger();
        for (var index = 0; index < CopilotAgentTaskEventJournal.MaxEvents - 1; index++)
            journal.RecordTaskLedger(ledger, $"checkpoint-{index}");
        var fullWindow = journal.Snapshot();
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.CheckpointUpdated(
                CreateCheckpoint(InitialUpdate, taskEventJournal: fullWindow),
                ledger));

        journal.RecordTaskLedger(ledger, "checkpoint-after-capacity");
        var forwardWindow = journal.Snapshot();
        var retainedSequence = forwardWindow.Events
            .Select(item => item.Sequence)
            .Intersect(fullWindow.Events.Select(item => item.Sequence))
            .First();
        var rewrittenWindow = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = forwardWindow.Events
                .Select(item => item.Sequence == retainedSequence
                    ? CopyWithSummary(item, "Rewritten historical evidence.")
                    : item)
                .ToArray(),
        };

        Assert.True(rewrittenWindow.IsStructurallyValid());
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.CheckpointUpdated(
                    CreateCheckpoint(
                        InitialUpdate.AddSeconds(1),
                        taskEventJournal: rewrittenWindow),
                    ledger)));

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

    private static CopilotAgentTaskEvent CopyWithSummary(
        CopilotAgentTaskEvent source,
        string summary) => new()
        {
            Sequence = source.Sequence,
            Id = source.Id,
            Type = source.Type,
            OccurredAtUtc = source.OccurredAtUtc,
            RunId = source.RunId,
            SubjectId = source.SubjectId,
            RelatedIds = source.RelatedIds,
            ToolName = source.ToolName,
            State = source.State,
            FailureCode = source.FailureCode,
            ExitCode = source.ExitCode,
            Summary = summary,
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

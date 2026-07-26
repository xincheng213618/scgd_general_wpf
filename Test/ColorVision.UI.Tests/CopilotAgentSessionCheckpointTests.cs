using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentSessionCheckpointTests
{
    [Fact]
    public void StructurallyValidCheckpointAcceptsCurrentEmptyTaskEventJournal()
    {
        var checkpoint = CreateCheckpoint(new CopilotAgentTaskEventJournalSnapshot());

        Assert.True(checkpoint.IsStructurallyValid());
    }

    [Fact]
    public void StructurallyValidCheckpointRejectsUnknownTaskEventJournalSchema()
    {
        var checkpoint = CreateCheckpoint(new CopilotAgentTaskEventJournalSnapshot
        {
            SchemaVersion = CopilotAgentTaskEventJournalSnapshot.CurrentSchemaVersion + 1,
        });

        Assert.False(checkpoint.IsStructurallyValid());
    }

    [Fact]
    public void StructurallyValidCheckpointRejectsOutOfOrderTaskEvents()
    {
        var runId = CopilotAgentTaskEventIds.CreateRunId();
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var later = CreateEvent(2, runId, occurredAtUtc.AddSeconds(1));
        var earlier = CreateEvent(1, runId, occurredAtUtc);
        var checkpoint = CreateCheckpoint(new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [later, earlier],
        });

        Assert.True(later.IsStructurallyValid());
        Assert.True(earlier.IsStructurallyValid());
        Assert.False(checkpoint.IsStructurallyValid());
    }

    [Fact]
    public void CopyWithTaskEventJournalRejectsInvalidJournal()
    {
        var checkpoint = CreateCheckpoint(new CopilotAgentTaskEventJournalSnapshot());
        var invalidJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            SchemaVersion = CopilotAgentTaskEventJournalSnapshot.CurrentSchemaVersion + 1,
        };

        Assert.Null(checkpoint.CopyWithTaskEventJournal(invalidJournal));
    }

    [Fact]
    public void CopyWithTaskEventJournalPreservesRecoveryIntentAndEnvironment()
    {
        var checkpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = "{}",
            TaskIntentText = "Inspect the current workspace",
            EnvironmentVersion = CopilotAgentSessionCheckpoint.CurrentEnvironmentVersion,
            EnvironmentFingerprint = new string('a', 64),
            TaskEventJournal = new CopilotAgentTaskEventJournalSnapshot(),
        };

        var copy = checkpoint.CopyWithTaskEventJournal(new CopilotAgentTaskEventJournalSnapshot());

        Assert.NotNull(copy);
        Assert.Equal(checkpoint.TaskIntentText, copy.TaskIntentText);
        Assert.Equal(checkpoint.EnvironmentVersion, copy.EnvironmentVersion);
        Assert.Equal(checkpoint.EnvironmentFingerprint, copy.EnvironmentFingerprint);
    }

    [Fact]
    public void ConversationNormalizationDropsCheckpointWithInvalidJournal()
    {
        var conversation = new CopilotConversationRecord
        {
            AgentSessionCheckpoint = CreateCheckpoint(new CopilotAgentTaskEventJournalSnapshot
            {
                SchemaVersion = CopilotAgentTaskEventJournalSnapshot.CurrentSchemaVersion + 1,
            }),
        };

        Assert.True(conversation.EnsureValid());
        Assert.Null(conversation.AgentSessionCheckpoint);
    }

    private static CopilotAgentSessionCheckpoint CreateCheckpoint(
        CopilotAgentTaskEventJournalSnapshot taskEventJournal)
    {
        return new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = "{}",
            TaskEventJournal = taskEventJournal,
        };
    }

    private static CopilotAgentTaskEvent CreateEvent(
        long sequence,
        string runId,
        DateTimeOffset occurredAtUtc)
    {
        return new CopilotAgentTaskEvent
        {
            Sequence = sequence,
            Id = CopilotAgentTaskEventIds.CreateEventId(
                sequence,
                runId,
                CopilotAgentTaskEventType.RunStarted,
                occurredAtUtc),
            Type = CopilotAgentTaskEventType.RunStarted,
            OccurredAtUtc = occurredAtUtc,
            RunId = runId,
            SubjectId = runId,
        };
    }
}

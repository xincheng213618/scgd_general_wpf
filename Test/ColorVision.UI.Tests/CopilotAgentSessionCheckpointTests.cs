using ColorVision.Copilot;
using Newtonsoft.Json;

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

    [Fact]
    public void NewtonsoftPersistenceCompressesLargeSessionAndRoundTrips()
    {
        var sessionJson = "{\"content\":\"" + new string('x', 32_000) + "\"}";
        var checkpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = sessionJson,
        };

        var serialized = JsonConvert.SerializeObject(checkpoint);
        var restored = JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(serialized);

        Assert.Contains(CopilotAgentSessionCheckpoint.CompressedSerializedSessionPrefix, serialized, StringComparison.Ordinal);
        Assert.True(serialized.Length < sessionJson.Length / 2);
        Assert.NotNull(restored);
        Assert.Equal(sessionJson, restored.SerializedSessionJson);
    }

    [Fact]
    public void NewtonsoftPersistenceLoadsLegacyUncompressedSession()
    {
        const string sessionJson = "{\"legacy\":true}";
        var serialized = "{\"SerializedSessionJson\":" + JsonConvert.SerializeObject(sessionJson) + "}";

        var restored = JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(serialized);

        Assert.NotNull(restored);
        Assert.Equal(sessionJson, restored.SerializedSessionJson);
    }

    [Fact]
    public void NewtonsoftPersistenceMigratesLargeLegacySessionOnNextSave()
    {
        var sessionJson = "{\"legacy\":\"" + new string('x', 32_000) + "\"}";
        var legacyDocument = "{\"SerializedSessionJson\":" + JsonConvert.SerializeObject(sessionJson) + "}";

        var restored = JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(legacyDocument);
        var migratedDocument = JsonConvert.SerializeObject(restored);

        Assert.NotNull(restored);
        Assert.Equal(sessionJson, restored.SerializedSessionJson);
        Assert.Contains(CopilotAgentSessionCheckpoint.CompressedSerializedSessionPrefix, migratedDocument, StringComparison.Ordinal);
        Assert.True(migratedDocument.Length < legacyDocument.Length / 2);
    }

    [Fact]
    public void MalformedCompressedSessionRemainsRecoverableForValidation()
    {
        var malformedPayload = CopilotAgentSessionCheckpoint.CompressedSerializedSessionPrefix + "not-base64";
        var serialized = "{\"SerializedSessionJson\":" + JsonConvert.SerializeObject(malformedPayload) + "}";

        var restored = JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(serialized);

        Assert.NotNull(restored);
        Assert.Equal(malformedPayload, restored.SerializedSessionJson);
        Assert.False(restored.IsStructurallyValid());
    }

    [Fact]
    public void SystemTextJsonPersistenceKeepsTheRuntimeSessionContract()
    {
        var sessionJson = "{\"content\":\"" + new string('x', 32_000) + "\"}";
        var checkpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = sessionJson,
        };

        var serialized = System.Text.Json.JsonSerializer.Serialize(checkpoint);
        using var document = System.Text.Json.JsonDocument.Parse(serialized);

        Assert.Equal(
            sessionJson,
            document.RootElement.GetProperty(nameof(CopilotAgentSessionCheckpoint.SerializedSessionJson)).GetString());
        Assert.DoesNotContain(CopilotAgentSessionCheckpoint.CompressedSerializedSessionPrefix, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void PersistedTextCodecRejectsContentBeyondTheDecodedLimit()
    {
        const string prefix = "test-gzip:";
        var decoded = new string('z', 4_097);
        var payload = CopilotPersistedTextCodec.Encode(
            decoded,
            prefix,
            minimumCompressionCharacters: 1,
            maximumDecodedCharacters: decoded.Length);

        var rejected = CopilotPersistedTextCodec.Decode(
            payload,
            prefix,
            maximumDecodedCharacters: decoded.Length - 1);

        Assert.StartsWith(prefix, payload, StringComparison.Ordinal);
        Assert.Equal(payload, rejected);
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

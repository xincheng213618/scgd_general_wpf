using ColorVision.Copilot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Copilot.Tests;

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
    public void StructurallyValidCheckpointRejectsAssistantSteeringProvenance()
    {
        var checkpoint = CreateCheckpoint(
            new CopilotAgentTaskEventJournalSnapshot(),
            [
                new CopilotRequestMessage("assistant", "invalid provenance")
                {
                    IsSteering = true,
                },
            ]);

        Assert.False(checkpoint.IsStructurallyValid());
    }

    [Fact]
    public void CreateFreezesPersistedTaskEventJournal()
    {
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var originalRunId = CopilotAgentTaskEventIds.CreateRunId();
        var originalEvent = CreateEvent(1, originalRunId, occurredAtUtc);
        var sourceEvents = new[] { originalEvent };
        var source = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = sourceEvents,
        };

        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            CreateOpenAiProfile(CopilotVendorType.OpenAI, "https://example.test/v1"),
            "{}",
            CopilotCapabilityCatalog.Shared.GetSnapshot(),
            taskEventJournal: source);

        Assert.NotNull(checkpoint);
        Assert.NotSame(source, checkpoint.TaskEventJournal);
        Assert.NotSame(originalEvent, Assert.Single(checkpoint.TaskEventJournal.Events));
        sourceEvents[0] = CreateEvent(
            1,
            CopilotAgentTaskEventIds.CreateRunId(),
            occurredAtUtc.AddSeconds(1));
        Assert.Equal(originalRunId, Assert.Single(checkpoint.TaskEventJournal.Events).RunId);
        var persistedEvents = Assert.IsAssignableFrom<IList<CopilotAgentTaskEvent>>(
            checkpoint.TaskEventJournal.Events);
        Assert.Throws<NotSupportedException>(() => persistedEvents[0] = sourceEvents[0]);
    }

    [Fact]
    public void PersistedCheckpointWithInvalidEvidenceArtifactIsRejected()
    {
        var checkpoint = JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(
            """
            {
              "ProfileKey": "test-profile",
              "SerializedSessionJson": "{}",
              "EvidenceArtifacts": [{}]
            }
            """)!;

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
            HookSurfaceVersion = CopilotAgentSessionCheckpoint.CurrentHookSurfaceVersion,
            HookSurfaceFingerprint = new string('b', 64),
            ProjectInstructionSurfaceVersion = CopilotAgentSessionCheckpoint.CurrentProjectInstructionSurfaceVersion,
            ProjectInstructionSurfaceFingerprint = new string('d', 64),
            TaskEventJournal = new CopilotAgentTaskEventJournalSnapshot(),
        };

        var copy = checkpoint.CopyWithTaskEventJournal(new CopilotAgentTaskEventJournalSnapshot());

        Assert.NotNull(copy);
        Assert.Equal(checkpoint.TaskIntentText, copy.TaskIntentText);
        Assert.Equal(checkpoint.EnvironmentVersion, copy.EnvironmentVersion);
        Assert.Equal(checkpoint.EnvironmentFingerprint, copy.EnvironmentFingerprint);
        Assert.Equal(checkpoint.HookSurfaceVersion, copy.HookSurfaceVersion);
        Assert.Equal(checkpoint.HookSurfaceFingerprint, copy.HookSurfaceFingerprint);
        Assert.Equal(checkpoint.ProjectInstructionSurfaceVersion, copy.ProjectInstructionSurfaceVersion);
        Assert.Equal(checkpoint.ProjectInstructionSurfaceFingerprint, copy.ProjectInstructionSurfaceFingerprint);
    }

    [Fact]
    public void CopyWithOutcomePreservesHookSurfaceWhileRefreshingConversationMemory()
    {
        var checkpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = "{}",
            HookSurfaceVersion = CopilotAgentSessionCheckpoint.CurrentHookSurfaceVersion,
            HookSurfaceFingerprint = new string('c', 64),
            ConversationMemory = [new CopilotRequestMessage("user", "old evidence")],
            TaskEventJournal = new CopilotAgentTaskEventJournalSnapshot(),
        };
        var refreshedMemory = new[]
        {
            new CopilotRequestMessage("user", "original task"),
            new CopilotRequestMessage("assistant", "partial final answer"),
        };

        var copy = checkpoint.CopyWithOutcome(
            new CopilotAgentTaskEventJournalSnapshot(),
            refreshedMemory);

        Assert.NotNull(copy);
        Assert.Equal(checkpoint.HookSurfaceVersion, copy.HookSurfaceVersion);
        Assert.Equal(checkpoint.HookSurfaceFingerprint, copy.HookSurfaceFingerprint);
        Assert.Equal(refreshedMemory, copy.ConversationMemory);
        Assert.NotSame(refreshedMemory, copy.ConversationMemory);
    }

    [Fact]
    public void HookSurfaceDriftRequiresAReplan()
    {
        var profile = CreateOpenAiProfile(
            CopilotVendorType.OpenAI,
            "https://example.test/v1");
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var registry = new CopilotToolExecutionHookRegistry();
        var executor = new CopilotToolExecutor(registry);
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            hookSurfaceSnapshot: executor.GetHookSurfaceSnapshot());
        using var registration = registry.Register(
            "test:checkpoint-drift",
            new NoOpHook(),
            "^CheckpointProbe$");

        var compatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            hookSurfaceSnapshot: executor.GetHookSurfaceSnapshot());

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.HookSurfaceDrift, compatibility.Kind);
        Assert.True(compatibility.RequiresReplan);
        Assert.False(compatibility.CanResume);
    }

    [Fact]
    public void LegacyCheckpointWithoutHookSurfaceRequiresAReplan()
    {
        var profile = CreateOpenAiProfile(
            CopilotVendorType.OpenAI,
            "https://example.test/v1");
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot);

        var compatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            hookSurfaceSnapshot: CopilotToolExecutor.GetSharedHookSurfaceSnapshot());

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.HookSurfaceSnapshotMissing, compatibility.Kind);
        Assert.True(compatibility.RequiresReplan);
    }

    [Fact]
    public void MatchingProjectInstructionsCanResume()
    {
        var profile = CreateOpenAiProfile(
            CopilotVendorType.OpenAI,
            "https://example.test/v1");
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var projectInstructions = CreateProjectInstructions("Keep the runtime contract stable.");
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            projectInstructions: projectInstructions);

        var compatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            projectInstructions: projectInstructions);

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.Compatible, compatibility.Kind);
        Assert.True(compatibility.CanResume);
    }

    [Fact]
    public void ProjectInstructionDriftRequiresAReplan()
    {
        var profile = CreateOpenAiProfile(
            CopilotVendorType.OpenAI,
            "https://example.test/v1");
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            projectInstructions: CreateProjectInstructions("Use the original workflow."));

        var compatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            projectInstructions: CreateProjectInstructions("Use the revised workflow."));

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.ProjectInstructionDrift, compatibility.Kind);
        Assert.True(compatibility.RequiresReplan);
        Assert.False(compatibility.CanResume);
    }

    [Fact]
    public void ConfiguredDeveloperInstructionDriftRequiresAReplan()
    {
        var profile = CreateOpenAiProfile(
            CopilotVendorType.OpenAI,
            "https://example.test/v1");
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var projectInstructions = CreateProjectInstructions("Keep the repository workflow stable.");
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            projectInstructions: projectInstructions,
            configuredDeveloperInstructions: "Use the original project persona.");

        var compatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            projectInstructions: projectInstructions,
            configuredDeveloperInstructions: "Use the revised project persona.");

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.ProjectInstructionDrift, compatibility.Kind);
        Assert.True(compatibility.RequiresReplan);
        Assert.False(compatibility.CanResume);
    }

    [Fact]
    public void ConfiguredDeveloperInstructionFingerprintUsesTheEffectiveTrimmedValue()
    {
        var profile = CreateOpenAiProfile(
            CopilotVendorType.OpenAI,
            "https://example.test/v1");
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var projectInstructions = CreateProjectInstructions("Keep the repository workflow stable.");
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            projectInstructions: projectInstructions,
            configuredDeveloperInstructions: "  Use the configured project persona.  ");

        var compatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            projectInstructions: projectInstructions,
            configuredDeveloperInstructions: "Use the configured project persona.");

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.Compatible, compatibility.Kind);
        Assert.True(compatibility.CanResume);
    }

    [Fact]
    public void LegacyCheckpointWithoutProjectInstructionSnapshotRequiresAReplan()
    {
        var profile = CreateOpenAiProfile(
            CopilotVendorType.OpenAI,
            "https://example.test/v1");
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot);

        var compatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            projectInstructions: Array.Empty<CopilotProjectInstructionDocument>());

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.ProjectInstructionSnapshotMissing, compatibility.Kind);
        Assert.True(compatibility.RequiresReplan);
    }

    [Fact]
    public void PreviousProjectInstructionSurfaceVersionRequiresAReplan()
    {
        var profile = CreateOpenAiProfile(
            CopilotVendorType.OpenAI,
            "https://example.test/v1");
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var projectInstructions = CreateProjectInstructions("Keep the repository workflow stable.");
        var currentCheckpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            projectInstructions: projectInstructions,
            configuredDeveloperInstructions: "Use the configured project persona.")!;
        var persisted = JObject.Parse(JsonConvert.SerializeObject(currentCheckpoint));
        persisted[nameof(CopilotAgentSessionCheckpoint.ProjectInstructionSurfaceVersion)] =
            CopilotAgentSessionCheckpoint.CurrentProjectInstructionSurfaceVersion - 1;
        var previousCheckpoint = persisted.ToObject<CopilotAgentSessionCheckpoint>()!;

        var compatibility = previousCheckpoint.EvaluateFor(
            profile,
            capabilitySnapshot,
            projectInstructions: projectInstructions,
            configuredDeveloperInstructions: "Use the configured project persona.");

        Assert.True(previousCheckpoint.IsStructurallyValid());
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.ProjectInstructionSnapshotMissing, compatibility.Kind);
        Assert.True(compatibility.RequiresReplan);
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
            HookSurfaceVersion = CopilotAgentSessionCheckpoint.CurrentHookSurfaceVersion,
            HookSurfaceFingerprint = new string('c', 64),
            ProjectInstructionSurfaceVersion = CopilotAgentSessionCheckpoint.CurrentProjectInstructionSurfaceVersion,
            ProjectInstructionSurfaceFingerprint = new string('d', 64),
        };

        var serialized = JsonConvert.SerializeObject(checkpoint);
        var restored = JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(serialized);

        Assert.Contains(CopilotAgentSessionCheckpoint.CompressedSerializedSessionPrefix, serialized, StringComparison.Ordinal);
        Assert.True(serialized.Length < sessionJson.Length / 2);
        Assert.NotNull(restored);
        Assert.Equal(sessionJson, restored.SerializedSessionJson);
        Assert.Equal(checkpoint.HookSurfaceVersion, restored.HookSurfaceVersion);
        Assert.Equal(checkpoint.HookSurfaceFingerprint, restored.HookSurfaceFingerprint);
        Assert.Equal(checkpoint.ProjectInstructionSurfaceVersion, restored.ProjectInstructionSurfaceVersion);
        Assert.Equal(checkpoint.ProjectInstructionSurfaceFingerprint, restored.ProjectInstructionSurfaceFingerprint);
    }

    [Fact]
    public void NewtonsoftPersistenceRoundTripsSteeringProvenanceWithoutChangingLegacyMessages()
    {
        var checkpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = "{}",
            ConversationMemory =
            [
                new CopilotRequestMessage("user", "ordinary request"),
                new CopilotRequestMessage("user", "mid-turn direction")
                {
                    IsSteering = true,
                },
            ],
        };

        var serialized = JsonConvert.SerializeObject(checkpoint);
        var restored = JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(serialized);

        Assert.DoesNotContain("\"IsSteering\":false", serialized, StringComparison.Ordinal);
        Assert.Contains("\"IsSteering\":true", serialized, StringComparison.Ordinal);
        Assert.NotNull(restored);
        Assert.False(restored.ConversationMemory[0].IsSteering);
        Assert.True(restored.ConversationMemory[1].IsSteering);
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

    [Fact]
    public void OfficialOpenAiResponsesMigrationRetiresLegacyAndPreviousTransportCheckpointKeys()
    {
        var officialProfile = CreateOpenAiProfile(
            CopilotVendorType.OpenAI,
            "https://api.openai.com/v1");
        var proxyProfile = CreateOpenAiProfile(
            CopilotVendorType.OpenAI,
            "https://example.test/v1");

        Assert.NotEqual(
            CreateLegacyProfileKey(officialProfile),
            CopilotAgentSessionCheckpoint.CreateProfileKey(officialProfile));
        Assert.NotEqual(
            CreateProfileKey(
                officialProfile,
                "openai-responses-stateless-v1"),
            CopilotAgentSessionCheckpoint.CreateProfileKey(officialProfile));
        Assert.Equal(
            CreateLegacyProfileKey(proxyProfile),
            CopilotAgentSessionCheckpoint.CreateProfileKey(proxyProfile));
    }

    [Fact]
    public void ConversationCheckpointOwnsEquivalentCurrentJournalWithoutPersistingDuplicate()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var checkpoint = CreateCheckpoint(journal.Snapshot());
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");

        Assert.True(conversation.SetAgentSessionCheckpoint(checkpoint));

        var serialized = JsonConvert.SerializeObject(conversation);
        var document = JObject.Parse(serialized);
        Assert.Same(checkpoint.TaskEventJournal, conversation.CurrentAgentTaskEventJournal);
        Assert.Null(conversation.LatestAgentTaskEventJournal);
        Assert.Null(document[nameof(CopilotConversationRecord.LatestAgentTaskEventJournal)]);
    }

    [Fact]
    public void CheckpointWithRewrittenEventPayloadCannotReplaceCurrentEvidence()
    {
        var runId = CopilotAgentTaskEventIds.CreateRunId();
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var originalEvent = CreateEvent(1, runId, occurredAtUtc);
        var rewrittenEvent = new CopilotAgentTaskEvent
        {
            Sequence = originalEvent.Sequence,
            Id = originalEvent.Id,
            Type = originalEvent.Type,
            OccurredAtUtc = originalEvent.OccurredAtUtc,
            RunId = originalEvent.RunId,
            SubjectId = originalEvent.SubjectId,
            Summary = "Rewritten persisted evidence.",
        };
        var originalCheckpoint = CreateCheckpoint(new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [originalEvent],
        });
        var rewrittenCheckpoint = CreateCheckpoint(new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [rewrittenEvent],
        });
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.SetAgentSessionCheckpoint(originalCheckpoint);

        Assert.False(conversation.SetAgentSessionCheckpoint(rewrittenCheckpoint));

        Assert.Same(originalCheckpoint, conversation.AgentSessionCheckpoint);
        Assert.Same(originalCheckpoint.TaskEventJournal, conversation.CurrentAgentTaskEventJournal);
    }

    [Fact]
    public void TrySetCheckpointDistinguishesEquivalentAcceptanceFromRegressionRejection()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var regressingCheckpoint = CreateCheckpoint(journal.Snapshot());
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var currentCheckpoint = CreateCheckpoint(journal.Snapshot());
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.SetAgentSessionCheckpoint(currentCheckpoint);

        Assert.True(conversation.TrySetAgentSessionCheckpoint(currentCheckpoint, out var equivalentChanged));
        Assert.False(equivalentChanged);
        Assert.False(conversation.TrySetAgentSessionCheckpoint(regressingCheckpoint, out var rejectedChanged));
        Assert.False(rejectedChanged);
        Assert.Same(currentCheckpoint, conversation.AgentSessionCheckpoint);
    }

    [Fact]
    public void ClearingCheckpointRetainsAndPersistsLatestJournalEvidence()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.SetAgentSessionCheckpoint(CreateCheckpoint(journal.Snapshot()));

        Assert.True(conversation.SetAgentSessionCheckpoint(null));

        var serialized = JsonConvert.SerializeObject(conversation);
        var document = JObject.Parse(serialized);
        Assert.Null(conversation.AgentSessionCheckpoint);
        Assert.Same(
            conversation.LatestAgentTaskEventJournal,
            conversation.CurrentAgentTaskEventJournal);
        Assert.NotNull(document[nameof(CopilotConversationRecord.LatestAgentTaskEventJournal)]);
    }

    [Fact]
    public void ValidationCollapsesLegacyDuplicateCheckpointJournal()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var snapshot = journal.Snapshot();
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.AgentSessionCheckpoint = CreateCheckpoint(snapshot);
        conversation.LatestAgentTaskEventJournal = snapshot;

        Assert.True(conversation.EnsureValid());

        Assert.Null(conversation.LatestAgentTaskEventJournal);
        Assert.Same(snapshot, conversation.CurrentAgentTaskEventJournal);
    }

    [Fact]
    public void ValidationMigratesNewerLegacyJournalIntoCheckpointWithoutRefreshingTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var checkpointUpdatedAtUtc = now.AddDays(-1);
        var checkpointJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [CreateEvent(1, CopilotAgentTaskEventIds.CreateRunId(), now.AddMinutes(-1))],
        };
        var newerJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [CreateEvent(1, CopilotAgentTaskEventIds.CreateRunId(), now)],
        };
        var originalCheckpoint = CreateCheckpoint(
            checkpointJournal,
            updatedAtUtc: checkpointUpdatedAtUtc);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.AgentSessionCheckpoint = originalCheckpoint;
        conversation.LatestAgentTaskEventJournal = newerJournal;

        Assert.True(conversation.EnsureValid());

        Assert.NotSame(originalCheckpoint, conversation.AgentSessionCheckpoint);
        Assert.Same(newerJournal, conversation.AgentSessionCheckpoint!.TaskEventJournal);
        Assert.Equal(checkpointUpdatedAtUtc, conversation.AgentSessionCheckpoint.UpdatedAtUtc);
        Assert.Null(conversation.LatestAgentTaskEventJournal);
        Assert.Same(newerJournal, conversation.CurrentAgentTaskEventJournal);
    }

    [Fact]
    public void CommitAgentRunStateRebasesLaggingCheckpointOntoSingleJournalOwner()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var checkpointJournal = journal.Snapshot();
        journal.RecordStop(CopilotAgentStopReason.Completed);
        var finalJournal = journal.Snapshot();
        var checkpoint = CreateCheckpoint(checkpointJournal);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");

        Assert.True(conversation.CommitAgentRunState(finalJournal, checkpoint));

        Assert.NotSame(checkpoint, conversation.AgentSessionCheckpoint);
        Assert.Same(finalJournal, conversation.AgentSessionCheckpoint!.TaskEventJournal);
        Assert.Same(finalJournal, conversation.CurrentAgentTaskEventJournal);
        Assert.Null(conversation.LatestAgentTaskEventJournal);
        Assert.False(conversation.ShouldSerializeLatestAgentTaskEventJournal());
    }

    [Fact]
    public void ContinuationCheckpointUsesPersistedSingleJournalOwner()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var checkpointJournal = journal.Snapshot();
        journal.RecordStop(CopilotAgentStopReason.Completed);
        var terminalJournal = journal.Snapshot();
        var persistedCheckpoint = CreateCheckpoint(checkpointJournal);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.CommitAgentRunState(terminalJournal, persistedCheckpoint);

        var continuationCheckpoint = conversation.CreateAgentContinuationCheckpoint();

        Assert.NotNull(continuationCheckpoint);
        Assert.NotSame(persistedCheckpoint, continuationCheckpoint);
        Assert.Same(conversation.AgentSessionCheckpoint, continuationCheckpoint);
        Assert.Same(terminalJournal, continuationCheckpoint.TaskEventJournal);
        Assert.Same(checkpointJournal, persistedCheckpoint.TaskEventJournal);
        Assert.Null(conversation.LatestAgentTaskEventJournal);
    }

    [Fact]
    public void CommitAgentRunStateRebasesTerminalJournalWhenCheckpointBelongsToDifferentRun()
    {
        var now = DateTimeOffset.UtcNow;
        var checkpointRunId = CopilotAgentTaskEventIds.CreateRunId();
        var terminalRunId = CopilotAgentTaskEventIds.CreateRunId();
        var checkpointJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [CreateEvent(1, checkpointRunId, now.AddMinutes(-1))],
        };
        var terminalJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [CreateEvent(1, terminalRunId, now)],
        };
        var checkpoint = CreateCheckpoint(checkpointJournal);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");

        Assert.True(conversation.CommitAgentRunState(terminalJournal, checkpoint));

        Assert.NotSame(checkpoint, conversation.AgentSessionCheckpoint);
        Assert.Same(terminalJournal, conversation.AgentSessionCheckpoint!.TaskEventJournal);
        Assert.Same(terminalJournal, conversation.CurrentAgentTaskEventJournal);
        Assert.Null(conversation.LatestAgentTaskEventJournal);
        Assert.False(conversation.ShouldSerializeLatestAgentTaskEventJournal());

        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(
            JsonConvert.SerializeObject(conversation));
        Assert.NotNull(restored);
        Assert.Null(restored.LatestAgentTaskEventJournal);
        Assert.Same(
            restored.AgentSessionCheckpoint!.TaskEventJournal,
            restored.CurrentAgentTaskEventJournal);
        Assert.Equal(
            terminalRunId,
            Assert.Single(restored.CurrentAgentTaskEventJournal!.Events).RunId);
    }

    [Fact]
    public void ClearingLaggingCheckpointDoesNotReplaceTerminalJournalEvidence()
    {
        var now = DateTimeOffset.UtcNow;
        var checkpointJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [CreateEvent(1, CopilotAgentTaskEventIds.CreateRunId(), now.AddMinutes(-1))],
        };
        var terminalJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [CreateEvent(1, CopilotAgentTaskEventIds.CreateRunId(), now)],
        };
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.CommitAgentRunState(
            terminalJournal,
            CreateCheckpoint(checkpointJournal));

        Assert.True(conversation.SetAgentSessionCheckpoint(null));

        Assert.Null(conversation.AgentSessionCheckpoint);
        Assert.Same(terminalJournal, conversation.LatestAgentTaskEventJournal);
        Assert.Same(terminalJournal, conversation.CurrentAgentTaskEventJournal);
    }

    [Fact]
    public void SettingLaggingCheckpointIsRejectedAgainstNewerIndependentEvidence()
    {
        var now = DateTimeOffset.UtcNow;
        var laggingJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [CreateEvent(1, CopilotAgentTaskEventIds.CreateRunId(), now.AddMinutes(-1))],
        };
        var newerJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [CreateEvent(1, CopilotAgentTaskEventIds.CreateRunId(), now)],
        };
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.CommitAgentRunState(newerJournal, CreateCheckpoint(laggingJournal));

        Assert.False(conversation.SetAgentSessionCheckpoint(CreateCheckpoint(laggingJournal)));

        Assert.Null(conversation.LatestAgentTaskEventJournal);
        Assert.Same(newerJournal, conversation.CurrentAgentTaskEventJournal);
    }

    [Fact]
    public void HigherSequenceDivergentCheckpointDoesNotReplaceLaterTerminalEvidence()
    {
        var now = DateTimeOffset.UtcNow;
        var runId = CopilotAgentTaskEventIds.CreateRunId();
        var sharedEvent = CreateEvent(1, runId, now.AddMinutes(-3));
        var checkpointJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [sharedEvent],
        };
        var terminalJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events =
            [
                sharedEvent,
                CreateEvent(2, runId, now),
            ],
        };
        var lateDivergentJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events =
            [
                sharedEvent,
                CreateEvent(2, runId, now.AddMinutes(-2)),
                CreateEvent(3, runId, now.AddMinutes(-1)),
            ],
        };
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.CommitAgentRunState(terminalJournal, CreateCheckpoint(checkpointJournal));

        Assert.False(conversation.SetAgentSessionCheckpoint(CreateCheckpoint(lateDivergentJournal)));

        Assert.Null(conversation.LatestAgentTaskEventJournal);
        Assert.Same(terminalJournal, conversation.CurrentAgentTaskEventJournal);
    }

    [Fact]
    public void SettingOlderCheckpointDoesNotRegressResumeState()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var olderCheckpoint = CreateCheckpoint(journal.Snapshot());
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var newerCheckpoint = CreateCheckpoint(journal.Snapshot());
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.SetAgentSessionCheckpoint(newerCheckpoint);

        Assert.False(conversation.SetAgentSessionCheckpoint(olderCheckpoint));

        Assert.Same(newerCheckpoint, conversation.AgentSessionCheckpoint);
        Assert.Same(newerCheckpoint.TaskEventJournal, conversation.CurrentAgentTaskEventJournal);
    }

    [Fact]
    public void CancelledConversationDoesNotResurrectLaggingCheckpoint()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var laggingCheckpoint = CreateCheckpoint(journal.Snapshot());
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.SetAgentSessionCheckpoint(laggingCheckpoint);
        conversation.CompleteOpenAgentRun(
            CopilotAgentStopReason.Cancelled,
            CopilotAgentControlIntent.Cancel);

        Assert.False(conversation.SetAgentSessionCheckpoint(laggingCheckpoint));

        Assert.Null(conversation.AgentSessionCheckpoint);
        Assert.NotNull(conversation.LatestAgentTaskEventJournal);
        Assert.Contains(
            conversation.LatestAgentTaskEventJournal!.Events,
            item => item.Type == CopilotAgentTaskEventType.RunStopped);
    }

    [Fact]
    public void LateOlderRunCommitDoesNotReplaceCurrentResumeState()
    {
        var now = DateTimeOffset.UtcNow;
        var olderRunId = CopilotAgentTaskEventIds.CreateRunId();
        var newerRunId = CopilotAgentTaskEventIds.CreateRunId();
        var olderJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [CreateEvent(1, olderRunId, now.AddMinutes(-1))],
        };
        var newerJournal = new CopilotAgentTaskEventJournalSnapshot
        {
            Events = [CreateEvent(1, newerRunId, now)],
        };
        var newerCheckpoint = CreateCheckpoint(newerJournal);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.CommitAgentRunState(newerJournal, newerCheckpoint);

        Assert.False(conversation.CommitAgentRunState(
            olderJournal,
            CreateCheckpoint(olderJournal)));

        Assert.Same(newerCheckpoint, conversation.AgentSessionCheckpoint);
        Assert.Same(newerJournal, conversation.CurrentAgentTaskEventJournal);
    }

    [Fact]
    public void TryCommitRunStateDistinguishesEquivalentAcceptanceFromStaleRejection()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var staleJournal = journal.Snapshot();
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var currentJournal = journal.Snapshot();
        var currentCheckpoint = CreateCheckpoint(currentJournal);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.CommitAgentRunState(currentJournal, currentCheckpoint);

        Assert.True(conversation.TryCommitAgentRunState(
            currentJournal,
            currentCheckpoint,
            out var equivalentChanged));
        Assert.False(equivalentChanged);
        Assert.False(conversation.TryCommitAgentRunState(
            staleJournal,
            CreateCheckpoint(staleJournal),
            out var rejectedChanged));
        Assert.False(rejectedChanged);
        Assert.Same(currentCheckpoint, conversation.AgentSessionCheckpoint);
    }

    [Fact]
    public void SettingForwardCheckpointRetiresOlderIndependentEvidence()
    {
        var firstRun = new CopilotAgentTaskEventJournalBuilder();
        firstRun.RecordRunStarted();
        var oldCheckpointJournal = firstRun.Snapshot();
        firstRun.RecordStop(CopilotAgentStopReason.Completed);
        var independentJournal = firstRun.Snapshot();
        var nextRun = new CopilotAgentTaskEventJournalBuilder(independentJournal);
        nextRun.RecordRunStarted();
        var newerCheckpointJournal = nextRun.Snapshot();
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.CommitAgentRunState(
            independentJournal,
            CreateCheckpoint(oldCheckpointJournal));
        var newerCheckpoint = CreateCheckpoint(newerCheckpointJournal);

        Assert.True(conversation.SetAgentSessionCheckpoint(newerCheckpoint));

        Assert.Same(newerCheckpoint, conversation.AgentSessionCheckpoint);
        Assert.Null(conversation.LatestAgentTaskEventJournal);
        Assert.Same(newerCheckpointJournal, conversation.CurrentAgentTaskEventJournal);
    }

    [Fact]
    public void EquivalentTerminalJournalRemainsOwnedByCheckpointOnly()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var snapshot = journal.Snapshot();
        var checkpoint = CreateCheckpoint(snapshot);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");

        Assert.True(conversation.CommitAgentRunState(snapshot, checkpoint));

        Assert.Same(checkpoint, conversation.AgentSessionCheckpoint);
        Assert.Null(conversation.LatestAgentTaskEventJournal);
        Assert.Same(snapshot, conversation.CurrentAgentTaskEventJournal);
    }

    private static CopilotAgentSessionCheckpoint CreateCheckpoint(
        CopilotAgentTaskEventJournalSnapshot taskEventJournal,
        IReadOnlyList<CopilotRequestMessage>? conversationMemory = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        return new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = "{}",
            TaskEventJournal = taskEventJournal,
            ConversationMemory = conversationMemory ?? [],
            UpdatedAtUtc = updatedAtUtc ?? default,
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
                CopilotAgentTaskEventType.TaskLedgerCaptured,
                occurredAtUtc),
            Type = CopilotAgentTaskEventType.TaskLedgerCaptured,
            OccurredAtUtc = occurredAtUtc,
            RunId = runId,
            SubjectId = runId,
            State = "test",
        };
    }

    private static IReadOnlyList<CopilotProjectInstructionDocument> CreateProjectInstructions(string content)
    {
        return
        [
            new CopilotProjectInstructionDocument
            {
                Path = @"C:\workspace\AGENTS.md",
                Content = content,
            },
        ];
    }

    private static CopilotProfileConfig CreateOpenAiProfile(
        CopilotVendorType vendorType,
        string baseUrl)
    {
        return new CopilotProfileConfig
        {
            Id = "test-profile",
            VendorType = vendorType,
            ProviderType = CopilotProviderType.OpenAICompatible,
            BaseUrl = baseUrl,
            Model = "gpt-5.5",
        };
    }

    private static string CreateLegacyProfileKey(CopilotProfileConfig profile)
    {
        return CreateProfileKey(profile, transportVersion: null);
    }

    private static string CreateProfileKey(
        CopilotProfileConfig profile,
        string? transportVersion)
    {
        var value = string.Join("|", new[]
        {
            profile.Id?.Trim() ?? string.Empty,
            profile.ProviderType.ToString(),
            profile.BaseUrl?.Trim().TrimEnd('/') ?? string.Empty,
            profile.Model?.Trim() ?? string.Empty,
            profile.EffectiveSystemPrompt,
        });
        if (!string.IsNullOrEmpty(transportVersion))
            value += "|" + transportVersion;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }

    private sealed class NoOpHook : ICopilotToolExecutionHook
    {
        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CopilotToolExecutionHookDecision.Proceed);
        }

        public Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}

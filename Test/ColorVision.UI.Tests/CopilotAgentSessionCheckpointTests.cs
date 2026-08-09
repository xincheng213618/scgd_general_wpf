using ColorVision.Copilot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

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

    private static CopilotAgentSessionCheckpoint CreateCheckpoint(
        CopilotAgentTaskEventJournalSnapshot taskEventJournal,
        IReadOnlyList<CopilotRequestMessage>? conversationMemory = null)
    {
        return new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = "{}",
            TaskEventJournal = taskEventJournal,
            ConversationMemory = conversationMemory ?? [],
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

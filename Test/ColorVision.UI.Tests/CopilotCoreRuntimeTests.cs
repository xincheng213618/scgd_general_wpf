using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ColorVision.UI;
using Microsoft.Extensions.AI;

namespace ColorVision.UI.Tests;

[Collection(CopilotSharedStateTestGroup.Name)]
public sealed class CopilotCoreRuntimeTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-Copilot-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void StateStore_UsesBackupWhenPrimaryStateIsCorrupt()
    {
        var store = new CopilotChatStateStore(_tempRoot);
        var state = new CopilotChatState();
        state.Conversations.Add(CopilotConversationRecord.CreateEmpty("profile", "Model"));
        store.Save(state);

        state.ActiveProfileId = "updated-profile";
        store.Save(state);
        File.WriteAllText(store.StateFilePath, "{ not valid json", Encoding.UTF8);

        var recovered = store.Load();

        Assert.Single(recovered.Conversations);
        Assert.Equal(CopilotChatState.CurrentSchemaVersion, recovered.SchemaVersion);
        Assert.True(File.Exists(store.BackupStateFilePath));
    }

    [Fact]
    public void StateStore_CleansOnlyUnreferencedManagedAttachments()
    {
        var store = new CopilotChatStateStore(_tempRoot);
        var state = new CopilotChatState();
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        state.Conversations.Add(conversation);
        store.Save(state);

        var referencedPath = Path.Combine(store.AttachmentDirectoryPath, "referenced.png");
        var orphanPath = Path.Combine(store.AttachmentDirectoryPath, "orphan.png");
        File.WriteAllText(referencedPath, "referenced");
        File.WriteAllText(orphanPath, "orphan");
        conversation.Attachments.Add(CopilotAttachmentItem.CreateImage(referencedPath, "referenced"));

        var deleted = store.CleanupOrphanedAttachments(state);

        Assert.Equal(1, deleted);
        Assert.True(File.Exists(referencedPath));
        Assert.False(File.Exists(orphanPath));
    }

    [Fact]
    public void StateStore_PersistsAgentFrameworkSessionCheckpoint()
    {
        var store = new CopilotChatStateStore(_tempRoot);
        var state = new CopilotChatState();
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var taskEventJournal = new CopilotAgentTaskEventJournalBuilder();
        taskEventJournal.RecordRunStarted();
        taskEventJournal.RecordStop(CopilotAgentStopReason.Completed);
        conversation.AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "profile-key",
            SerializedSessionJson = "{\"state\":{}}",
            CapabilityCatalogRevision = 3,
            Capabilities =
            [
                new CopilotAgentCheckpointCapability
                {
                    Id = "builtin:searchdocs",
                    Revision = 2,
                    Fingerprint = new string('a', 64),
                },
            ],
            EvidenceArtifacts =
            [
                new CopilotAgentEvidenceArtifact
                {
                    Id = "evidence:" + new string('b', 32),
                    CapabilityId = "builtin:searchdocs",
                    CapabilityFingerprint = new string('a', 64),
                    ToolName = "SearchDocs",
                    ResourceKey = "resource:0123456789abcdef",
                    Summary = "Documentation evidence collected.",
                    ContentFingerprint = new string('c', 64),
                    CapturedAtUtc = DateTimeOffset.UtcNow,
                },
            ],
            TaskEventJournal = taskEventJournal.Snapshot(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
        state.Conversations.Add(conversation);

        store.Save(state);
        var recovered = store.Load();

        var checkpoint = Assert.Single(recovered.Conversations).AgentSessionCheckpoint;
        Assert.NotNull(checkpoint);
        Assert.Equal("profile-key", checkpoint!.ProfileKey);
        Assert.Equal("{\"state\":{}}", checkpoint.SerializedSessionJson);
        Assert.Equal(3, checkpoint.CapabilityCatalogRevision);
        var capability = Assert.Single(checkpoint.Capabilities);
        Assert.Equal("builtin:searchdocs", capability.Id);
        Assert.Equal(2, capability.Revision);
        Assert.Equal(new string('a', 64), capability.Fingerprint);
        var evidence = Assert.Single(checkpoint.EvidenceArtifacts);
        Assert.Equal("builtin:searchdocs", evidence.CapabilityId);
        Assert.Equal("Documentation evidence collected.", evidence.Summary);
        Assert.Equal(2, checkpoint.TaskEventJournal.Events.Count);
        Assert.Equal(CopilotAgentTaskEventType.RunStopped, checkpoint.TaskEventJournal.Events[^1].Type);
    }

    [Fact]
    public void TaskEventJournal_IsBoundedRedactedCorrelatedAndCursorQueryable()
    {
        var builder = new CopilotAgentTaskEventJournalBuilder();
        builder.RecordRunStarted();
        var startedAt = DateTimeOffset.UtcNow;
        builder.Observe(CopilotAgentEvent.ToolStarted(new CopilotToolExecutionInfo
        {
            CallId = "provider-call-123",
            ToolName = "FetchUrl",
            State = CopilotToolExecutionState.Running,
            StartedAtUtc = startedAt,
        }));
        builder.Observe(CopilotAgentEvent.FromToolResult(new CopilotToolResult
        {
            ToolName = "FetchUrl",
            Success = true,
            Summary = "FetchUrl is waiting for approval.",
        }, new CopilotToolExecutionInfo
        {
            CallId = "provider-call-123",
            ToolName = "FetchUrl",
            ApprovalActionId = "approval-456",
            State = CopilotToolExecutionState.AwaitingApproval,
            StartedAtUtc = startedAt,
        }));
        builder.RecordApprovalDecision("FetchUrl", "provider-call-123", "approval-456", approved: true);
        builder.RecordTaskLedger(new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute",
            Items = [new CopilotAgentTaskItem { Id = 7, Title = "Secret task title", IsComplete = true }],
        }, "final");
        builder.Observe(CopilotAgentEvent.Error("api_key=secret-value runtime failed"));
        builder.RecordStop(CopilotAgentStopReason.Completed);

        var snapshot = builder.Snapshot();
        Assert.True(snapshot.IsStructurallyValid());
        Assert.DoesNotContain("secret-value", JsonSerializer.Serialize(snapshot), StringComparison.Ordinal);
        Assert.DoesNotContain("Secret task title", JsonSerializer.Serialize(snapshot), StringComparison.Ordinal);
        var callEvents = CopilotAgentTaskEventJournal.Query(snapshot, new CopilotAgentTaskEventQuery
        {
            SubjectOrRelatedId = CopilotAgentTaskEventIds.ForCall("provider-call-123"),
        });
        Assert.Contains(callEvents.Events, item => item.Type == CopilotAgentTaskEventType.ToolStarted);
        Assert.Contains(callEvents.Events, item => item.Type == CopilotAgentTaskEventType.ApprovalRequested);
        Assert.Contains(callEvents.Events, item => item.Type == CopilotAgentTaskEventType.ApprovalApproved);

        var boundedBuilder = new CopilotAgentTaskEventJournalBuilder();
        boundedBuilder.RecordRunStarted();
        for (var index = 0; index < CopilotAgentTaskEventJournal.MaxEvents + 20; index++)
            boundedBuilder.RecordSteering("steering-" + index);
        var bounded = boundedBuilder.Snapshot();
        Assert.Equal(CopilotAgentTaskEventJournal.MaxEvents, bounded.Events.Count);
        Assert.True(bounded.IsStructurallyValid());
        var firstPage = CopilotAgentTaskEventJournal.Query(bounded, new CopilotAgentTaskEventQuery { Limit = 10 });
        Assert.Equal(10, firstPage.Events.Count);
        Assert.True(firstPage.HasMore);
        Assert.NotNull(firstPage.NextBeforeSequence);
        var secondPage = CopilotAgentTaskEventJournal.Query(bounded, new CopilotAgentTaskEventQuery
        {
            BeforeSequence = firstPage.NextBeforeSequence!.Value,
            Limit = 10,
        });
        Assert.DoesNotContain(secondPage.Events, second => firstPage.Events.Any(first => first.Id == second.Id));

        var unknownSchema = new CopilotAgentTaskEventJournalSnapshot { SchemaVersion = 999 };
        Assert.Empty(CopilotAgentTaskEventJournal.Query(unknownSchema).Events);
        Assert.Null(CopilotAgentSessionCheckpoint.Create(CreateProfile(), "{\"state\":{}}", taskEventJournal: unknownSchema));
        var recoveredBuilder = new CopilotAgentTaskEventJournalBuilder(unknownSchema);
        recoveredBuilder.RecordRunStarted();
        Assert.Single(recoveredBuilder.Snapshot().Events);

        CopilotAgentTaskEventJournalRegistry.Clear();
        try
        {
            Assert.True(CopilotAgentTaskEventJournalRegistry.Publish("conversation-journal-test", snapshot));
            Assert.Equal("conversation-journal-test", CopilotAgentTaskEventJournalRegistry.Current?.ConversationId);
            Assert.False(CopilotAgentTaskEventJournalRegistry.Publish("conversation-invalid", unknownSchema));
            Assert.Equal("conversation-journal-test", CopilotAgentTaskEventJournalRegistry.Current?.ConversationId);
        }
        finally
        {
            CopilotAgentTaskEventJournalRegistry.Clear();
        }
    }

    [Fact]
    public void AgentRecoveryPolicy_OnlyOffersSafeCheckpointRecoveryModes()
    {
        var profile = CreateProfile();
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordStop(CopilotAgentStopReason.TaskPassLimit);
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{\"state\":{}}",
            capabilitySnapshot,
            taskEventJournal: journal.Snapshot());
        Assert.NotNull(checkpoint);

        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial result")
        {
            AgentStopReason = CopilotAgentStopReason.TaskPassLimit,
            AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items = [new CopilotAgentTaskItem { Id = 1, Title = "Read evidence", IsComplete = false }],
            },
        };

        var resume = CopilotAgentRecoveryPolicy.Evaluate(message, checkpoint, profile, capabilitySnapshot);
        Assert.True(resume.IsAvailable);
        Assert.Equal(CopilotAgentRecoveryMode.Resume, resume.Request!.Mode);

        message.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            CallId = "failed-read",
            ToolName = "FetchUrl",
            Access = CopilotToolAccess.ReadOnly,
            Idempotency = CopilotToolIdempotency.Idempotent,
            State = CopilotToolExecutionState.Failed,
            RetryEligible = true,
        });
        var retryRead = CopilotAgentRecoveryPolicy.Evaluate(message, checkpoint, profile, capabilitySnapshot);
        Assert.Equal(CopilotAgentRecoveryMode.RetryRead, retryRead.Request!.Mode);
        Assert.Equal("FetchUrl", retryRead.Request.ToolName);
        Assert.StartsWith("call:", retryRead.Request.SourceCallKey, StringComparison.Ordinal);
        Assert.Equal(37, retryRead.Request.SourceCallKey.Length);

        message.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            CallId = "failed-write",
            ToolName = "ApplyPatch",
            Access = CopilotToolAccess.Write,
            Idempotency = CopilotToolIdempotency.Idempotent,
            State = CopilotToolExecutionState.Failed,
            RetryEligible = true,
        });
        var writeExcluded = CopilotAgentRecoveryPolicy.Evaluate(message, checkpoint, profile, capabilitySnapshot);
        Assert.Equal(CopilotAgentRecoveryMode.RetryRead, writeExcluded.Request!.Mode);
        Assert.Equal("FetchUrl", writeExcluded.Request.ToolName);

        message.AgentStopReason = CopilotAgentStopReason.AwaitingUser;
        Assert.False(CopilotAgentRecoveryPolicy.Evaluate(message, checkpoint, profile, capabilitySnapshot).IsAvailable);
        message.AgentStopReason = CopilotAgentStopReason.ApprovalDenied;
        Assert.False(CopilotAgentRecoveryPolicy.Evaluate(message, checkpoint, profile, capabilitySnapshot).IsAvailable);
    }

    [Fact]
    public void TaskEventJournal_RecordsRecoveryWithoutArgumentsOrApprovalReuse()
    {
        var builder = new CopilotAgentTaskEventJournalBuilder();
        builder.RecordRunStarted();
        builder.RecordRecovery(new CopilotAgentRecoveryRequest
        {
            Mode = CopilotAgentRecoveryMode.RetryRead,
            PreviousStopReason = CopilotAgentStopReason.BudgetExhausted,
            ToolName = "FetchUrl",
            SourceCallKey = CopilotAgentTaskEventIds.ForCall("provider-call-with-secret-arguments"),
        });

        var recovery = Assert.Single(builder.Snapshot().Events, item => item.Type == CopilotAgentTaskEventType.RecoveryRequested);
        Assert.Equal(CopilotAgentRecoveryMode.RetryRead.ToString(), recovery.State);
        Assert.Equal("FetchUrl", recovery.ToolName);
        var serialized = JsonSerializer.Serialize(recovery);
        Assert.DoesNotContain("secret-arguments", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("approval", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StateStore_PersistsAgentTaskLedgerAndStructuredStopReason()
    {
        var store = new CopilotChatStateStore(_tempRoot);
        var state = new CopilotChatState();
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Waiting for a decision.")
        {
            AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "plan",
                ResumedFromCheckpoint = true,
                Items = new[]
                {
                    new CopilotAgentTaskItem { Id = 1, Title = "Inspect state", Description = "Read current state.", IsComplete = true },
                    new CopilotAgentTaskItem { Id = 2, Title = "Choose change", Description = "Needs user direction." },
                },
            },
            AgentStopReason = CopilotAgentStopReason.AwaitingUser,
            AgentBlockers =
            [
                new CopilotAgentBlockerSnapshot
                {
                    Kind = CopilotAgentBlockerKind.UserDecision,
                    Code = "user_decision_required",
                    Summary = "The Agent needs a user decision before continuing the remaining tasks.",
                    RequiresUserInput = true,
                },
            ],
        });
        state.Conversations.Add(conversation);

        store.Save(state);
        var recovered = store.Load();

        var message = Assert.Single(Assert.Single(recovered.Conversations).Messages);
        Assert.Equal(CopilotAgentStopReason.AwaitingUser, message.AgentStopReason);
        Assert.Equal("plan", message.AgentTaskLedger.Mode);
        Assert.Equal(2, message.AgentTaskLedger.TotalCount);
        Assert.Equal(1, message.AgentTaskLedger.CompletedCount);
        Assert.True(message.HasIncompleteAgentTasks);
        var blocker = Assert.Single(message.AgentBlockers);
        Assert.Equal(CopilotAgentBlockerKind.UserDecision, blocker.Kind);
        Assert.True(message.HasAgentBlockers);
        Assert.Equal("等待用户决定", message.AgentStopReasonLabel);
    }

    [Fact]
    public void AgentSessionCheckpoint_RejectsCorruptJsonAndChangedProfile()
    {
        var profile = CreateProfile();
        Assert.Null(CopilotAgentSessionCheckpoint.Create(profile, "{not-json"));

        var checkpoint = CopilotAgentSessionCheckpoint.Create(profile, "{\"state\":{}}");
        Assert.NotNull(checkpoint);
        Assert.True(checkpoint!.IsUsableFor(profile));

        var changedProfile = profile.Clone();
        changedProfile.Model = "different-model";
        Assert.False(checkpoint.IsUsableFor(changedProfile));

        var corruptCapability = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = CopilotAgentSessionCheckpoint.CreateProfileKey(profile),
            SerializedSessionJson = "{\"state\":{}}",
            CapabilityCatalogRevision = 1,
            Capabilities = [new CopilotAgentCheckpointCapability { Id = "builtin:test", Revision = 1, Fingerprint = null! }],
        };
        Assert.False(corruptCapability.IsStructurallyValid());
    }

    [Fact]
    public void AgentTrace_PersistsRedactedToolLifecycleAndRecoversRunningEntryAsInterrupted()
    {
        var store = new CopilotChatStateStore(_tempRoot);
        var state = new CopilotChatState();
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            IsExecutionInProgress = true,
        };
        message.UpsertAgentTrace(CopilotAgentTraceEntry.FromStarted(new CopilotToolExecutionInfo
        {
            CallId = "call-1",
            Round = 2,
            Attempt = 2,
            MaxAttempts = 2,
            RuntimeName = "microsoft-agent-framework",
            ToolName = "FetchUrl",
            Access = CopilotToolAccess.ReadOnly,
            ArgumentSummary = "query=https://example.test/?api_key=secret-value",
            State = CopilotToolExecutionState.Running,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-2),
            TimeoutMs = 30_000,
        }));
        conversation.Messages.Add(message);
        state.Conversations.Add(conversation);
        store.Save(state);

        var loadedMessage = Assert.Single(Assert.Single(store.Load().Conversations).Messages);

        Assert.True(loadedMessage.EnsureValid());
        var trace = Assert.Single(loadedMessage.AgentTraceEntries);
        Assert.Equal(CopilotAgentTraceEntry.CurrentSchemaVersion, trace.SchemaVersion);
        Assert.Equal(CopilotToolExecutionState.Interrupted, trace.State);
        Assert.Equal(2, trace.Attempt);
        Assert.Equal(2, trace.MaxAttempts);
        Assert.NotNull(trace.CompletedAtUtc);
        Assert.False(loadedMessage.IsExecutionInProgress);
        Assert.Contains("Interrupted", loadedMessage.ExecutionContent, StringComparison.Ordinal);
        Assert.Contains("<redacted>", loadedMessage.ExecutionContent, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", loadedMessage.ExecutionContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentTrace_ResultSnapshotRedactsAndCapsPersistedOutput()
    {
        var execution = new CopilotToolExecutionInfo
        {
            CallId = "call-2",
            Round = 1,
            Attempt = 1,
            MaxAttempts = 2,
            RuntimeName = "microsoft-agent-framework",
            ToolName = "WebSearch",
            State = CopilotToolExecutionState.Failed,
            FailureKind = CopilotToolFailureKind.Transient,
            RetryEligible = true,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(-20),
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DurationMs = 20,
        };
        var trace = CopilotAgentTraceEntry.FromResult(execution, new CopilotToolResult
        {
            ToolName = "WebSearch",
            Success = false,
            Summary = "token=secret-value " + new string('x', 1_000),
            ErrorMessage = "api_key=another-secret",
        });

        Assert.Contains("<redacted>", trace.ResultSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", trace.ResultSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("another-secret", trace.ErrorMessage, StringComparison.Ordinal);
        Assert.True(trace.ResultSummary.Length <= 803);
        Assert.Equal(1, trace.Attempt);
        Assert.Equal(2, trace.MaxAttempts);
        Assert.Equal(CopilotToolFailureKind.Transient, trace.FailureKind);
        Assert.True(trace.RetryEligible);
    }

    [Fact]
    public void AgentTrace_RecoversPendingApprovalAsInterruptedWithoutDroppingActionId()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        message.UpsertAgentTrace(CopilotAgentTraceEntry.FromResult(new CopilotToolExecutionInfo
        {
            CallId = "approval-call",
            Round = 1,
            RuntimeName = "microsoft-agent-framework",
            ToolName = "CreateFlow",
            Access = CopilotToolAccess.Write,
            RiskLevel = CopilotToolRiskLevel.High,
            ApprovalMode = CopilotToolApprovalMode.Always,
            Idempotency = CopilotToolIdempotency.NonIdempotent,
            ApprovalActionId = "approval-1",
            State = CopilotToolExecutionState.AwaitingApproval,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
        }, new CopilotToolResult
        {
            ToolName = "CreateFlow",
            Success = true,
            Summary = "Waiting for approval.",
        }));

        Assert.StartsWith("Awaiting approval", message.ExecutionSummary, StringComparison.Ordinal);

        Assert.True(message.EnsureValid());

        var trace = Assert.Single(message.AgentTraceEntries);
        Assert.Equal(CopilotToolExecutionState.Interrupted, trace.State);
        Assert.Equal("approval-1", trace.ApprovalActionId);
        Assert.Contains("fresh approval", trace.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatService_ParsesOpenAiStreamingReasoningContentAndUsage()
    {
        const string responseBody = """
            data: {"choices":[{"delta":{"reasoning_content":"inspect "}}]}

            data: {"choices":[{"delta":{"content":"done"}}]}

            data: {"choices":[],"usage":{"prompt_tokens":3,"completion_tokens":2,"total_tokens":5}}

            data: [DONE]

            """;
        using var httpClient = new HttpClient(new StaticResponseHandler(() => CreateEventStreamResponse(responseBody)));
        var service = new CopilotChatService(httpClient);
        var deltas = new List<CopilotStreamDelta>();

        var usage = await service.StreamReplyAsync(CreateProfile(), new[] { new CopilotRequestMessage("user", "test") }, deltas.Add, CancellationToken.None);

        Assert.Equal("inspect ", string.Concat(deltas.Select(delta => delta.ReasoningContent)));
        Assert.Equal("done", string.Concat(deltas.Select(delta => delta.Content)));
        Assert.Equal(new CopilotTokenUsage(3, 2, 5), usage);
    }

    [Fact]
    public async Task ChatService_SendsDeepSeekAnthropicHighReasoningParameters()
    {
        var profile = CreateProfile();
        profile.VendorType = CopilotVendorType.DeepSeek;
        profile.ProviderType = CopilotProviderType.AnthropicCompatible;
        profile.BaseUrl = "https://api.deepseek.com/anthropic";
        profile.ReasoningMode = CopilotReasoningMode.High;
        using var handler = new CapturingResponseHandler(() => CreateJsonResponse("{\"content\":[{\"type\":\"text\",\"text\":\"ok\"}]}"));
        using var httpClient = new HttpClient(handler);
        var service = new CopilotChatService(httpClient);

        await service.CompleteReplyAsync(profile, new[] { new CopilotRequestMessage("user", "test") }, CancellationToken.None);

        using var document = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var root = document.RootElement;
        Assert.Equal("enabled", root.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("high", root.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.False(root.TryGetProperty("reasoning_effort", out _));
        Assert.False(root.TryGetProperty("temperature", out _));
    }

    [Fact]
    public async Task ChatService_SendsDeepSeekOpenAiMaxReasoningParameters()
    {
        var profile = CreateProfile();
        profile.VendorType = CopilotVendorType.DeepSeek;
        profile.ReasoningMode = CopilotReasoningMode.Max;
        using var handler = new CapturingResponseHandler(() => CreateJsonResponse("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}"));
        using var httpClient = new HttpClient(handler);
        var service = new CopilotChatService(httpClient);

        await service.CompleteReplyAsync(profile, new[] { new CopilotRequestMessage("user", "test") }, CancellationToken.None);

        using var document = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var root = document.RootElement;
        Assert.Equal("enabled", root.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("max", root.GetProperty("reasoning_effort").GetString());
        Assert.False(root.TryGetProperty("output_config", out _));
        Assert.False(root.TryGetProperty("temperature", out _));
    }

    [Fact]
    public async Task ChatService_DefaultReasoningPreservesExistingTemperatureBehavior()
    {
        var profile = CreateProfile();
        profile.VendorType = CopilotVendorType.DeepSeek;
        using var handler = new CapturingResponseHandler(() => CreateJsonResponse("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}"));
        using var httpClient = new HttpClient(handler);
        var service = new CopilotChatService(httpClient);

        await service.CompleteReplyAsync(profile, new[] { new CopilotRequestMessage("user", "test") }, CancellationToken.None);

        using var document = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var root = document.RootElement;
        Assert.Equal(profile.Temperature, root.GetProperty("temperature").GetDouble());
        Assert.False(root.TryGetProperty("thinking", out _));
        Assert.False(root.TryGetProperty("reasoning_effort", out _));
    }

    [Fact]
    public async Task ChatService_SendsXiaomiThinkingToggleWithoutFakeEffortLevel()
    {
        var profile = CreateProfile();
        profile.VendorType = CopilotVendorType.Xiaomi;
        profile.ProviderType = CopilotProviderType.AnthropicCompatible;
        profile.BaseUrl = "https://api.xiaomimimo.com/anthropic";
        profile.Model = "mimo-v2.5-pro";
        profile.ReasoningMode = CopilotReasoningMode.Enabled;
        using var handler = new CapturingResponseHandler(() => CreateJsonResponse("{\"content\":[{\"type\":\"text\",\"text\":\"ok\"}]}"));
        using var httpClient = new HttpClient(handler);
        var service = new CopilotChatService(httpClient);

        await service.CompleteReplyAsync(profile, new[] { new CopilotRequestMessage("user", "test") }, CancellationToken.None);

        using var document = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var root = document.RootElement;
        Assert.Equal("enabled", root.GetProperty("thinking").GetProperty("type").GetString());
        Assert.False(root.TryGetProperty("reasoning_effort", out _));
        Assert.False(root.TryGetProperty("output_config", out _));
        Assert.False(root.TryGetProperty("temperature", out _));
    }

    [Fact]
    public void ReasoningCapabilities_ExposeOnlyProviderSupportedChoices()
    {
        var profile = CreateProfile();
        profile.VendorType = CopilotVendorType.DeepSeek;
        profile.ReasoningMode = CopilotReasoningMode.Max;
        Assert.Equal(
            new[] { CopilotReasoningMode.Default, CopilotReasoningMode.Disabled, CopilotReasoningMode.High, CopilotReasoningMode.Max },
            CopilotReasoningCapabilities.GetOptions(profile).Select(option => option.Mode));
        Assert.Equal(CopilotReasoningMode.Max, profile.Clone().ReasoningMode);

        profile.VendorType = CopilotVendorType.Xiaomi;
        Assert.Equal(CopilotReasoningMode.Enabled, profile.ReasoningMode);
        Assert.Equal(
            new[] { CopilotReasoningMode.Default, CopilotReasoningMode.Disabled, CopilotReasoningMode.Enabled },
            CopilotReasoningCapabilities.GetOptions(profile).Select(option => option.Mode));
    }

    [Fact]
    public async Task ChatService_ReportsProviderErrorWithoutLeakingRequestSecret()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":{\"message\":\"invalid model\"}}", Encoding.UTF8, "application/json"),
        }));
        var service = new CopilotChatService(httpClient);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteReplyAsync(
            CreateProfile(),
            new[] { new CopilotRequestMessage("user", "test") },
            CancellationToken.None));

        Assert.Contains("400: invalid model", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-key", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChatService_PropagatesRequestCancellation()
    {
        using var httpClient = new HttpClient(new DelayedResponseHandler());
        var service = new CopilotChatService(httpClient);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CompleteReplyAsync(
            CreateProfile(),
            new[] { new CopilotRequestMessage("user", "cancel") },
            cts.Token));
    }

    [Fact]
    public async Task McpToolRouterRejectsDuplicatesAndReturnsStableUnknownToolError()
    {
        var router = new CopilotMcpToolRouter()
            .Register("ping", (_, _, _) => Task.FromResult(CopilotMcpToolCallResult.Ok("pong")));

        Assert.Throws<InvalidOperationException>(() => router.Register("PING", (_, _, _) => Task.FromResult(CopilotMcpToolCallResult.Ok("duplicate"))));
        var ping = await router.DispatchAsync("ping", null, "test", CancellationToken.None);
        var missing = await router.DispatchAsync("missing", null, "test", CancellationToken.None);

        Assert.True(ping.Success);
        Assert.Equal("pong", ping.Text);
        Assert.False(missing.Success);
        Assert.Equal("tool_not_found", missing.ErrorCode);
    }

    [Fact]
    public async Task AgentService_ExecutesPlannedToolThenStreamsFinalAnswer()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            CreateJsonResponse("{\"choices\":[{\"message\":{\"content\":\"{\\\"action\\\":\\\"tool\\\",\\\"toolName\\\":\\\"TestTool\\\",\\\"reason\\\":\\\"collect evidence\\\"}\"}}]}"),
            CreateJsonResponse("{\"choices\":[{\"message\":{\"content\":\"{\\\"action\\\":\\\"finish\\\",\\\"reason\\\":\\\"enough evidence\\\"}\"}}]}"),
            CreateEventStreamResponse("data: {\"choices\":[{\"delta\":{\"content\":\"final answer\"}}]}\n\ndata: [DONE]\n\n"),
        });
        using var httpClient = new HttpClient(new StaticResponseHandler(() => responses.Dequeue()));
        var chatService = new CopilotChatService(httpClient);
        var tool = new TestAgentTool();
        var service = new CopilotAgentService(chatService, new CopilotToolRegistry(new[] { tool }), new CopilotAgentContextBuilder());
        var events = new List<CopilotAgentEvent>();

        var result = await service.RunAsync(new CopilotAgentRequest
        {
            UserText = "diagnose",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, events.Add, CancellationToken.None);

        Assert.Equal(1, tool.ExecutionCount);
        Assert.Single(result.StepRecords);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.AnswerDelta && item.Text == "final answer");
        Assert.Equal(CopilotAgentEventType.Completed, events[^1].Type);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_LoadsProjectSkillWithoutApprovalOrScriptExecution()
    {
        var skillDirectory = Path.Combine(_tempRoot, ".agents", "skills", "test-diagnostics");
        Directory.CreateDirectory(skillDirectory);
        File.WriteAllText(Path.Combine(skillDirectory, "SKILL.md"), """
            ---
            name: test-diagnostics
            description: Test-only diagnostic workflow.
            ---

            Follow the test-only diagnostic workflow.
            """);
        using var fakeChatClient = new ScriptedHarnessChatClient(
            options => CreateLoadSkillCall(options, "test-diagnostics"));
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        var events = new List<CopilotAgentEvent>();

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "Use the test diagnostic workflow.",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Auto,
            SearchRootPaths = new[] { _tempRoot },
        }, events.Add, CancellationToken.None);

        Assert.True(fakeChatClient.StreamCallCount >= 2);
        Assert.Empty(result.StepRecords);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
            && item.Text.Contains("Agent Skills enabled", StringComparison.Ordinal));
        Assert.DoesNotContain(events, item => item.Text.Contains("waiting for explicit approval", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_ExecutesExternalToolThroughUnifiedExecutor()
    {
        var schema = CopilotToolInputSchema.FromJsonSchema(JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { message = new { type = "string" } },
            required = new[] { "message" },
            additionalProperties = false,
        }));
        var tool = new TestAgentTool("Mcp_Test_Echo", inputSchema: schema);
        var externalProvider = new StaticExternalToolProvider(tool);
        using var fakeChatClient = new ScriptedHarnessChatClient(options =>
            new FunctionCallContent("external-call", GetFunction(options, "colorvision_mcp_test_echo").Name, new Dictionary<string, object?>
            {
                ["message"] = "hello MCP",
            }));
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => fakeChatClient,
            externalProvider);
        var events = new List<CopilotAgentEvent>();

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "Use the configured MCP echo tool.",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Auto,
        }, events.Add, CancellationToken.None);

        Assert.Equal(1, externalProvider.DiscoverCount);
        Assert.Equal(1, tool.ExecutionCount);
        Assert.Equal("hello MCP", tool.LastInput?.Arguments["message"]);
        var step = Assert.Single(result.StepRecords);
        Assert.Equal("Mcp_Test_Echo", step.ToolCall.ToolName);
        Assert.Equal(CopilotToolExecutionState.Completed, step.Execution.State);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
            && item.Text.Contains("MCP client test connected", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_ExternalProtectedToolStillRequiresExactCallApproval()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        var tool = new FrameworkApprovalTestTool();
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_protected_write", new Dictionary<string, object?>
        {
            ["query"] = "external-approved-value",
        });
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => fakeChatClient,
            new StaticExternalToolProvider(tool));

        var runTask = runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "Run the configured protected external tool.",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Auto,
        }, _ => { }, CancellationToken.None);
        var action = await WaitForPendingActionAsync();

        Assert.Equal(0, tool.ApprovedExecutionCount);
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out _));
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, tool.ApprovedExecutionCount);
        Assert.Equal(0, tool.UnapprovedExecutionCount);
        Assert.Equal("external-approved-value", tool.LastInput?.Query);
        Assert.Equal(CopilotToolExecutionState.Completed, Assert.Single(result.StepRecords).Execution.State);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_ConnectsOfficialMcpClientToColorVisionServer()
    {
        const string tokenVariable = "COLORVISION_TEST_EXTERNAL_MCP_TOKEN";
        const string token = "external-mcp-test-token";
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var server = CopilotMcpServer.Instance;
        Environment.SetEnvironmentVariable(tokenVariable, token);
        server.ApplySettings(new CopilotMcpRuntimeSettings
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            BearerToken = token,
        });

        try
        {
            FunctionCallContent? CallStatus(ChatOptions options)
            {
                var function = options.Tools?.OfType<AIFunctionDeclaration>()
                    .SingleOrDefault(tool => tool.Name.EndsWith("get_server_status", StringComparison.Ordinal));
                return function == null
                    ? null
                    : new FunctionCallContent("mcp-status-call", function.Name, new Dictionary<string, object?>());
            }
            using var fakeChatClient = new ScriptedHarnessChatClient(
                CallStatus, _ => null,
                CallStatus, _ => null,
                CallStatus, _ => null,
                CallStatus);
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
                new CopilotAgentContextBuilder(),
                _ => fakeChatClient);
            var events = new List<CopilotAgentEvent>();
            var externalServer = new CopilotMcpClientServerConfig
            {
                Name = "colorvision",
                Endpoint = $"http://127.0.0.1:{port}/mcp",
                BearerTokenEnvironmentVariable = tokenVariable,
                AccessPolicy = CopilotMcpClientAccessPolicy.RequireApproval,
                ToolRules =
                [
                    new CopilotMcpClientToolRule
                    {
                        ToolName = "get_server_status",
                        AccessPolicy = CopilotMcpClientAccessPolicy.ReadOnly,
                    },
                ],
            };

            var request = new CopilotAgentRequest
            {
                UserText = "Read the current ColorVision status from the configured MCP server.",
                Profile = CreateProfile(),
                Mode = CopilotAgentMode.Auto,
                ExternalMcpServers =
                [
                    externalServer,
                ],
            };
            var result = await runtime.RunAsync(request, events.Add, CancellationToken.None);

            Assert.True(events.Any(item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
                    && item.Text.Contains("MCP client connected to colorvision", StringComparison.Ordinal)),
                string.Join(Environment.NewLine, events.Where(item => item.Type == CopilotAgentEventType.RuntimeDiagnostic).Select(item => item.Text)));
            var step = Assert.Single(result.StepRecords);
            Assert.Equal("Mcp_colorvision_get_server_status", step.ToolCall.ToolName);
            Assert.Equal(CopilotToolExecutionState.Completed, step.Execution.State);
            Assert.True(CopilotMcpClientHealthRegistry.TryGetSnapshot(externalServer, out var health));
            Assert.Equal(CopilotMcpClientHealthState.Connected, health.State);
            Assert.True(health.DiscoveredToolCount > 1);
            Assert.Equal(1, health.ExposedToolCount);
            Assert.Equal(health.DiscoveredToolCount - 1, health.FilteredToolCount);
            Assert.False(health.UsedCachedDiscovery);
            Assert.Equal(1, health.CapabilityRevision);
            var externalSourceId = CopilotCapabilityCatalog.BuildExternalMcpSourceId(externalServer);
            var catalogEntry = Assert.Single(
                CopilotCapabilityCatalog.Shared.GetSnapshot().Capabilities,
                capability => capability.Id == externalSourceId + ":get_server_status");
            Assert.Equal(CopilotCapabilitySourceKind.ExternalMcp, catalogEntry.SourceKind);
            Assert.Equal(CopilotToolApprovalMode.Never, catalogEntry.ApprovalMode);
            Assert.Equal(CopilotToolAuditArgumentMode.NamesOnly, catalogEntry.AuditArgumentMode);

            var cachedResult = await runtime.RunAsync(request, events.Add, CancellationToken.None);

            Assert.Equal(CopilotToolExecutionState.Completed, Assert.Single(cachedResult.StepRecords).Execution.State);
            Assert.True(CopilotMcpClientHealthRegistry.TryGetSnapshot(externalServer, out var cachedHealth));
            Assert.True(cachedHealth.UsedCachedDiscovery);
            Assert.Equal(health.CapabilityRevision, cachedHealth.CapabilityRevision);
            Assert.False(cachedHealth.ToolListChangeNotificationsEnabled);
            Assert.Contains(events, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
                && item.Text.Contains("cached discovery", StringComparison.Ordinal));

            Assert.True(CopilotMcpClientDiscoveryRegistry.NotifyToolListChanged(externalServer));
            Assert.True(CopilotMcpClientHealthRegistry.TryGetSnapshot(externalServer, out var invalidatedHealth));
            Assert.True(invalidatedHealth.CacheInvalidated);
            Assert.True(invalidatedHealth.CapabilitiesChanged);

            var invalidatedResult = await runtime.RunAsync(request, events.Add, CancellationToken.None);

            Assert.Equal(CopilotToolExecutionState.Completed, Assert.Single(invalidatedResult.StepRecords).Execution.State);
            Assert.True(CopilotMcpClientHealthRegistry.TryGetSnapshot(externalServer, out var rediscoveredHealth));
            Assert.False(rediscoveredHealth.UsedCachedDiscovery);
            Assert.False(rediscoveredHealth.CacheInvalidated);
            Assert.Equal(cachedHealth.CapabilityRevision, rediscoveredHealth.CapabilityRevision);
            Assert.False(rediscoveredHealth.CapabilitiesChanged);

            var forcedResult = await runtime.RunAsync(new CopilotAgentRequest
            {
                UserText = request.UserText,
                Profile = request.Profile,
                Mode = request.Mode,
                ExternalMcpServers = request.ExternalMcpServers,
                ForceExternalMcpToolRefresh = true,
            }, events.Add, CancellationToken.None);

            Assert.Equal(CopilotToolExecutionState.Completed, Assert.Single(forcedResult.StepRecords).Execution.State);
            Assert.True(CopilotMcpClientHealthRegistry.TryGetSnapshot(externalServer, out var forcedHealth));
            Assert.False(forcedHealth.UsedCachedDiscovery);
            Assert.Equal(rediscoveredHealth.CapabilityRevision, forcedHealth.CapabilityRevision);
            Assert.False(forcedHealth.CapabilitiesChanged);
        }
        finally
        {
            server.Stop();
            Environment.SetEnvironmentVariable(tokenVariable, null);
            CopilotCapabilityCatalog.Shared.RetainExternalMcpServers(Array.Empty<CopilotMcpClientServerConfig>());
        }
    }

    [Fact]
    public async Task AgentService_FetchesDirectUrlBeforePlannerCanFinish()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            CreateJsonResponse("{\"choices\":[{\"message\":{\"content\":\"{\\\"action\\\":\\\"finish\\\",\\\"reason\\\":\\\"no web access\\\"}\"}}]}"),
            CreateEventStreamResponse("data: {\"choices\":[{\"delta\":{\"content\":\"page-backed answer\"}}]}\n\ndata: [DONE]\n\n"),
        });
        using var httpClient = new HttpClient(new StaticResponseHandler(() => responses.Dequeue()));
        var tool = new TestAgentTool("FetchUrl");
        var service = new CopilotAgentService(
            new CopilotChatService(httpClient),
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder());
        var events = new List<CopilotAgentEvent>();

        var result = await service.RunAsync(new CopilotAgentRequest
        {
            UserText = "https://codexradar.com/ 这里实现了什么？",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Auto,
        }, events.Add, CancellationToken.None);

        Assert.Equal(1, tool.ExecutionCount);
        Assert.Equal("https://codexradar.com/", tool.LastInput?.Query);
        Assert.Single(result.StepRecords);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.Status && item.Text.Contains("required web evidence policy", StringComparison.Ordinal));
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.AnswerDelta && item.Text == "page-backed answer");
    }

    [Fact]
    public async Task AgentService_SearchesWebWhenDirectUrlFetchFails()
    {
        var responses = new Queue<HttpResponseMessage>(new[]
        {
            CreateJsonResponse("{\"choices\":[{\"message\":{\"content\":\"{\\\"action\\\":\\\"finish\\\",\\\"reason\\\":\\\"fallback complete\\\"}\"}}]}"),
            CreateEventStreamResponse("data: {\"choices\":[{\"delta\":{\"content\":\"search-backed answer\"}}]}\n\ndata: [DONE]\n\n"),
        });
        using var httpClient = new HttpClient(new StaticResponseHandler(() => responses.Dequeue()));
        var fetchTool = new TestAgentTool("FetchUrl", success: false);
        var searchTool = new TestAgentTool("WebSearch");
        var service = new CopilotAgentService(
            new CopilotChatService(httpClient),
            new CopilotToolRegistry(new[] { fetchTool, searchTool }),
            new CopilotAgentContextBuilder());

        var result = await service.RunAsync(new CopilotAgentRequest
        {
            UserText = "https://example.test/ 这里实现了什么？",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Auto,
        }, _ => { }, CancellationToken.None);

        Assert.Equal(1, fetchTool.ExecutionCount);
        Assert.Equal(1, searchTool.ExecutionCount);
        Assert.Equal(2, result.StepRecords.Count);
        Assert.Equal("https://example.test/ 这里实现了什么？", searchTool.LastInput?.Query);
    }

    [Fact]
    public async Task AgentRuntimeRouter_UsesFrameworkForOpenAiAndAnthropicProfiles()
    {
        var builtIn = new RecordingAgentRuntime("built-in");
        var framework = new RecordingAgentRuntime("agent-framework");
        var router = new CopilotAgentRuntimeRouter(builtIn, framework);
        var profile = CreateProfile();
        var events = new List<CopilotAgentEvent>();

        await router.RunAsync(new CopilotAgentRequest { Profile = profile }, events.Add, CancellationToken.None);
        Assert.Equal(0, builtIn.RunCount);
        Assert.Equal(1, framework.RunCount);

        profile.ProviderType = CopilotProviderType.AnthropicCompatible;
        await router.RunAsync(new CopilotAgentRequest { Profile = profile }, events.Add, CancellationToken.None);
        Assert.Equal(0, builtIn.RunCount);
        Assert.Equal(2, framework.RunCount);

        profile.ProviderType = CopilotProviderType.OpenAICompatible;
        profile.VendorType = CopilotVendorType.Xiaomi;
        profile.ReasoningMode = CopilotReasoningMode.Enabled;
        await router.RunAsync(new CopilotAgentRequest { Profile = profile }, events.Add, CancellationToken.None);
        Assert.Equal(0, builtIn.RunCount);
        Assert.Equal(3, framework.RunCount);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_ExecutesAnyRegisteredRequestScopedToolAndStreamsAnswer()
    {
        var tool = new TestAgentTool("FetchUrl", inputSchema: CopilotToolInputSchema.Query("Complete URL.", required: true));
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_fetch_url");
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        var profile = CreateProfile();
        var events = new List<CopilotAgentEvent>();

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "https://example.test/ what does this page contain?",
            Profile = profile,
            Mode = CopilotAgentMode.Diagnose,
        }, events.Add, CancellationToken.None);

        Assert.Equal(1, tool.ExecutionCount);
        Assert.Equal("https://example.test/", tool.LastInput?.Query);
        Assert.Single(result.StepRecords);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.AnswerDelta && item.Text == "harness answer");
        Assert.Equal(CopilotAgentEventType.Completed, events[^1].Type);

        var function = Assert.IsAssignableFrom<AIFunctionDeclaration>(Assert.Single(fakeChatClient.LastOptions!.Tools!, tool => tool.Name == "colorvision_fetch_url"));
        Assert.True(function.JsonSchema.GetProperty("properties").TryGetProperty("query", out _));
        Assert.False(function.JsonSchema.GetProperty("properties").TryGetProperty("path", out _));
        Assert.Equal("query", function.JsonSchema.GetProperty("required")[0].GetString());
        Assert.False(function.JsonSchema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public async Task AgentFrameworkRuntime_InvokesIndependentReadToolsConcurrently()
    {
        var probe = new ParallelInvocationProbe(2);
        var firstTool = new ParallelProbeTool("ReadFirst", probe);
        var secondTool = new ParallelProbeTool("ReadSecond", probe);
        using var fakeChatClient = new BatchFunctionCallingChatClient(
            ("colorvision_read_first", "first"),
            ("colorvision_read_second", "second"));
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new ICopilotTool[] { firstTool, secondTool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "inspect both independent resources",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, _ => { }, CancellationToken.None);

        Assert.Equal(2, probe.MaximumActive);
        Assert.Equal(2, result.StepRecords.Count);
        Assert.All(result.StepRecords, step => Assert.Equal(CopilotToolConcurrencyMode.SharedRead, step.Execution.ConcurrencyMode));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_CompactsOversizedHistoryBeforeProviderCall()
    {
        using var fakeChatClient = new CapturingFinalChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        var oversizedHistory = Enumerable.Range(1, 8)
            .Select(index => new CopilotRequestMessage(index % 2 == 0 ? "assistant" : "user", $"history-{index}:" + new string('x', 40_000)))
            .ToArray();

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "current request must remain",
            History = oversizedHistory,
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, _ => { }, CancellationToken.None);

        Assert.True(result.Budget.CompactionEnabled);
        Assert.Equal(CopilotAgentTokenBudget.DefaultContextWindowTokens, result.Budget.ContextWindowTokens);
        Assert.NotNull(fakeChatClient.LastMessages);
        Assert.True(fakeChatClient.LastMessages!.Count < oversizedHistory.Length + 1);
        Assert.Contains(fakeChatClient.LastMessages, message => message.Text.Contains("current request must remain", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_StopsProviderLoopWhenRequestTokenBudgetIsExhausted()
    {
        var tool = new TestAgentTool("FetchUrl", inputSchema: CopilotToolInputSchema.Query("URL.", required: true));
        using var fakeChatClient = new FunctionCallingChatClient(
            "colorvision_fetch_url",
            repeatFunctionCallOnce: true,
            usageTokensPerCall: 40_000);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        var events = new List<CopilotAgentEvent>();

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "fetch with a bounded agent budget",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, events.Add, CancellationToken.None);

        Assert.Equal(2, fakeChatClient.StreamCallCount);
        Assert.True(result.Budget.BudgetExhausted);
        Assert.Equal(CopilotAgentStopReason.BudgetExhausted, result.StopReason);
        Assert.Equal(80_000, result.Budget.ConsumedTokens);
        Assert.Equal(2, result.Budget.ProviderCalls);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
            && item.Text.Contains("budget exhausted", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.AnswerDelta
            && item.Text.Contains("bounded token budget", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_ResumesPersistedFrameworkSessionWithoutDuplicatingVisibleHistory()
    {
        var profile = CreateProfile();
        using var firstClient = new CapturingFinalChatClient();
        var firstRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => firstClient);
        var firstResult = await firstRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "first persisted agent turn",
            Profile = profile,
            Mode = CopilotAgentMode.Diagnose,
        }, _ => { }, CancellationToken.None);

        Assert.NotNull(firstResult.SessionCheckpoint);
        Assert.True(firstResult.SessionCheckpoint!.IsUsableFor(profile));

        using var secondClient = new CapturingFinalChatClient();
        var secondRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => secondClient);
        var events = new List<CopilotAgentEvent>();
        var secondResult = await secondRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "second persisted agent turn",
            History = new[] { new CopilotRequestMessage("user", "DUPLICATE-SENTINEL") },
            Profile = profile,
            Mode = CopilotAgentMode.Diagnose,
            SessionCheckpoint = firstResult.SessionCheckpoint,
        }, events.Add, CancellationToken.None);

        Assert.NotNull(secondResult.SessionCheckpoint);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
            && item.Text.Contains("session resumed", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(secondClient.LastMessages);
        Assert.DoesNotContain(secondClient.LastMessages!, message => message.Text.Contains("DUPLICATE-SENTINEL", StringComparison.Ordinal));
        Assert.Contains(secondClient.LastMessages!, message => message.Text.Contains("first persisted agent turn", StringComparison.Ordinal));
        Assert.Contains(secondClient.LastMessages!, message => message.Text.Contains("second persisted agent turn", StringComparison.Ordinal));
        Assert.Contains(secondResult.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.SessionResumed);
        Assert.Contains(secondResult.TaskEventJournal.Events, item => item.Id == firstResult.TaskEventJournal.Events[0].Id);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_DiscardsPersistedPlanAndReplansAfterCapabilityDrift()
    {
        var profile = CreateProfile();
        var catalog = new CopilotCapabilityCatalog();
        var firstTool = new TestAgentTool(
            "CatalogProbe",
            inputSchema: CopilotToolInputSchema.OptionalQuery,
            evidenceMode: CopilotToolEvidenceMode.RedactedExcerpt);
        catalog.PublishSource(
            CopilotCapabilitySourceKind.Plugin,
            "plugin:runtime-test",
            "Runtime test",
            [firstTool]);
        using var firstClient = new FunctionCallingChatClient("colorvision_catalog_probe");
        var firstRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([firstTool]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => firstClient,
            new StaticExternalToolProvider(),
            catalog);
        var firstResult = await firstRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "create a checkpoint before the capability changes",
            Profile = profile,
            Mode = CopilotAgentMode.Auto,
        }, _ => { }, CancellationToken.None);
        Assert.NotNull(firstResult.SessionCheckpoint);
        var persistedEvidence = Assert.Single(firstResult.SessionCheckpoint!.EvidenceArtifacts);
        Assert.Equal("CatalogProbe", persistedEvidence.ToolName);
        Assert.Contains("deterministic evidence", persistedEvidence.ContentExcerpt, StringComparison.Ordinal);
        Assert.Contains(firstResult.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted && item.ToolName == "CatalogProbe");
        Assert.Contains(firstResult.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.EvidenceCaptured && item.SubjectId == persistedEvidence.Id);
        Assert.Contains(firstResult.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped);

        var changedTool = new TestAgentTool(
            "CatalogProbe",
            inputSchema: CopilotToolInputSchema.Query("Changed required input.", required: true),
            evidenceMode: CopilotToolEvidenceMode.RedactedExcerpt);
        catalog.PublishSource(
            CopilotCapabilitySourceKind.Plugin,
            "plugin:runtime-test",
            "Runtime test",
            [changedTool]);
        using var secondClient = new CapturingFinalChatClient();
        var secondRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([changedTool]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => secondClient,
            new StaticExternalToolProvider(),
            catalog);
        var events = new List<CopilotAgentEvent>();

        var secondResult = await secondRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "continue after the capability changes",
            History = new[] { new CopilotRequestMessage("user", "CAPABILITY-DRIFT-HISTORY-SENTINEL") },
            Profile = profile,
            Mode = CopilotAgentMode.Auto,
            SessionCheckpoint = firstResult.SessionCheckpoint,
        }, events.Add, CancellationToken.None);

        Assert.False(secondResult.TaskLedger.ResumedFromCheckpoint);
        Assert.NotNull(secondClient.LastMessages);
        Assert.Contains(secondClient.LastMessages!, message => message.Text.Contains("CAPABILITY-DRIFT-HISTORY-SENTINEL", StringComparison.Ordinal));
        Assert.Contains(secondClient.LastMessages!, message => message.Role == ChatRole.User
            && message.Text.Contains("Persisted evidence artifacts", StringComparison.Ordinal)
            && message.Text.Contains("deterministic evidence", StringComparison.Ordinal));
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
            && item.Text.Contains("capability drift", StringComparison.OrdinalIgnoreCase)
            && item.Text.Contains("re-plan", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(secondResult.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.ReplanRequired
            && item.State == CopilotAgentCheckpointCompatibilityKind.CapabilityDrift.ToString());
        Assert.Contains(secondResult.TaskEventJournal.Events, item => item.Id == firstResult.TaskEventJournal.Events[0].Id);
        Assert.True(secondResult.SessionCheckpoint!.IsUsableFor(profile, catalog.GetSnapshot()));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_UsesHarnessTodoLedgerAndLoopsUntilTasksComplete()
    {
        using var fakeChatClient = new ScriptedHarnessChatClient(
            options => CreateTodoAddCall(options, "Inspect current state", "Collect current evidence.", "Apply verified change", "Make the requested update."),
            options => CreateTodoCompleteCall(options, 1, "Current state inspected."),
            _ => null,
            options => CreateTodoCompleteCall(options, 2, "Verified change applied."),
            _ => null);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        var events = new List<CopilotAgentEvent>();

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "perform and verify a multi-step task",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Auto,
        }, events.Add, CancellationToken.None);

        Assert.Equal(5, fakeChatClient.StreamCallCount);
        Assert.Equal("execute", result.TaskLedger.Mode);
        Assert.Equal(2, result.TaskLedger.TotalCount);
        Assert.Equal(2, result.TaskLedger.CompletedCount);
        Assert.Equal(0, result.TaskLedger.RemainingCount);
        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Contains(result.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.TaskLedgerCaptured
            && item.State == "final"
            && item.RelatedIds.Contains("task:1", StringComparer.Ordinal)
            && item.RelatedIds.Contains("task:2", StringComparer.Ordinal));
        Assert.Contains(result.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped
            && item.State == CopilotAgentStopReason.Completed.ToString());
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
            && item.Text.Contains("2/2 complete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_ReportsBoundedStopWithOpenExecuteTasks()
    {
        using var fakeChatClient = new ScriptedHarnessChatClient(
            options => CreateTodoAddCall(options, "Still open", "The fake model intentionally leaves this task incomplete."),
            _ => null);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "exercise the bounded completion loop",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Auto,
        }, _ => { }, CancellationToken.None);

        Assert.Equal(4, fakeChatClient.StreamCallCount);
        Assert.Equal(1, result.TaskLedger.RemainingCount);
        Assert.Equal("execute", result.TaskLedger.Mode);
        Assert.Equal(CopilotAgentStopReason.TaskPassLimit, result.StopReason);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_ClassifiesPermanentToolFailureAsQueryableBlocker()
    {
        var tool = new TestAgentTool("PermanentProbe", success: false);
        using var client = new ScriptedHarnessChatClient(
            options => CreateTodoAddCall(options, "Collect required evidence", "The task depends on the probe."),
            _ => new FunctionCallContent("permanent-probe-call", "colorvision_permanent_probe", new Dictionary<string, object?>()),
            _ => null);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => client);

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "run a required probe",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Auto,
        }, _ => { }, CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Blocked, result.StopReason);
        var blocker = Assert.Single(result.Blockers);
        Assert.Equal(CopilotAgentBlockerKind.ToolFailure, blocker.Kind);
        Assert.Equal("PermanentProbe", blocker.ToolName);
        Assert.False(blocker.RetryEligible);
        Assert.StartsWith("call:", blocker.SourceCallKey, StringComparison.Ordinal);
        Assert.Contains(result.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.BlockerDetected
            && item.SubjectId == blocker.SourceCallKey
            && item.ToolName == "PermanentProbe");
    }

    [Fact]
    public async Task AgentFrameworkRuntime_PausesWithCheckpointAndResumesOpenTodo()
    {
        var profile = CreateProfile();
        var control = new CopilotAgentRunControl();
        using var cancellation = new CancellationTokenSource();
        using var firstClient = new TodoThenBlockingChatClient();
        var firstRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => firstClient);
        var run = firstRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "start a pausable task",
            Profile = profile,
            Mode = CopilotAgentMode.Auto,
            RunControl = control,
        }, _ => { }, cancellation.Token);

        await firstClient.BlockingCallStarted.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(control.RequestPause());
        cancellation.Cancel();
        var paused = await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CopilotAgentStopReason.Paused, paused.StopReason);
        Assert.Equal(1, paused.TaskLedger.RemainingCount);
        Assert.NotNull(paused.SessionCheckpoint);
        Assert.Contains(paused.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.PauseRequested);
        Assert.Contains(paused.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped
            && item.State == CopilotAgentStopReason.Paused.ToString());

        using var secondClient = new ScriptedHarnessChatClient(
            options => CreateTodoCompleteCall(options, 1, "Paused task resumed and completed."),
            _ => null);
        var secondRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => secondClient);
        var resumed = await secondRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "continue the paused task",
            Profile = profile,
            Mode = CopilotAgentMode.Auto,
            SessionCheckpoint = paused.SessionCheckpoint,
            Recovery = new CopilotAgentRecoveryRequest
            {
                Mode = CopilotAgentRecoveryMode.Resume,
                PreviousStopReason = CopilotAgentStopReason.Paused,
            },
        }, _ => { }, CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, resumed.StopReason);
        Assert.True(resumed.TaskLedger.ResumedFromCheckpoint);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_ExplicitCancelDiscardsNewCheckpoint()
    {
        var control = new CopilotAgentRunControl();
        using var cancellation = new CancellationTokenSource();
        using var client = new TodoThenBlockingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => client);
        var run = runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "start a cancellable task",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Auto,
            RunControl = control,
        }, _ => { }, cancellation.Token);

        await client.BlockingCallStarted.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(control.RequestCancel());
        cancellation.Cancel();
        var cancelled = await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CopilotAgentStopReason.Cancelled, cancelled.StopReason);
        Assert.Null(cancelled.SessionCheckpoint);
        Assert.Contains(cancelled.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.CancelRequested);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_AcceptsOnlyCheckpointMatchedRecoveryRequest()
    {
        var profile = CreateProfile();
        using var firstClient = new ScriptedHarnessChatClient(
            options => CreateTodoAddCall(options, "Recover current read", "The task remains open at the pass limit."),
            _ => null);
        var firstRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => firstClient);
        var firstResult = await firstRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "create one bounded recovery task",
            Profile = profile,
            Mode = CopilotAgentMode.Auto,
        }, _ => { }, CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.TaskPassLimit, firstResult.StopReason);
        Assert.NotNull(firstResult.SessionCheckpoint);

        using var secondClient = new ScriptedHarnessChatClient(
            options => CreateTodoCompleteCall(options, 1, "Recovered task completed."),
            _ => null);
        var secondRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => secondClient);
        var secondResult = await secondRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "continue the bounded recovery task",
            Profile = profile,
            Mode = CopilotAgentMode.Auto,
            SessionCheckpoint = firstResult.SessionCheckpoint,
            Recovery = new CopilotAgentRecoveryRequest
            {
                Mode = CopilotAgentRecoveryMode.Resume,
                PreviousStopReason = CopilotAgentStopReason.TaskPassLimit,
            },
        }, _ => { }, CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, secondResult.StopReason);
        var recovery = Assert.Single(secondResult.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.RecoveryRequested);
        Assert.Equal(CopilotAgentRecoveryMode.Resume.ToString(), recovery.State);
        Assert.Contains(secondResult.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.SessionResumed);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_RecoversOpenHarnessTodoAcrossRuntimeInstances()
    {
        var profile = CreateProfile();
        using var firstClient = new ScriptedHarnessChatClient(
            options => CreateTodoAddCall(options, "Inspect recovered resource", "Read-only work may continue after recovery."),
            options => CreateModeSetCall(options, "plan"),
            _ => null);
        var firstRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => firstClient);

        var firstResult = await firstRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "plan a resumable read-only inspection",
            Profile = profile,
            Mode = CopilotAgentMode.Auto,
        }, _ => { }, CancellationToken.None);

        Assert.NotNull(firstResult.SessionCheckpoint);
        Assert.Equal("plan", firstResult.TaskLedger.Mode);
        Assert.Equal(1, firstResult.TaskLedger.RemainingCount);
        Assert.Equal(CopilotAgentStopReason.AwaitingUser, firstResult.StopReason);

        using var secondClient = new ScriptedHarnessChatClient(
            options => CreateModeSetCall(options, "execute"),
            options => CreateTodoCompleteCall(options, 1, "Recovered read-only inspection completed."),
            _ => null);
        var secondRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => secondClient);
        var events = new List<CopilotAgentEvent>();

        var secondResult = await secondRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "continue the recovered inspection",
            Profile = profile,
            Mode = CopilotAgentMode.Auto,
            SessionCheckpoint = firstResult.SessionCheckpoint,
        }, events.Add, CancellationToken.None);

        Assert.True(secondResult.TaskLedger.ResumedFromCheckpoint);
        Assert.Equal("execute", secondResult.TaskLedger.Mode);
        Assert.Equal(1, secondResult.TaskLedger.CompletedCount);
        Assert.Equal(CopilotAgentStopReason.Completed, secondResult.StopReason);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.RuntimeDiagnostic
            && item.Text.Contains("task ledger recovered", StringComparison.OrdinalIgnoreCase)
            && item.Text.Contains("not authorization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_RequiresFreshWriteApprovalAfterTaskCheckpointRecovery()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        var profile = CreateProfile();
        var tool = new FrameworkApprovalTestTool();
        using var firstClient = new ScriptedHarnessChatClient(
            _ => new FunctionCallContent("first-write-call", "colorvision_protected_write", new Dictionary<string, object?> { ["query"] = "approved-value" }),
            options => CreateTodoAddCall(options, "Recheck protected value", "A recovered task must not reuse the prior approval."),
            options => CreateModeSetCall(options, "plan"),
            _ => null);
        var firstRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => firstClient);
        var firstRun = firstRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "apply once, then leave a recheck task",
            Profile = profile,
            Mode = CopilotAgentMode.Auto,
        }, _ => { }, CancellationToken.None);
        var firstAction = await WaitForPendingActionAsync();
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(firstAction.ActionId, out _));
        var firstResult = await firstRun.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, tool.ApprovedExecutionCount);
        Assert.NotNull(firstResult.SessionCheckpoint);
        Assert.Equal(1, firstResult.TaskLedger.RemainingCount);
        Assert.Contains(firstResult.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.ApprovalRequested);
        Assert.Contains(firstResult.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.ApprovalApproved);

        using var secondClient = new ScriptedHarnessChatClient(
            options => CreateModeSetCall(options, "execute"),
            _ => new FunctionCallContent("recovered-write-call", "colorvision_protected_write", new Dictionary<string, object?> { ["query"] = "approved-value" }),
            options => CreateTodoCompleteCall(options, 1, "Protected value rechecked with a new approval."),
            _ => null);
        var secondRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => secondClient);
        var secondRun = secondRuntime.RunAsync(new CopilotAgentRequest
        {
            UserText = "continue the protected recheck",
            Profile = profile,
            Mode = CopilotAgentMode.Auto,
            SessionCheckpoint = firstResult.SessionCheckpoint,
        }, _ => { }, CancellationToken.None);
        var secondAction = await WaitForPendingActionAsync();

        Assert.NotEqual(firstAction.ActionId, secondAction.ActionId);
        Assert.Equal("recovered-write-call", secondAction.AgentCallId);
        Assert.Equal(1, tool.ApprovedExecutionCount);
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(secondAction.ActionId, out _));
        var secondResult = await secondRun.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, tool.ApprovedExecutionCount);
        Assert.Equal(1, secondResult.TaskLedger.CompletedCount);
        Assert.Equal(0, secondResult.TaskLedger.RemainingCount);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_InjectsUserSteeringIntoActiveHarnessSession()
    {
        using var fakeChatClient = new SteerableChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);

        var runTask = runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "begin the long-running agent response",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Auto,
        }, _ => { }, CancellationToken.None);
        await fakeChatClient.FirstCallStarted.WaitAsync(TimeSpan.FromSeconds(5));
        const string steeringMessage = "change direction and verify the second path\nkeep this instruction on its own line";

        Assert.False(runtime.TryEnqueueSteeringMessage("   "));
        Assert.False(runtime.TryEnqueueSteeringMessage(new string('x', 16_001)));
        Assert.True(runtime.TryEnqueueSteeringMessage(steeringMessage));
        fakeChatClient.ReleaseFirstResponse();
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, fakeChatClient.StreamCallCount);
        Assert.NotNull(fakeChatClient.SecondCallMessages);
        Assert.Contains(fakeChatClient.SecondCallMessages!, message => message.Role == ChatRole.User
            && string.Equals(message.Text, steeringMessage, StringComparison.Ordinal));
        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        var steeringEvent = Assert.Single(result.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.SteeringQueued);
        Assert.Equal(CopilotAgentTaskEventIds.ForSteering(steeringMessage), steeringEvent.SubjectId);
        Assert.DoesNotContain("change direction", JsonSerializer.Serialize(result.TaskEventJournal), StringComparison.OrdinalIgnoreCase);
        Assert.False(runtime.TryEnqueueSteeringMessage("too late"));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_BudgetMiddlewarePropagatesCancellation()
    {
        using var fakeChatClient = new BlockingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "cancel the long provider request",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, _ => { }, cancellation.Token));

        Assert.Equal(1, fakeChatClient.StreamCallCount);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_ExecutesToolsForAnthropicCompatibleProfile()
    {
        var tool = new TestAgentTool("WebSearch", inputSchema: CopilotToolInputSchema.Query("Search query.", required: true));
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_web_search");
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        var profile = CreateProfile();
        profile.ProviderType = CopilotProviderType.AnthropicCompatible;
        profile.BaseUrl = "https://example.test/anthropic";

        await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "search the web",
            Profile = profile,
            Mode = CopilotAgentMode.Web,
        }, _ => { }, CancellationToken.None);

        Assert.Equal(1, tool.ExecutionCount);
        Assert.NotNull(fakeChatClient.LastOptions);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_RejectsArgumentsOutsideToolSchema()
    {
        var tool = new TestAgentTool("FetchUrl", inputSchema: CopilotToolInputSchema.Query("Complete URL.", required: true));
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_fetch_url", new Dictionary<string, object?>
        {
            ["query"] = "https://example.test/",
            ["path"] = "unexpected.txt",
        });
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "https://example.test/",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Web,
        }, _ => { }, CancellationToken.None);

        Assert.Equal(0, tool.ExecutionCount);
        Assert.Empty(result.StepRecords);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_PausesThenResumesApprovedProtectedToolInSameSession()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        var tool = new FrameworkApprovalTestTool();
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_protected_write", new Dictionary<string, object?>
        {
            ["query"] = "approved-value",
        });
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        var events = new List<CopilotAgentEvent>();

        var runTask = runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "apply the protected value",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, events.Add, CancellationToken.None);
        var action = await WaitForPendingActionAsync();

        Assert.Equal(0, tool.ApprovedExecutionCount);
        Assert.Equal(0, tool.UnapprovedExecutionCount);
        Assert.True(action.ResumesAgentOnApproval);
        Assert.False(action.ExecuteOnApproval);
        Assert.Equal("call-1", action.AgentCallId);
        Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(fakeChatClient.LastOptions!.Tools!, tool => tool.Name == "colorvision_protected_write"));
        await WaitUntilAsync(() => events.Any(item => item.ToolExecution?.State == CopilotToolExecutionState.AwaitingApproval));
        Assert.Contains(events, item => item.ToolExecution?.State == CopilotToolExecutionState.AwaitingApproval);

        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out _));
        Assert.False(CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out _));
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, tool.ApprovedExecutionCount);
        Assert.Equal(0, tool.UnapprovedExecutionCount);
        Assert.Equal("approved-value", tool.LastInput?.Query);
        Assert.Single(result.StepRecords);
        Assert.Equal("call-1", result.StepRecords[0].Execution.CallId);
        Assert.Equal(action.ActionId, result.StepRecords[0].Execution.ApprovalActionId);
        Assert.Equal(CopilotToolExecutionState.Completed, result.StepRecords[0].Execution.State);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.AnswerDelta && item.Text == "harness answer");
    }

    [Fact]
    public async Task AgentFrameworkRuntime_RejectionContinuesWithoutExecutingProtectedTool()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        var tool = new FrameworkApprovalTestTool();
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_protected_write");
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        var events = new List<CopilotAgentEvent>();

        var runTask = runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "apply the protected value",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, events.Add, CancellationToken.None);
        var action = await WaitForPendingActionAsync();

        Assert.True(CopilotMcpConfirmationStore.Instance.Reject(action.ActionId, out _));
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, tool.ApprovedExecutionCount);
        Assert.Equal(0, tool.UnapprovedExecutionCount);
        var step = Assert.Single(result.StepRecords);
        Assert.Equal(CopilotToolExecutionState.Denied, step.Execution.State);
        Assert.Equal(action.ActionId, step.Execution.ApprovalActionId);
        Assert.Contains(events, item => item.ToolExecution?.State == CopilotToolExecutionState.Denied);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.AnswerDelta && item.Text == "harness answer");
    }

    [Fact]
    public async Task AgentFrameworkRuntime_CancellationRejectsPendingApprovalAndDoesNotExecute()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        var tool = new FrameworkApprovalTestTool();
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_protected_write");
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        using var cancellation = new CancellationTokenSource();

        var runTask = runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "apply the protected value",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, _ => { }, cancellation.Token);
        var action = await WaitForPendingActionAsync();

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);

        Assert.Equal(ConfirmableActionStatus.Rejected, action.Status);
        Assert.Equal(0, tool.ApprovedExecutionCount);
        Assert.False(CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out _));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_ExpiresPendingApprovalWithoutUiTimer()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        CopilotMcpConfirmationStore.Instance.ActionLifetime = TimeSpan.FromMilliseconds(40);
        var tool = new FrameworkApprovalTestTool();
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_protected_write");
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);

        var runTask = runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "apply the protected value",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, _ => { }, CancellationToken.None);
        var action = await WaitForPendingActionAsync();
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ConfirmableActionStatus.Expired, action.Status);
        Assert.Equal(0, tool.ApprovedExecutionCount);
        Assert.Equal(CopilotToolExecutionState.Denied, Assert.Single(result.StepRecords).Execution.State);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_DoesNotReapproveOrReplaySameNonIdempotentCall()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        var tool = new FrameworkApprovalTestTool();
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_protected_write", repeatFunctionCallOnce: true);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);
        var events = new List<CopilotAgentEvent>();

        var runTask = runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "apply the protected value once",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, events.Add, CancellationToken.None);
        var action = await WaitForPendingActionAsync();

        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out _));
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, tool.ApprovedExecutionCount);
        Assert.Single(result.StepRecords);
        Assert.Empty(CopilotMcpConfirmationStore.Instance.GetPendingActions());
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.Status
            && item.Text.Contains("exact protected tool call", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AgentFrameworkRuntime_RetriesExplicitTransientFailureOnlyForIdempotentTool()
    {
        var tool = new FlakyIdempotentTool("FetchUrl");
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_fetch_url", repeatFunctionCallOnce: true);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "fetch the page and retry once if the network fails transiently",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, _ => { }, CancellationToken.None);

        Assert.Equal(2, tool.ExecutionCount);
        Assert.Collection(result.StepRecords,
            first =>
            {
                Assert.Equal(1, first.Execution.Attempt);
                Assert.Equal(2, first.Execution.MaxAttempts);
                Assert.Equal(CopilotToolExecutionState.Failed, first.Execution.State);
                Assert.Equal(CopilotToolFailureKind.Transient, first.Execution.FailureKind);
                Assert.True(first.Execution.RetryEligible);
            },
            second =>
            {
                Assert.Equal(2, second.Execution.Attempt);
                Assert.Equal(2, second.Execution.MaxAttempts);
                Assert.Equal(CopilotToolExecutionState.Completed, second.Execution.State);
                Assert.Equal(CopilotToolFailureKind.None, second.Execution.FailureKind);
                Assert.False(second.Execution.RetryEligible);
            });
    }

    [Fact]
    public async Task AgentFrameworkRuntime_DoesNotRetryIdempotentToolAfterPermanentFailure()
    {
        var tool = new PermanentFailureIdempotentTool("FetchUrl");
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_fetch_url", repeatFunctionCallOnce: true);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);

        var result = await runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "fetch the invalid page",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, _ => { }, CancellationToken.None);

        Assert.Equal(1, tool.ExecutionCount);
        var step = Assert.Single(result.StepRecords);
        Assert.Equal(CopilotToolFailureKind.Validation, step.Execution.FailureKind);
        Assert.False(step.Execution.RetryEligible);
    }

    [Fact]
    public async Task AgentFrameworkRuntime_RequiresFreshApprovalForProtectedIdempotentRetry()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        var tool = new FlakyApprovedIdempotentTool();
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_protected_idempotent_write", repeatFunctionCallOnce: true);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);

        var runTask = runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "apply the protected idempotent value and retry only after another approval",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, _ => { }, CancellationToken.None);
        var firstAction = await WaitForPendingActionAsync();

        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(firstAction.ActionId, out _));
        var secondAction = await WaitForPendingActionAsync();

        Assert.NotEqual(firstAction.ActionId, secondAction.ActionId);
        Assert.Equal(1, tool.ApprovedExecutionCount);
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(secondAction.ActionId, out _));
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, tool.ApprovedExecutionCount);
        Assert.Equal(0, tool.UnapprovedExecutionCount);
        Assert.Collection(result.StepRecords,
            first =>
            {
                Assert.Equal(1, first.Execution.Attempt);
                Assert.True(first.Execution.RetryEligible);
                Assert.Equal(firstAction.ActionId, first.Execution.ApprovalActionId);
            },
            second =>
            {
                Assert.Equal(2, second.Execution.Attempt);
                Assert.False(second.Execution.RetryEligible);
                Assert.Equal(secondAction.ActionId, second.Execution.ApprovalActionId);
                Assert.Equal(CopilotToolExecutionState.Completed, second.Execution.State);
            });
    }

    [Fact]
    public async Task AgentFrameworkRuntime_UsesNativeApprovalForRealTemplateApplyTool()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        CopilotMcpTemplatePatchPreviewStore.Instance.ClearForTests();
        const string currentJson = "{\"Exposure\":10}";
        var applyCount = 0;
        var context = new CopilotLiveContext
        {
            SourceId = "template-json-editor:framework-runtime-test",
            Title = "Template JSON editor",
            SnapshotItems = new[]
            {
                new CopilotContextItem { Title = "Template", Content = "Current JSON:\n```json\n" + currentJson + "\n```" },
            },
        };
        CopilotLiveContextRegistry.Publish(context);
        var preview = CopilotMcpTemplatePatchPreviewStore.Instance.Create(
            "active-template",
            context.SourceId,
            currentJson,
            "{\"Exposure\":12}",
            "{\"Exposure\":12}",
            new[] { "Exposure: 10 -> 12" });
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            LiveContextProvider = () => context,
            ApplyTemplatePatchHandler = (_, _) =>
            {
                applyCount++;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("applied"));
            },
        });
        var tool = new CopilotApplyTemplatePatchTool(dispatcher);
        using var fakeChatClient = new FunctionCallingChatClient("colorvision_apply_template_patch", new Dictionary<string, object?>
        {
            ["query"] = JsonSerializer.Serialize(new { preview_id = preview.PreviewId }),
        });
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry(new[] { tool }),
            new CopilotAgentContextBuilder(),
            _ => fakeChatClient);

        var runTask = runtime.RunAsync(new CopilotAgentRequest
        {
            UserText = "应用这个预览",
            Profile = CreateProfile(),
            Mode = CopilotAgentMode.Diagnose,
        }, _ => { }, CancellationToken.None);
        var action = await WaitForPendingActionAsync();

        Assert.Equal("ApplyTemplatePatch", action.ToolName);
        Assert.Equal(0, applyCount);
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, out _));
        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, applyCount);
        var step = Assert.Single(result.StepRecords);
        Assert.Equal("ApplyTemplatePatch", step.Execution.ToolName);
        Assert.Equal(CopilotToolExecutionState.Completed, step.Execution.State);
        Assert.Equal(action.ActionId, step.Execution.ApprovalActionId);
    }

    [Fact]
    public async Task AgentRuntimeRouter_FallsBackWhenFrameworkFailsBeforeMaterialProgress()
    {
        var builtIn = new RecordingAgentRuntime("built-in");
        var router = new CopilotAgentRuntimeRouter(builtIn, new ThrowingAgentRuntime());
        var events = new List<CopilotAgentEvent>();

        await router.RunAsync(new CopilotAgentRequest { Profile = CreateProfile() }, events.Add, CancellationToken.None);

        Assert.Equal(1, builtIn.RunCount);
        Assert.Contains(events, item => item.Type == CopilotAgentEventType.Status && item.Text.Contains("failed before executing a tool", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AgentRuntimeRouter_DoesNotFallbackAfterToolExecutionStarts()
    {
        var builtIn = new RecordingAgentRuntime("built-in");
        var router = new CopilotAgentRuntimeRouter(builtIn, new ToolStartingThenThrowingAgentRuntime());

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.RunAsync(
            new CopilotAgentRequest { Profile = CreateProfile() },
            _ => { },
            CancellationToken.None));

        Assert.Equal(0, builtIn.RunCount);
    }

    [Fact]
    public void ToolRegistry_RejectsDuplicateToolNames()
    {
        var error = Assert.Throws<ArgumentException>(() => new CopilotToolRegistry(new[]
        {
            new TestAgentTool("FetchUrl"),
            new TestAgentTool("fetchurl"),
        }));

        Assert.Contains("already registered", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        CopilotMcpTemplatePatchPreviewStore.Instance.ClearForTests();
        CopilotLiveContextRegistry.Clear("template-json-editor:framework-runtime-test");
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static async Task<ConfirmableAction> WaitForPendingActionAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var action = CopilotMcpConfirmationStore.Instance.GetPendingActions().SingleOrDefault();
            if (action != null)
                return action;
            await Task.Delay(10);
        }

        throw new TimeoutException("The framework approval action was not created.");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(10);
        }

        throw new TimeoutException("The expected framework event was not observed.");
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "secret-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 256,
            MaxToolRounds = 3,
        };
    }

    private static HttpResponseMessage CreateEventStreamResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "text/event-stream"),
        };
    }

    private static HttpResponseMessage CreateJsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StaticResponseHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responseFactory());
        }
    }

    private sealed class CapturingResponseHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestBody = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory();
        }
    }

    private sealed class DelayedResponseHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }

    private sealed class TestAgentTool : ICopilotTool
    {
        private readonly bool _success;

        public TestAgentTool(
            string name = "TestTool",
            bool success = true,
            CopilotToolInputSchema? inputSchema = null,
            CopilotToolEvidenceMode evidenceMode = CopilotToolEvidenceMode.Summary)
        {
            Name = name;
            _success = success;
            InputSchema = inputSchema ?? CopilotToolInputSchema.OptionalQuery;
            EvidenceMode = evidenceMode;
        }

        public string Name { get; }

        public string Description => "Collect deterministic test evidence.";

        public CopilotToolEvidenceMode EvidenceMode { get; }

        public CopilotToolInputSchema InputSchema { get; }

        public int ExecutionCount { get; private set; }

        public CopilotAgentToolInput? LastInput { get; private set; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            LastInput = toolInput;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = _success,
                Summary = _success ? "Evidence collected." : "Evidence collection failed.",
                Content = _success ? "deterministic evidence" : string.Empty,
                ErrorMessage = _success ? string.Empty : "deterministic failure",
            });
        }
    }

    private sealed class StaticExternalToolProvider(params ICopilotTool[] tools) : ICopilotExternalToolProvider
    {
        public int DiscoverCount { get; private set; }

        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DiscoverCount++;
            return Task.FromResult(new CopilotExternalToolLease(tools, ["MCP client test connected."]));
        }
    }

    private sealed class ParallelProbeTool(string name, ParallelInvocationProbe probe) : ICopilotTool
    {
        public string Name { get; } = name;

        public string Description => "Reads one independent deterministic resource.";

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Resource name.", required: true);

        public bool CanHandle(CopilotAgentRequest request) => true;

        public async Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            await probe.EnterAsync(cancellationToken);
            return new CopilotToolResult { ToolName = Name, Success = true, Summary = $"{Name} completed." };
        }
    }

    private sealed class ParallelInvocationProbe(int expectedConcurrentCalls)
    {
        private readonly TaskCompletionSource _allEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        private int _entered;
        private int _maximumActive;

        public int MaximumActive => Volatile.Read(ref _maximumActive);

        public async Task EnterAsync(CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            if (Interlocked.Increment(ref _entered) >= expectedConcurrentCalls)
                _allEntered.TrySetResult();
            try
            {
                await _allEntered.Task.WaitAsync(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
            catch (TimeoutException)
            {
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        private void UpdateMaximum(int value)
        {
            var current = Volatile.Read(ref _maximumActive);
            while (value > current)
            {
                var previous = Interlocked.CompareExchange(ref _maximumActive, value, current);
                if (previous == current)
                    return;
                current = previous;
            }
        }
    }

    private sealed class FrameworkApprovalTestTool : ICopilotFrameworkApprovedTool
    {
        public string Name => "ProtectedWrite";

        public string Description => "Apply a deterministic protected test value.";

        public CopilotToolAccess Access => CopilotToolAccess.Write;

        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.High;

        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Always;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.NonIdempotent;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Protected value.", required: true);

        public int ApprovedExecutionCount { get; private set; }

        public int UnapprovedExecutionCount { get; private set; }

        public CopilotAgentToolInput? LastInput { get; private set; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            UnapprovedExecutionCount++;
            return Task.FromResult(CreateResult(toolInput));
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            ApprovedExecutionCount++;
            return Task.FromResult(CreateResult(toolInput));
        }

        private CopilotToolResult CreateResult(CopilotAgentToolInput toolInput)
        {
            LastInput = toolInput;
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Protected value applied.",
            };
        }
    }

    private sealed class FlakyIdempotentTool(string name) : ICopilotTool
    {
        public string Name { get; } = name;

        public string Description => "Fails transiently once, then succeeds.";

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.Idempotent;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Test value.", required: true);

        public int ExecutionCount { get; private set; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = ExecutionCount > 1,
                Summary = ExecutionCount > 1 ? "Evidence collected after retry." : "Transient network failure.",
                ErrorMessage = ExecutionCount > 1 ? string.Empty : "Temporary network failure.",
                FailureKind = ExecutionCount > 1 ? CopilotToolFailureKind.None : CopilotToolFailureKind.Transient,
            });
        }
    }

    private sealed class PermanentFailureIdempotentTool(string name) : ICopilotTool
    {
        public string Name { get; } = name;

        public string Description => "Returns a permanent validation failure.";

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.Idempotent;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Test value.", required: true);

        public int ExecutionCount { get; private set; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = "Input is invalid.",
                ErrorMessage = "The URL is invalid.",
                FailureKind = CopilotToolFailureKind.Validation,
            });
        }
    }

    private sealed class FlakyApprovedIdempotentTool : ICopilotFrameworkApprovedTool
    {
        public string Name => "ProtectedIdempotentWrite";

        public string Description => "Applies a protected idempotent value.";

        public CopilotToolAccess Access => CopilotToolAccess.Write;

        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.High;

        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Always;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.Idempotent;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Protected value.", required: true);

        public int ApprovedExecutionCount { get; private set; }

        public int UnapprovedExecutionCount { get; private set; }

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            UnapprovedExecutionCount++;
            return Task.FromResult(CreateResult());
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            ApprovedExecutionCount++;
            return Task.FromResult(CreateResult());
        }

        private CopilotToolResult CreateResult()
        {
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = ApprovedExecutionCount > 1,
                Summary = ApprovedExecutionCount > 1 ? "Protected value applied." : "Protected operation failed transiently.",
                ErrorMessage = ApprovedExecutionCount > 1 ? string.Empty : "Temporary application service failure.",
                FailureKind = ApprovedExecutionCount > 1 ? CopilotToolFailureKind.None : CopilotToolFailureKind.Transient,
            };
        }
    }

    private sealed class ThrowingAgentRuntime : ICopilotAgentRuntime
    {
        public Task<CopilotAgentRunResult> RunAsync(CopilotAgentRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken)
        {
            onEvent(CopilotAgentEvent.Status("framework-started"));
            throw new InvalidOperationException("framework unavailable");
        }
    }

    private sealed class ToolStartingThenThrowingAgentRuntime : ICopilotAgentRuntime
    {
        public Task<CopilotAgentRunResult> RunAsync(CopilotAgentRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken)
        {
            onEvent(CopilotAgentEvent.ToolStarted(new CopilotToolExecutionInfo
            {
                CallId = "call-1",
                Round = 1,
                RuntimeName = "agent-framework",
                ToolName = "SetTheme",
                State = CopilotToolExecutionState.Running,
                StartedAtUtc = DateTimeOffset.UtcNow,
            }));
            throw new InvalidOperationException("framework failed after dispatching a tool");
        }
    }

    private sealed class RecordingAgentRuntime(string runtimeName) : ICopilotAgentRuntime
    {
        public int RunCount { get; private set; }

        public Task<CopilotAgentRunResult> RunAsync(CopilotAgentRequest request, Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken)
        {
            RunCount++;
            onEvent(CopilotAgentEvent.Status(runtimeName));
            return Task.FromResult(new CopilotAgentRunResult());
        }
    }

    private sealed class FunctionCallingChatClient : IChatClient
    {
        private readonly string _functionName;
        private readonly IDictionary<string, object?> _arguments;
        private readonly bool _repeatFunctionCallOnce;
        private readonly int _usageTokensPerCall;
        private int _streamCallCount;

        public FunctionCallingChatClient(
            string functionName,
            IDictionary<string, object?>? arguments = null,
            bool repeatFunctionCallOnce = false,
            int usageTokensPerCall = 0)
        {
            _functionName = functionName;
            _arguments = arguments ?? new Dictionary<string, object?> { ["query"] = "https://example.test/" };
            _repeatFunctionCallOnce = repeatFunctionCallOnce;
            _usageTokensPerCall = Math.Max(0, usageTokensPerCall);
        }

        public ChatOptions? LastOptions { get; private set; }

        public int StreamCallCount => Volatile.Read(ref _streamCallCount);

        public Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastOptions = options;
            return Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, "harness answer")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastOptions = options;
            await Task.Yield();

            var callNumber = Interlocked.Increment(ref _streamCallCount);
            if (callNumber == 1 || _repeatFunctionCallOnce && callNumber == 2)
            {
                var contents = new List<AIContent>
                {
                    new FunctionCallContent($"call-{callNumber}", _functionName, _arguments),
                };
                if (_usageTokensPerCall > 0)
                {
                    contents.Add(new UsageContent(new UsageDetails
                    {
                        InputTokenCount = _usageTokensPerCall - 1,
                        OutputTokenCount = 1,
                        TotalTokenCount = _usageTokensPerCall,
                    }));
                }
                yield return new ChatResponseUpdate(ChatRole.Assistant, contents);
                yield break;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, "harness answer");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private static FunctionCallContent CreateTodoAddCall(ChatOptions options, params string[] titleAndDescriptionPairs)
    {
        Assert.True(titleAndDescriptionPairs.Length > 0 && titleAndDescriptionPairs.Length % 2 == 0);
        var function = GetFunction(options, "todos_add");
        var arrayProperty = Assert.Single(function.JsonSchema.GetProperty("properties").EnumerateObject());
        var itemProperties = arrayProperty.Value.GetProperty("items").GetProperty("properties");
        var titleProperty = itemProperties.EnumerateObject().Single(property => property.Name.Equals("title", StringComparison.OrdinalIgnoreCase)).Name;
        var descriptionProperty = itemProperties.EnumerateObject().Single(property => property.Name.Equals("description", StringComparison.OrdinalIgnoreCase)).Name;
        var items = Enumerable.Range(0, titleAndDescriptionPairs.Length / 2)
            .Select(index => (object)new Dictionary<string, object?>
            {
                [titleProperty] = titleAndDescriptionPairs[index * 2],
                [descriptionProperty] = titleAndDescriptionPairs[index * 2 + 1],
            }).ToArray();
        return new FunctionCallContent("todo-add-" + Guid.NewGuid().ToString("N"), function.Name, new Dictionary<string, object?>
        {
            [arrayProperty.Name] = items,
        });
    }

    private static FunctionCallContent CreateTodoCompleteCall(ChatOptions options, int id, string reason)
    {
        var function = GetFunction(options, "todos_complete");
        var arrayProperty = Assert.Single(function.JsonSchema.GetProperty("properties").EnumerateObject());
        var itemProperties = arrayProperty.Value.GetProperty("items").GetProperty("properties");
        var idProperty = itemProperties.EnumerateObject().Single(property => property.Name.Equals("id", StringComparison.OrdinalIgnoreCase)).Name;
        var reasonProperty = itemProperties.EnumerateObject().Single(property => property.Name.Equals("reason", StringComparison.OrdinalIgnoreCase)).Name;
        return new FunctionCallContent("todo-complete-" + Guid.NewGuid().ToString("N"), function.Name, new Dictionary<string, object?>
        {
            [arrayProperty.Name] = new object[]
            {
                new Dictionary<string, object?>
                {
                    [idProperty] = id,
                    [reasonProperty] = reason,
                },
            },
        });
    }

    private static FunctionCallContent CreateModeSetCall(ChatOptions options, string mode)
    {
        var function = GetFunction(options, "mode_set");
        var modeProperty = Assert.Single(function.JsonSchema.GetProperty("properties").EnumerateObject()).Name;
        return new FunctionCallContent("mode-set-" + Guid.NewGuid().ToString("N"), function.Name, new Dictionary<string, object?>
        {
            [modeProperty] = mode,
        });
    }

    private static FunctionCallContent CreateLoadSkillCall(ChatOptions options, string skillName)
    {
        var function = GetFunction(options, "load_skill");
        var nameProperty = Assert.Single(function.JsonSchema.GetProperty("properties").EnumerateObject()).Name;
        return new FunctionCallContent("load-skill-" + Guid.NewGuid().ToString("N"), function.Name, new Dictionary<string, object?>
        {
            [nameProperty] = skillName,
        });
    }

    private static AIFunctionDeclaration GetFunction(ChatOptions options, string name)
    {
        return Assert.IsAssignableFrom<AIFunctionDeclaration>(Assert.Single(options.Tools!, tool => tool.Name == name));
    }

    private sealed class ScriptedHarnessChatClient(params Func<ChatOptions, FunctionCallContent?>[] steps) : IChatClient
    {
        private int _streamCallCount;

        public int StreamCallCount => Volatile.Read(ref _streamCallCount);

        public Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, "scripted harness answer")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.NotNull(options);
            await Task.Yield();
            var callIndex = Interlocked.Increment(ref _streamCallCount) - 1;
            if (callIndex < steps.Length)
            {
                var functionCall = steps[callIndex](options!);
                if (functionCall != null)
                {
                    yield return new ChatResponseUpdate(ChatRole.Assistant, new AIContent[] { functionCall });
                    yield break;
                }
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, "scripted harness answer");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class CapturingFinalChatClient : IChatClient
    {
        public IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? LastMessages { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMessages = messages.ToArray();
            return Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, "compacted answer")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMessages = messages.ToArray();
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "compacted answer");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class BlockingChatClient : IChatClient
    {
        private int _streamCallCount;

        public int StreamCallCount => Volatile.Read(ref _streamCallCount);

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _streamCallCount);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class TodoThenBlockingChatClient : IChatClient
    {
        private readonly TaskCompletionSource<bool> _blockingCallStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _streamCallCount;

        public Task BlockingCallStarted => _blockingCallStarted.Task;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, "unused")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Assert.NotNull(options);
            var callNumber = Interlocked.Increment(ref _streamCallCount);
            if (callNumber == 1)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, new AIContent[]
                {
                    CreateTodoAddCall(options!, "Pause-safe task", "This todo must survive a pause boundary."),
                });
                yield break;
            }

            _blockingCallStarted.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class SteerableChatClient : IChatClient
    {
        private readonly TaskCompletionSource<bool> _firstCallStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseFirstResponse = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _streamCallCount;

        public Task FirstCallStarted => _firstCallStarted.Task;

        public int StreamCallCount => Volatile.Read(ref _streamCallCount);

        public IReadOnlyList<Microsoft.Extensions.AI.ChatMessage>? SecondCallMessages { get; private set; }

        public void ReleaseFirstResponse() => _releaseFirstResponse.TrySetResult(true);

        public Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, "steered answer")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var callNumber = Interlocked.Increment(ref _streamCallCount);
            if (callNumber == 1)
            {
                _firstCallStarted.TrySetResult(true);
                await _releaseFirstResponse.Task.WaitAsync(cancellationToken);
                yield return new ChatResponseUpdate(ChatRole.Assistant, "initial answer");
                yield break;
            }

            SecondCallMessages = messages.ToArray();
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "steered answer");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            _releaseFirstResponse.TrySetCanceled();
        }
    }

    private sealed class BatchFunctionCallingChatClient(params (string FunctionName, string Query)[] calls) : IChatClient
    {
        private int _streamCallCount;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, "harness answer")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            if (Interlocked.Increment(ref _streamCallCount) == 1)
            {
                var contents = calls.Select((call, index) => (AIContent)new FunctionCallContent(
                    $"batch-call-{index + 1}",
                    call.FunctionName,
                    new Dictionary<string, object?> { ["query"] = call.Query })).ToList();
                yield return new ChatResponseUpdate(ChatRole.Assistant, contents);
                yield break;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, "harness answer");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}

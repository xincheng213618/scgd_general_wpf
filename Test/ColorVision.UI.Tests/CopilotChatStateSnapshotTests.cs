using ColorVision.Copilot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public class CopilotChatStateSnapshotTests
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    [Fact]
    public void IncrementalCaptureMatchesExistingStateContract()
    {
        var firstConversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        firstConversation.Title = "First";
        firstConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Question"));
        firstConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Answer"));
        var secondConversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        secondConversation.Title = "Second";
        var state = new CopilotChatState
        {
            ActiveConversationId = firstConversation.Id,
            ActiveProfileId = "profile",
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                firstConversation,
                secondConversation,
            },
            QueuedFollowUpRecoveries = new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>
            {
                new()
                {
                    RunId = "run-1",
                    ConversationId = firstConversation.Id,
                    Prompt = "Continue",
                    ProfileId = "profile",
                    QueuedAtUtc = new DateTimeOffset(2026, 8, 10, 8, 30, 0, TimeSpan.Zero),
                    ResumeAfterRestart = true,
                },
            },
        };
        var store = new CopilotChatStateStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var expected = JObject.Parse(JsonConvert.SerializeObject(state, SerializerSettings));

        var capture = store.BeginSnapshot(state);
        var chunkCount = 0;
        while (capture.CaptureNextChunk())
            chunkCount++;
        var actual = JObject.Parse(store.Serialize(capture.Complete()));

        Assert.True(JToken.DeepEquals(expected, actual));
        Assert.Equal(3, chunkCount);
    }

    [Fact]
    public void StateStoreRoundTripsDurableQueuedFollowUpMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            var queuedAtUtc = new DateTimeOffset(2026, 8, 10, 8, 30, 0, TimeSpan.Zero);
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id,
                ActiveProfileId = "profile",
                Conversations = [conversation],
                QueuedFollowUpRecoveries =
                [
                    new CopilotQueuedFollowUpRecoveryRecord
                    {
                        RunId = "persisted-run",
                        ConversationId = conversation.Id,
                        ProfileId = "profile",
                        QueuedAtUtc = queuedAtUtc,
                        ResumeAfterRestart = true,
                        ComposerState = CopilotComposerStash.Capture(
                            "continue after restart",
                            22,
                            CopilotAgentMode.Code,
                            Array.Empty<CopilotAttachmentItem>()),
                    },
                ],
            };
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var loaded = store.Load();

            var queued = Assert.Single(loaded.QueuedFollowUpRecoveries);
            Assert.Equal("persisted-run", queued.RunId);
            Assert.Equal("profile", queued.ProfileId);
            Assert.Equal(queuedAtUtc, queued.QueuedAtUtc);
            Assert.True(queued.ResumeAfterRestart);
            Assert.Equal(CopilotAgentMode.Code, queued.ComposerState?.RequestMode);
            Assert.Equal("continue after restart", queued.ComposerState?.Text);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CapturePlanKeepsTheStartedConversationSetAndCapturedChunks()
    {
        var firstConversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        firstConversation.Title = "Original";
        var secondConversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var state = new CopilotChatState
        {
            ActiveConversationId = firstConversation.Id,
            ActiveProfileId = "profile",
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                firstConversation,
                secondConversation,
            },
        };
        var store = new CopilotChatStateStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var capture = store.BeginSnapshot(state);

        Assert.True(capture.CaptureNextChunk());
        firstConversation.Title = "Changed after capture";
        state.ActiveConversationId = secondConversation.Id;
        state.Conversations.Add(CopilotConversationRecord.CreateEmpty("profile", "Profile"));
        while (capture.CaptureNextChunk())
        {
        }

        var document = JObject.Parse(store.Serialize(capture.Complete()));
        var conversations = Assert.IsType<JArray>(document[nameof(CopilotChatState.Conversations)]);

        Assert.Equal(2, conversations.Count);
        Assert.Equal("Original", conversations[0]![nameof(CopilotConversationRecord.Title)]!.Value<string>());
        Assert.Equal(firstConversation.Id, document[nameof(CopilotChatState.ActiveConversationId)]!.Value<string>());
    }

    [Fact]
    public void CompleteRejectsAnIncompleteCapture()
    {
        var state = new CopilotChatState
        {
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                CopilotConversationRecord.CreateEmpty("profile", "Profile"),
            },
        };
        var store = new CopilotChatStateStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var capture = store.BeginSnapshot(state);

        var exception = Assert.Throws<InvalidOperationException>(() => capture.Complete());

        Assert.Equal("Copilot state snapshot capture is incomplete.", exception.Message);
    }

    [Fact]
    public void InitializationRepairsDuplicateConversationIdsWithoutChangingActiveIdentity()
    {
        var profile = CopilotProfileConfig.CreateDefault();
        var config = new CopilotConfig
        {
            Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
        };
        var activeConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        var duplicateConversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        duplicateConversation.Id = activeConversation.Id;
        var originalActiveId = activeConversation.Id;
        var state = new CopilotChatState
        {
            ActiveConversationId = originalActiveId,
            ActiveProfileId = profile.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord>
            {
                activeConversation,
                duplicateConversation,
            },
        };

        Assert.True(state.EnsureInitialized(config));

        Assert.Equal(originalActiveId, activeConversation.Id);
        Assert.Equal(originalActiveId, state.ActiveConversationId);
        Assert.NotEqual(activeConversation.Id, duplicateConversation.Id);
        Assert.Equal(2, state.Conversations.Select(conversation => conversation.Id).Distinct(StringComparer.Ordinal).Count());

        CopilotAgentRunStatusSynchronizer.Refresh(
            state.Conversations,
            state.ActiveConversationId,
            CopilotHostedRunState.Running,
            Array.Empty<string>());

        Assert.Equal("运行中", activeConversation.AgentRunStatusLabel);
        Assert.Empty(duplicateConversation.AgentRunStatusLabel);
    }

    [Fact]
    public void ExplicitNeutralPersonalityRoundTripsAndLegacySelectionsBecomeExplicit()
    {
        var explicitNeutral = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        explicitNeutral.ResponsePersonality = CopilotResponsePersonality.None;
        explicitNeutral.HasResponsePersonalityOverride = true;

        string json = JsonConvert.SerializeObject(explicitNeutral, SerializerSettings);
        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(json);

        Assert.Contains(nameof(CopilotConversationRecord.HasResponsePersonalityOverride), json, StringComparison.Ordinal);
        Assert.Null(JObject.Parse(json)[nameof(CopilotConversationRecord.ResponsePersonality)]);
        Assert.NotNull(restored);
        Assert.True(restored.HasResponsePersonalityOverride);
        Assert.Equal(CopilotResponsePersonality.None, restored.ResponsePersonality);

        explicitNeutral.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Question"));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Answer");
        explicitNeutral.Messages.Add(assistant);
        var branch = CopilotConversationBranchService.CreateBranch(explicitNeutral, assistant);
        Assert.True(branch.HasResponsePersonalityOverride);
        Assert.Equal(CopilotResponsePersonality.None, branch.ResponsePersonality);

        var legacy = JsonConvert.DeserializeObject<CopilotConversationRecord>(
            "{\"ResponsePersonality\":2}");
        Assert.NotNull(legacy);
        Assert.False(legacy.HasResponsePersonalityOverride);
        Assert.True(legacy.EnsureValid());
        Assert.True(legacy.HasResponsePersonalityOverride);
        Assert.Equal(CopilotResponsePersonality.Pragmatic, legacy.ResponsePersonality);
    }

    [Fact]
    public void StateStoreRoundTripsACompressedCheckpointSession()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var sessionJson = "{\"content\":\"" + new string('x', 32_000) + "\"}";
            var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            conversation.AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "test-profile",
                SerializedSessionJson = sessionJson,
            };
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id,
                ActiveProfileId = "profile",
                Conversations = new ObservableCollection<CopilotConversationRecord>
                {
                    conversation,
                },
            };
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var serialized = File.ReadAllText(store.StateFilePath);
            var loaded = store.Load();

            Assert.Contains(CopilotAgentSessionCheckpoint.CompressedSerializedSessionPrefix, serialized, StringComparison.Ordinal);
            Assert.True(serialized.Length < sessionJson.Length / 2);
            var loadedConversation = Assert.Single(loaded.Conversations);
            Assert.NotNull(loadedConversation.AgentSessionCheckpoint);
            Assert.Equal(sessionJson, loadedConversation.AgentSessionCheckpoint.SerializedSessionJson);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MessageSnapshotsOmitDefaultsAndDerivedExecutionWithoutLosingRecoveryState()
    {
        var user = new CopilotChatMessage(CopilotChatRole.User, "Inspect the workspace.")
        {
            RequestContent = "Prepared request with captured context.",
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            AssistantName = "test-model",
            RequestMode = CopilotAgentMode.Auto,
            AgentStopReason = CopilotAgentStopReason.Completed,
            IsExecutionExpanded = false,
            IsReasoningExpanded = false,
        };
        assistant.UpsertAgentTrace(new CopilotAgentTraceEntry
        {
            CallId = "call-1",
            Round = 1,
            RuntimeName = "test-runtime",
            ToolName = "ReadLocalFile",
            State = CopilotToolExecutionState.Completed,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(-10),
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DurationMs = 10,
            ResultSummary = "Read the requested file.",
        });
        assistant.RecordResponseTimelineTool("call-1");
        assistant.AppendResponseTimelineText("The file was inspected.");
        var derivedExecutionContent = assistant.ExecutionContent;
        var diagnosticOnlyAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "The run paused.")
        {
            RequestMode = CopilotAgentMode.Auto,
            AgentStopReason = CopilotAgentStopReason.Paused,
            ExecutionContent = "Agent pause requested; preserving the current checkpoint.",
        };
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(user);
        conversation.Messages.Add(assistant);
        conversation.Messages.Add(diagnosticOnlyAssistant);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = "profile",
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        var store = new CopilotChatStateStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var serialized = store.Serialize(state);
        var document = JObject.Parse(serialized);
        var conversationDocument = Assert.IsType<JObject>(
            Assert.IsType<JArray>(document[nameof(CopilotChatState.Conversations)])[0]);
        var messageDocuments = Assert.IsType<JArray>(
            conversationDocument[nameof(CopilotConversationRecord.Messages)]);
        var userDocument = Assert.IsType<JObject>(messageDocuments[0]);
        var assistantDocument = Assert.IsType<JObject>(messageDocuments[1]);
        var diagnosticDocument = Assert.IsType<JObject>(messageDocuments[2]);

        Assert.NotNull(userDocument[nameof(CopilotChatMessage.RequestContent)]);
        Assert.NotNull(userDocument[nameof(CopilotChatMessage.RequestMode)]);
        Assert.Null(userDocument[nameof(CopilotChatMessage.ExecutionContent)]);
        Assert.Null(userDocument[nameof(CopilotChatMessage.AgentTraceEntries)]);
        Assert.Null(userDocument[nameof(CopilotChatMessage.AgentTaskLedger)]);
        Assert.Null(userDocument[nameof(CopilotChatMessage.IsExecutionExpanded)]);
        Assert.Null(userDocument[nameof(CopilotChatMessage.ThinkingStartedAt)]);

        Assert.Null(assistantDocument[nameof(CopilotChatMessage.RequestContent)]);
        Assert.Null(assistantDocument[nameof(CopilotChatMessage.ExecutionContent)]);
        Assert.NotNull(assistantDocument[nameof(CopilotChatMessage.AgentTraceEntries)]);
        Assert.NotNull(assistantDocument[nameof(CopilotChatMessage.ResponseTimelineEvents)]);
        Assert.NotNull(assistantDocument[nameof(CopilotChatMessage.UsesResponseTimeline)]);
        Assert.NotNull(assistantDocument[nameof(CopilotChatMessage.AgentStopReason)]);
        Assert.False(assistantDocument[nameof(CopilotChatMessage.IsExecutionExpanded)]!.Value<bool>());
        Assert.False(assistantDocument[nameof(CopilotChatMessage.IsReasoningExpanded)]!.Value<bool>());

        Assert.Equal(
            diagnosticOnlyAssistant.ExecutionContent,
            diagnosticDocument[nameof(CopilotChatMessage.ExecutionContent)]!.Value<string>());

        var restored = JsonConvert.DeserializeObject<CopilotChatState>(serialized);
        Assert.NotNull(restored);
        var restoredConversation = Assert.Single(restored.Conversations);
        Assert.True(restoredConversation.EnsureValid());
        Assert.Equal(CopilotAgentMode.Auto, restoredConversation.Messages[1].RequestMode);
        Assert.Equal(derivedExecutionContent, restoredConversation.Messages[1].ExecutionContent);
        Assert.False(restoredConversation.Messages[1].IsExecutionExpanded);
        Assert.False(restoredConversation.Messages[1].IsReasoningExpanded);
        Assert.Equal(
            diagnosticOnlyAssistant.ExecutionContent,
            restoredConversation.Messages[2].ExecutionContent);
    }

    [Fact]
    public void TraceAndTimelineSnapshotsOmitDefaultsWithoutChangingRoundTrip()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        assistant.UpsertAgentTrace(new CopilotAgentTraceEntry
        {
            CallId = "call-1",
            Round = 1,
            RuntimeName = "test-runtime",
            ToolName = "ReadLocalFile",
            Idempotency = CopilotToolIdempotency.Idempotent,
            State = CopilotToolExecutionState.Completed,
            StartedAtUtc = DateTimeOffset.Parse("2026-07-26T01:02:03+00:00"),
            CompletedAtUtc = DateTimeOffset.Parse("2026-07-26T01:02:04+00:00"),
            TimeoutMs = 30_000,
            ResultSummary = "Read the requested file.",
            HookRuns =
            [
                new CopilotToolExecutionHookRun
                {
                    SourceId = "test:policy",
                    Phase = CopilotToolExecutionHookPhase.BeforeExecute,
                    State = CopilotToolExecutionHookState.Completed,
                    DurationMs = 2,
                },
                new CopilotToolExecutionHookRun
                {
                    SourceId = "test:audit",
                    Phase = CopilotToolExecutionHookPhase.AfterExecute,
                    State = CopilotToolExecutionHookState.Failed,
                    DurationMs = 3,
                    FailureCode = "tool_hook_failed",
                },
                new CopilotToolExecutionHookRun
                {
                    SourceId = "test:async-audit",
                    Phase = CopilotToolExecutionHookPhase.AfterExecute,
                    ExecutionMode = CopilotToolExecutionHookMode.Async,
                    State = CopilotToolExecutionHookState.Scheduled,
                },
            ],
        });
        assistant.RecordResponseTimelineTool("call-1");
        assistant.AppendResponseTimelineText("The file was inspected.");
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(assistant);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = "profile",
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        var store = new CopilotChatStateStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var serialized = store.Serialize(state);
        var document = JObject.Parse(serialized);
        var messageDocument = Assert.IsType<JObject>(
            document[nameof(CopilotChatState.Conversations)]![0]![nameof(CopilotConversationRecord.Messages)]![0]);
        var traceDocument = Assert.IsType<JObject>(
            messageDocument[nameof(CopilotChatMessage.AgentTraceEntries)]![0]);
        var timelineDocuments = Assert.IsType<JArray>(
            messageDocument[nameof(CopilotChatMessage.ResponseTimelineEvents)]);
        var toolTimelineDocument = Assert.IsType<JObject>(timelineDocuments[0]);
        var markdownTimelineDocument = Assert.IsType<JObject>(timelineDocuments[1]);

        Assert.NotNull(traceDocument[nameof(CopilotAgentTraceEntry.SchemaVersion)]);
        Assert.NotNull(traceDocument[nameof(CopilotAgentTraceEntry.CallId)]);
        Assert.NotNull(traceDocument[nameof(CopilotAgentTraceEntry.Round)]);
        Assert.NotNull(traceDocument[nameof(CopilotAgentTraceEntry.Idempotency)]);
        Assert.NotNull(traceDocument[nameof(CopilotAgentTraceEntry.State)]);
        Assert.NotNull(traceDocument[nameof(CopilotAgentTraceEntry.StartedAtUtc)]);
        Assert.NotNull(traceDocument[nameof(CopilotAgentTraceEntry.CompletedAtUtc)]);
        Assert.NotNull(traceDocument[nameof(CopilotAgentTraceEntry.TimeoutMs)]);
        Assert.NotNull(traceDocument[nameof(CopilotAgentTraceEntry.ResultSummary)]);
        var hookDocuments = Assert.IsType<JArray>(
            traceDocument[nameof(CopilotAgentTraceEntry.HookRuns)]);
        Assert.Equal(3, hookDocuments.Count);
        Assert.Null(hookDocuments[0]![nameof(CopilotToolExecutionHookRun.ExecutionMode)]);
        Assert.Equal(
            (int)CopilotToolExecutionHookMode.Async,
            hookDocuments[2]![nameof(CopilotToolExecutionHookRun.ExecutionMode)]!.Value<int>());
        Assert.Equal(
            (int)CopilotToolExecutionHookState.Scheduled,
            hookDocuments[2]![nameof(CopilotToolExecutionHookRun.State)]!.Value<int>());
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.Attempt)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.MaxAttempts)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.Access)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.RiskLevel)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.ApprovalMode)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.ConcurrencyMode)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.ApprovalActionId)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.FailureKind)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.RetryEligible)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.DurationMs)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.QueueDurationMs)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.ErrorMessage)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.DelegatedRunId)]);
        Assert.Null(traceDocument[nameof(CopilotAgentTraceEntry.DelegatedRequestTokenBudget)]);

        Assert.NotNull(toolTimelineDocument[nameof(CopilotResponseTimelineEvent.SchemaVersion)]);
        Assert.NotNull(toolTimelineDocument[nameof(CopilotResponseTimelineEvent.Kind)]);
        Assert.NotNull(toolTimelineDocument[nameof(CopilotResponseTimelineEvent.CallId)]);
        Assert.Null(toolTimelineDocument[nameof(CopilotResponseTimelineEvent.ContentStart)]);
        Assert.Null(toolTimelineDocument[nameof(CopilotResponseTimelineEvent.ContentLength)]);
        Assert.NotNull(markdownTimelineDocument[nameof(CopilotResponseTimelineEvent.SchemaVersion)]);
        Assert.Null(markdownTimelineDocument[nameof(CopilotResponseTimelineEvent.Kind)]);
        Assert.Null(markdownTimelineDocument[nameof(CopilotResponseTimelineEvent.ContentStart)]);
        Assert.NotNull(markdownTimelineDocument[nameof(CopilotResponseTimelineEvent.ContentLength)]);
        Assert.Null(markdownTimelineDocument[nameof(CopilotResponseTimelineEvent.CallId)]);

        var restored = JsonConvert.DeserializeObject<CopilotChatState>(serialized);

        Assert.NotNull(restored);
        var restoredConversation = Assert.Single(restored.Conversations);
        Assert.True(restoredConversation.EnsureValid());
        var restoredMessage = Assert.Single(restoredConversation.Messages);
        var restoredTrace = Assert.Single(restoredMessage.AgentTraceEntries);
        Assert.Equal(1, restoredTrace.Attempt);
        Assert.Equal(1, restoredTrace.MaxAttempts);
        Assert.Equal(CopilotToolAccess.ReadOnly, restoredTrace.Access);
        Assert.Equal(CopilotToolRiskLevel.Low, restoredTrace.RiskLevel);
        Assert.Equal(CopilotToolApprovalMode.Never, restoredTrace.ApprovalMode);
        Assert.Equal(CopilotToolConcurrencyMode.SharedRead, restoredTrace.ConcurrencyMode);
        Assert.Equal(CopilotToolIdempotency.Idempotent, restoredTrace.Idempotency);
        Assert.Equal(CopilotToolExecutionState.Completed, restoredTrace.State);
        Assert.Equal(30_000, restoredTrace.TimeoutMs);
        Assert.Equal("Read the requested file.", restoredTrace.ResultSummary);
        Assert.Collection(
            restoredTrace.HookRuns,
            hookRun =>
            {
                Assert.Equal("test:policy", hookRun.SourceId);
                Assert.Equal(CopilotToolExecutionHookState.Completed, hookRun.State);
            },
            hookRun =>
            {
                Assert.Equal("test:audit", hookRun.SourceId);
                Assert.Equal(CopilotToolExecutionHookState.Failed, hookRun.State);
                Assert.Equal("tool_hook_failed", hookRun.FailureCode);
            },
            hookRun =>
            {
                Assert.Equal("test:async-audit", hookRun.SourceId);
                Assert.Equal(CopilotToolExecutionHookMode.Async, hookRun.ExecutionMode);
                Assert.Equal(CopilotToolExecutionHookState.Scheduled, hookRun.State);
            });
        Assert.Equal(2, restoredMessage.ResponseTimelineEvents.Count);
        Assert.Equal(CopilotResponseTimelineEventKind.ToolCall, restoredMessage.ResponseTimelineEvents[0].Kind);
        Assert.Equal("call-1", restoredMessage.ResponseTimelineEvents[0].CallId);
        Assert.Equal(CopilotResponseTimelineEventKind.Markdown, restoredMessage.ResponseTimelineEvents[1].Kind);
        Assert.Equal(0, restoredMessage.ResponseTimelineEvents[1].ContentStart);
        Assert.Equal("The file was inspected.".Length, restoredMessage.ResponseTimelineEvents[1].ContentLength);
    }

    [Fact]
    public void SaveAndLoadPreservesActiveWorkspaceRollbackAuthority()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var now = DateTimeOffset.UtcNow;
            var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Applied the requested change.");
            assistant.UpsertAgentTrace(CreateWorkspaceApplyTrace(now));
            var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            conversation.Messages.Add(assistant);
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id,
                ActiveProfileId = "profile",
                Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            };
            var store = new CopilotChatStateStore(root);

            store.Save(state);

            var persistedDocument = JObject.Parse(File.ReadAllText(store.StateFilePath));
            var persistedTrace = persistedDocument[nameof(CopilotChatState.Conversations)]![0]!
                [nameof(CopilotConversationRecord.Messages)]![0]!
                [nameof(CopilotChatMessage.AgentTraceEntries)]![0]!;
            Assert.Equal(
                "workspace-change-set:11111111111111111111111111111111",
                persistedTrace[nameof(CopilotAgentTraceEntry.WorkspaceChangeSetId)]!.Value<string>());
            Assert.NotNull(persistedTrace[nameof(CopilotAgentTraceEntry.WorkspaceChangeSetExpiresAtUtc)]);

            var restored = new CopilotChatStateStore(root).Load();
            var restoredTrace = Assert.Single(
                Assert.Single(Assert.Single(restored.Conversations).Messages).AgentTraceEntries);
            Assert.Equal(
                "workspace-change-set:11111111111111111111111111111111",
                restoredTrace.WorkspaceChangeSetId);
            Assert.Equal(now.AddMinutes(20), restoredTrace.WorkspaceChangeSetExpiresAtUtc);
            Assert.True(restoredTrace.CanRequestWorkspaceRollback);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TraceValidationDropsOverlongWorkspaceRollbackAuthority()
    {
        var now = DateTimeOffset.UtcNow;
        var traceDocument = JObject.Parse(JsonConvert.SerializeObject(CreateWorkspaceApplyTrace(now)));
        traceDocument[nameof(CopilotAgentTraceEntry.WorkspaceChangeSetExpiresAtUtc)] = now.AddHours(1);

        var restoredTrace = traceDocument.ToObject<CopilotAgentTraceEntry>();

        Assert.NotNull(restoredTrace);
        Assert.True(restoredTrace.EnsureValid(now));
        Assert.Empty(restoredTrace.WorkspaceChangeSetId);
        Assert.Null(restoredTrace.WorkspaceChangeSetExpiresAtUtc);
        Assert.False(restoredTrace.CanRequestWorkspaceRollback);
    }

    [Fact]
    public void MessageSnapshotCompressesLargeRequestContentWithoutChangingRuntimeContracts()
    {
        var requestContent = "# Captured context\n" + new string('x', 32_000);
        var message = new CopilotChatMessage(CopilotChatRole.User, "Inspect the captured context.")
        {
            RequestContent = requestContent,
        };
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(message);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = "profile",
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        var store = new CopilotChatStateStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var serialized = store.Serialize(state);
        var document = JObject.Parse(serialized);
        var persistedRequestContent = document[nameof(CopilotChatState.Conversations)]![0]!
            [nameof(CopilotConversationRecord.Messages)]![0]!
            [nameof(CopilotChatMessage.RequestContent)]!
            .Value<string>();
        var restored = JsonConvert.DeserializeObject<CopilotChatState>(serialized);
        var systemTextJson = System.Text.Json.JsonSerializer.Serialize(message);
        using var systemTextDocument = System.Text.Json.JsonDocument.Parse(systemTextJson);

        Assert.NotNull(persistedRequestContent);
        Assert.StartsWith(CopilotChatMessage.CompressedRequestContentPrefix, persistedRequestContent, StringComparison.Ordinal);
        Assert.True(persistedRequestContent.Length < requestContent.Length / 2);
        Assert.NotNull(restored);
        Assert.Equal(requestContent, Assert.Single(Assert.Single(restored.Conversations).Messages).RequestContent);
        Assert.Equal(
            requestContent,
            systemTextDocument.RootElement.GetProperty(nameof(CopilotChatMessage.RequestContent)).GetString());
        Assert.DoesNotContain(CopilotChatMessage.CompressedRequestContentPrefix, systemTextJson, StringComparison.Ordinal);
    }

    [Fact]
    public void MessageSnapshotMigratesLegacyRequestContentAndEscapesPrefixCollisions()
    {
        var legacyRequestContent = "# Legacy context\n" + new string('y', 32_000);
        var legacyDocument = "{\"Role\":0,\"Content\":\"Question\",\"RequestContent\":"
            + JsonConvert.SerializeObject(legacyRequestContent)
            + "}";

        var restoredLegacy = JsonConvert.DeserializeObject<CopilotChatMessage>(legacyDocument);
        var migratedDocument = JsonConvert.SerializeObject(restoredLegacy);
        var prefixCollision = CopilotChatMessage.CompressedRequestContentPrefix + "literal user text";
        var collisionMessage = new CopilotChatMessage(CopilotChatRole.User, "Question")
        {
            RequestContent = prefixCollision,
        };
        var collisionDocument = JObject.Parse(JsonConvert.SerializeObject(collisionMessage));
        var collisionPayload = collisionDocument[nameof(CopilotChatMessage.RequestContent)]!.Value<string>();
        var restoredCollision = JsonConvert.DeserializeObject<CopilotChatMessage>(collisionDocument.ToString(Formatting.None));

        Assert.NotNull(restoredLegacy);
        Assert.Equal(legacyRequestContent, restoredLegacy.RequestContent);
        Assert.Contains(CopilotChatMessage.CompressedRequestContentPrefix, migratedDocument, StringComparison.Ordinal);
        Assert.True(migratedDocument.Length < legacyDocument.Length / 2);
        Assert.NotEqual(prefixCollision, collisionPayload);
        Assert.StartsWith(CopilotChatMessage.CompressedRequestContentPrefix, collisionPayload, StringComparison.Ordinal);
        Assert.NotNull(restoredCollision);
        Assert.Equal(prefixCollision, restoredCollision.RequestContent);
    }

    [Fact]
    public void LoadDiscardsOlderTemporaryStateAndKeepsNewerPrimary()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CopilotChatStateStore(root);
            store.Save(CreateState("Current"));
            File.WriteAllText(store.TemporaryStateFilePath, store.Serialize(CreateState("Stale")));
            File.SetLastWriteTimeUtc(
                store.TemporaryStateFilePath,
                File.GetLastWriteTimeUtc(store.StateFilePath).AddMinutes(-1));

            var loadStore = new CopilotChatStateStore(root);
            var loaded = loadStore.Load();

            Assert.Equal(CopilotChatStateLoadSource.Primary, loadStore.LastLoadStatus.Source);
            Assert.Equal("Current", Assert.Single(loaded.Conversations).Title);
            Assert.False(File.Exists(store.TemporaryStateFilePath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(CopilotChatState.CurrentSchemaVersion + 1L, CopilotChatState.CurrentSchemaVersion + 1)]
    [InlineData((long)int.MaxValue + 1, int.MaxValue)]
    public async Task FutureSchemaStateBlocksFallbackAndAllWrites(
        long futureSchemaVersion,
        int reportedSchemaVersion)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CopilotChatStateStore(root);
            Directory.CreateDirectory(store.StateDirectoryPath);
            var futureDocument = CreateStateDocument(futureSchemaVersion, "Future history");
            var backupDocument = CreateStateDocument(CopilotChatState.CurrentSchemaVersion, "Older backup");
            File.WriteAllText(store.StateFilePath, futureDocument.ToString(Formatting.None));
            File.WriteAllText(store.BackupStateFilePath, backupDocument.ToString(Formatting.None));
            var originalPrimary = File.ReadAllText(store.StateFilePath);
            var originalBackup = File.ReadAllText(store.BackupStateFilePath);

            var loaded = store.Load();
            var replacement = CreateState("Replacement");
            var serializedReplacement = store.Serialize(replacement);

            Assert.Equal(CopilotChatStateLoadSource.FutureVersion, store.LastLoadStatus.Source);
            Assert.Equal(reportedSchemaVersion, store.LastLoadStatus.SchemaVersion);
            Assert.True(store.IsStatePersistenceBlocked);
            Assert.Empty(loaded.Conversations);
            Assert.Throws<CopilotChatStateFutureVersionException>(() => store.Save(replacement));
            await Assert.ThrowsAsync<CopilotChatStateFutureVersionException>(
                () => store.SaveSerializedAsync(serializedReplacement));
            Assert.Equal(originalPrimary, File.ReadAllText(store.StateFilePath));
            Assert.Equal(originalBackup, File.ReadAllText(store.BackupStateFilePath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SaveStopsWhenAnotherProcessWritesAFutureSchema()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CopilotChatStateStore(root);
            store.Save(CreateState("Original"));
            var loaded = store.Load();
            Assert.Equal("Original", Assert.Single(loaded.Conversations).Title);

            var futureDocument = CreateStateDocument(CopilotChatState.CurrentSchemaVersion + 1, "Newer process");
            File.WriteAllText(store.StateFilePath, futureDocument.ToString(Formatting.None));
            var originalPrimary = File.ReadAllText(store.StateFilePath);

            var exception = Assert.Throws<CopilotChatStateFutureVersionException>(
                () => store.Save(CreateState("Older process")));

            Assert.Equal(CopilotChatState.CurrentSchemaVersion + 1, exception.SchemaVersion);
            Assert.True(store.IsStatePersistenceBlocked);
            Assert.Equal(originalPrimary, File.ReadAllText(store.StateFilePath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SavesCreateThrottledRecoverySnapshots()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CopilotChatStateStore(root);

            store.Save(CreateState("First"));
            store.Save(CreateState("Second"));
            store.Save(CreateState("Third"));

            var recoveryFiles = Directory.GetFiles(
                store.RecoveryStateDirectoryPath,
                "chat-state-backup-*.json",
                SearchOption.TopDirectoryOnly);
            var recoveryDocument = JObject.Parse(File.ReadAllText(Assert.Single(recoveryFiles)));
            var recoveryConversation = Assert.IsType<JObject>(
                Assert.IsType<JArray>(recoveryDocument[nameof(CopilotChatState.Conversations)])[0]);

            Assert.Equal("First", recoveryConversation[nameof(CopilotConversationRecord.Title)]!.Value<string>());
            Assert.Equal("Third", Assert.Single(store.Load().Conversations).Title);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFallsBackToARecoverySnapshotAfterPrimaryAndBackupFail()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CopilotChatStateStore(root);
            store.Save(CreateState("Recover me"));
            store.Save(CreateState("Latest"));
            File.WriteAllText(store.StateFilePath, "{broken-primary");
            File.WriteAllText(store.BackupStateFilePath, "{broken-backup");

            var recoveryStore = new CopilotChatStateStore(root);
            var recovered = recoveryStore.Load();

            Assert.Equal(CopilotChatStateLoadSource.RecoverySnapshot, recoveryStore.LastLoadStatus.Source);
            Assert.Equal("Recover me", Assert.Single(recovered.Conversations).Title);

            var verificationStore = new CopilotChatStateStore(root);
            var verified = verificationStore.Load();
            Assert.Equal(CopilotChatStateLoadSource.Primary, verificationStore.LastLoadStatus.Source);
            Assert.Equal("Recover me", Assert.Single(verified.Conversations).Title);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static CopilotChatState CreateState(string title)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = title;
        return new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = "profile",
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
    }

    private static CopilotAgentTraceEntry CreateWorkspaceApplyTrace(DateTimeOffset now)
    {
        return CopilotAgentTraceEntry.FromResult(
            new CopilotToolExecutionInfo
            {
                CallId = "workspace-apply",
                Round = 1,
                ToolName = "ApplyWorkspacePatchEnvelope",
                State = CopilotToolExecutionState.Completed,
                StartedAtUtc = now.AddSeconds(-1),
                CompletedAtUtc = now,
            },
            new CopilotToolResult
            {
                ToolName = "ApplyWorkspacePatchEnvelope",
                Success = true,
                Summary = "Applied one workspace change set.",
                Content = string.Join(
                    Environment.NewLine,
                    "[Workspace Change Set Result]",
                    "change_set_id: workspace-change-set:11111111111111111111111111111111",
                    "file_count: 1",
                    "state: Applied",
                    $"expires_at_utc: {now.AddMinutes(20):O}",
                    "file_1_operation: Update",
                    @"file_1_path: C:\workspace\target.txt",
                    "file_1_before_sha256: before",
                    "file_1_after_sha256: after"),
            });
    }

    private static JObject CreateStateDocument(long schemaVersion, string title)
    {
        return new JObject
        {
            [nameof(CopilotChatState.SchemaVersion)] = schemaVersion,
            [nameof(CopilotChatState.ActiveConversationId)] = "conversation",
            [nameof(CopilotChatState.ActiveProfileId)] = "profile",
            [nameof(CopilotChatState.Conversations)] = new JArray
            {
                new JObject
                {
                    [nameof(CopilotConversationRecord.Id)] = "conversation",
                    [nameof(CopilotConversationRecord.Title)] = title,
                    [nameof(CopilotConversationRecord.Messages)] = new JArray(),
                    [nameof(CopilotConversationRecord.Attachments)] = new JArray(),
                },
            },
        };
    }
}

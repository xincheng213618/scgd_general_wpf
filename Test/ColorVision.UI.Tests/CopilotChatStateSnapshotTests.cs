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
}

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
}

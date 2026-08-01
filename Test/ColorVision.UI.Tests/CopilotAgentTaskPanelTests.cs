using ColorVision.Copilot;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTaskPanelTests
{
    [Fact]
    public void TaskPanelStartsExpandedAndOmitsDefaultPreference()
    {
        var state = CreateState();

        var document = JObject.FromObject(state);

        Assert.True(state.IsAgentTaskPanelExpanded);
        Assert.Null(document[nameof(CopilotChatState.IsAgentTaskPanelExpanded)]);
    }

    [Fact]
    public void CollapsedTaskPanelPreferenceSurvivesRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var state = CreateState();
            var expanded = state.ToggleAgentTaskPanelExpanded();
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var document = JObject.Parse(File.ReadAllText(store.StateFilePath));
            var restored = new CopilotChatStateStore(root).Load();

            Assert.False(expanded);
            Assert.False(document[nameof(CopilotChatState.IsAgentTaskPanelExpanded)]!.Value<bool>());
            Assert.False(restored.IsAgentTaskPanelExpanded);
            Assert.Equal(CopilotChatState.CurrentSchemaVersion, restored.SchemaVersion);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static CopilotChatState CreateState()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        return new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = "profile",
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
    }
}

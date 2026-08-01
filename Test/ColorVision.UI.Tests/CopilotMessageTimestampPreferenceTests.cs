using ColorVision.Copilot;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotMessageTimestampPreferenceTests
{
    [Fact]
    public void TimestampsCommandOffersExplicitStatesAndRunsDuringAgentWork()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/timestamps off");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Timestamps, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal(["on", "off"], invocation.Command.Arguments!.Select(item => item.Value));
        Assert.Equal("off", invocation.Arguments);
    }

    [Theory]
    [InlineData("", true, false)]
    [InlineData("", false, true)]
    [InlineData("on", false, true)]
    [InlineData("OFF", true, false)]
    public void ResolverSupportsToggleAndExplicitStates(
        string arguments,
        bool currentlyVisible,
        bool expectedVisible)
    {
        Assert.True(CopilotMessageTimestampPreference.TryResolve(
            arguments,
            currentlyVisible,
            out var visible));
        Assert.Equal(expectedVisible, visible);
    }

    [Fact]
    public void InvalidArgumentKeepsTheCurrentState()
    {
        Assert.False(CopilotMessageTimestampPreference.TryResolve(
            "status",
            currentlyVisible: true,
            out var visible));
        Assert.True(visible);
        Assert.Contains("/timestamps [on|off]", CopilotMessageTimestampPreference.Usage);
    }

    [Fact]
    public void HiddenPreferenceSurvivesRestartWhileDefaultIsOmitted()
    {
        var defaultState = CreateState();
        Assert.True(defaultState.ShowMessageTimestamps);
        Assert.Null(JObject.FromObject(defaultState)[nameof(CopilotChatState.ShowMessageTimestamps)]);

        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var state = CreateState();
            Assert.True(state.SetShowMessageTimestamps(false));
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var document = JObject.Parse(File.ReadAllText(store.StateFilePath));
            var restored = new CopilotChatStateStore(root).Load();

            Assert.False(document[nameof(CopilotChatState.ShowMessageTimestamps)]!.Value<bool>());
            Assert.False(restored.ShowMessageTimestamps);
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

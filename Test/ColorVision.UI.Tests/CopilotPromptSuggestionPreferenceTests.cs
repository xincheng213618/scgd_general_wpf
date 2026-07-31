using ColorVision.Copilot;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotPromptSuggestionPreferenceTests
{
    [Fact]
    public void SuggestionsCommandOffersExplicitStatesAndRunsDuringAgentWork()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/suggestions off");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.PromptSuggestions, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal(
            ["on", "off"],
            invocation.Command.Arguments!.Select(item => item.Value));
        Assert.Equal("off", invocation.Arguments);
    }

    [Theory]
    [InlineData("", true, false)]
    [InlineData("", false, true)]
    [InlineData("on", false, true)]
    [InlineData("OFF", true, false)]
    public void ResolverSupportsToggleAndExplicitStates(
        string arguments,
        bool currentlyEnabled,
        bool expectedEnabled)
    {
        Assert.True(CopilotPromptSuggestionPreference.TryResolve(
            arguments,
            currentlyEnabled,
            out var enabled));
        Assert.Equal(expectedEnabled, enabled);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("predict-on")]
    [InlineData("predict-model current")]
    public void InvalidAndRemovedArgumentsKeepTheCurrentState(string arguments)
    {
        Assert.False(CopilotPromptSuggestionPreference.TryResolve(
            arguments,
            currentlyEnabled: true,
            out var enabled));
        Assert.True(enabled);
        Assert.Contains("[on|off]", CopilotPromptSuggestionPreference.Usage);
    }

    [Fact]
    public void DisabledPreferenceSurvivesRestartWhileDefaultIsOmitted()
    {
        var defaultState = CreateState();
        Assert.True(defaultState.EnablePromptHistoryCompletions);
        Assert.Null(JObject.FromObject(defaultState)[nameof(CopilotChatState.EnablePromptHistoryCompletions)]);

        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var state = CreateState();
            Assert.True(state.SetEnablePromptHistoryCompletions(false));
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var document = JObject.Parse(File.ReadAllText(store.StateFilePath));
            var restored = new CopilotChatStateStore(root).Load();

            Assert.False(document[nameof(CopilotChatState.EnablePromptHistoryCompletions)]!.Value<bool>());
            Assert.False(restored.EnablePromptHistoryCompletions);
            Assert.Equal(CopilotChatState.CurrentSchemaVersion, restored.SchemaVersion);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LegacyPredictionSettingsAreIgnoredAndDroppedOnNextSave()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CopilotChatStateStore(root);
            store.Save(CreateState());
            var legacyDocument = JObject.Parse(File.ReadAllText(store.StateFilePath));
            legacyDocument["EnablePredictedPromptSuggestions"] = true;
            legacyDocument["PredictedPromptProfileId"] = "legacy-profile";
            File.WriteAllText(store.StateFilePath, legacyDocument.ToString());

            var restored = new CopilotChatStateStore(root).Load();
            store.Save(restored);
            var normalizedDocument = JObject.Parse(File.ReadAllText(store.StateFilePath));

            Assert.Equal(CopilotChatState.CurrentSchemaVersion, restored.SchemaVersion);
            Assert.Null(normalizedDocument["EnablePredictedPromptSuggestions"]);
            Assert.Null(normalizedDocument["PredictedPromptProfileId"]);
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

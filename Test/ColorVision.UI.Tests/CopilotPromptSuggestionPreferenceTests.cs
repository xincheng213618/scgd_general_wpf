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
            ["on", "off", "predict-on", "predict-off"],
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

    [Fact]
    public void InvalidArgumentKeepsTheCurrentState()
    {
        Assert.False(CopilotPromptSuggestionPreference.TryResolve(
            "status",
            currentlyEnabled: true,
            out var enabled));
        Assert.True(enabled);
        Assert.Contains("/suggestions [on|off|predict-on|predict-off]", CopilotPromptSuggestionPreference.Usage);
    }

    [Theory]
    [InlineData("predict-on", false, true)]
    [InlineData("PREDICT-OFF", true, false)]
    public void PredictedResolverSupportsExplicitOptInAndOptOut(
        string arguments,
        bool currentlyEnabled,
        bool expectedEnabled)
    {
        Assert.True(CopilotPromptSuggestionPreference.TryResolvePredicted(
            arguments,
            currentlyEnabled,
            out var enabled));
        Assert.Equal(expectedEnabled, enabled);
    }

    [Fact]
    public void PredictedResolverDoesNotConsumeHistoryArguments()
    {
        Assert.False(CopilotPromptSuggestionPreference.TryResolvePredicted(
            "on",
            currentlyEnabled: false,
            out var enabled));
        Assert.False(enabled);
    }

    [Fact]
    public void DisabledPreferenceSurvivesRestartWhileDefaultIsOmitted()
    {
        var defaultState = CreateState();
        Assert.True(defaultState.EnablePromptHistoryCompletions);
        Assert.False(defaultState.EnablePredictedPromptSuggestions);
        Assert.Null(JObject.FromObject(defaultState)[nameof(CopilotChatState.EnablePromptHistoryCompletions)]);
        Assert.Null(JObject.FromObject(defaultState)[nameof(CopilotChatState.EnablePredictedPromptSuggestions)]);

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
    public void PredictedOptInSurvivesRestartWhileDefaultIsOmitted()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var state = CreateState();
            Assert.True(state.SetEnablePredictedPromptSuggestions(true));
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var document = JObject.Parse(File.ReadAllText(store.StateFilePath));
            var restored = new CopilotChatStateStore(root).Load();

            Assert.True(document[nameof(CopilotChatState.EnablePredictedPromptSuggestions)]!.Value<bool>());
            Assert.True(restored.EnablePredictedPromptSuggestions);
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

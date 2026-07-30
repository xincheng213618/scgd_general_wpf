using ColorVision.Copilot;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotMultilineComposerPreferenceTests
{
    [Theory]
    [InlineData("/multiline off", "off")]
    [InlineData("/ml on", "on")]
    public void MultilineCommandsOfferExplicitStatesAndRunDuringAgentWork(
        string input,
        string expectedArguments)
    {
        var invocation = CopilotLocalCommandCatalog.Parse(input);

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.MultilineComposer, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal(["on", "off"], invocation.Command.Arguments!.Select(item => item.Value));
        Assert.Equal(expectedArguments, invocation.Arguments);
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
        Assert.True(CopilotMultilineComposerPreference.TryResolve(
            arguments,
            currentlyEnabled,
            out var enabled));
        Assert.Equal(expectedEnabled, enabled);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, true)]
    public void EnterGestureKeepsControlEnterAsStableSubmitFallback(
        bool multilineEnabled,
        bool shiftPressed,
        bool controlPressed,
        bool shouldSubmit)
    {
        Assert.Equal(
            shouldSubmit
                ? CopilotComposerEnterAction.Submit
                : CopilotComposerEnterAction.InsertLine,
            CopilotMultilineComposerPreference.ResolveEnterAction(
                multilineEnabled,
                shiftPressed,
                controlPressed));
    }

    [Fact]
    public void InvalidArgumentKeepsTheCurrentState()
    {
        Assert.False(CopilotMultilineComposerPreference.TryResolve(
            "status",
            currentlyEnabled: true,
            out var enabled));
        Assert.True(enabled);
        Assert.Contains("/multiline [on|off]", CopilotMultilineComposerPreference.Usage);
    }

    [Fact]
    public void EnabledPreferenceSurvivesRestartWhileDefaultIsOmitted()
    {
        var defaultState = CreateState();
        Assert.False(defaultState.UseMultilineComposer);
        Assert.Null(JObject.FromObject(defaultState)[nameof(CopilotChatState.UseMultilineComposer)]);

        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var state = CreateState();
            Assert.True(state.SetUseMultilineComposer(true));
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var document = JObject.Parse(File.ReadAllText(store.StateFilePath));
            var restored = new CopilotChatStateStore(root).Load();

            Assert.True(document[nameof(CopilotChatState.UseMultilineComposer)]!.Value<bool>());
            Assert.True(restored.UseMultilineComposer);
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

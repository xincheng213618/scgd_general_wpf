using ColorVision.Copilot;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotFollowUpPreferenceTests
{
    [Theory]
    [InlineData("", CopilotFollowUpBehavior.Queue)]
    [InlineData("steer", CopilotFollowUpBehavior.Steer)]
    [InlineData("STEER", CopilotFollowUpBehavior.Steer)]
    [InlineData("queue", CopilotFollowUpBehavior.Queue)]
    public void ResolvesSupportedArguments(string arguments, CopilotFollowUpBehavior expected)
    {
        Assert.True(CopilotFollowUpPreference.TryResolve(
            arguments,
            CopilotFollowUpBehavior.Queue,
            out var behavior));

        Assert.Equal(expected, behavior);
    }

    [Fact]
    public void InvalidArgumentKeepsCurrentBehavior()
    {
        Assert.False(CopilotFollowUpPreference.TryResolve(
            "later",
            CopilotFollowUpBehavior.Queue,
            out var behavior));

        Assert.Equal(CopilotFollowUpBehavior.Queue, behavior);
    }

    [Theory]
    [InlineData(CopilotFollowUpBehavior.Steer, CopilotFollowUpBehavior.Queue)]
    [InlineData(CopilotFollowUpBehavior.Queue, CopilotFollowUpBehavior.Steer)]
    [InlineData((CopilotFollowUpBehavior)99, CopilotFollowUpBehavior.Queue)]
    public void AlternateAlwaysReturnsTheOtherSupportedBehavior(
        CopilotFollowUpBehavior behavior,
        CopilotFollowUpBehavior expected)
    {
        Assert.Equal(expected, CopilotFollowUpPreference.Alternate(behavior));
    }

    [Fact]
    public void CommandCatalogExposesBothBehaviorArguments()
    {
        var command = Assert.Single(
            CopilotLocalCommandCatalog.All,
            item => item.Name == "/follow-up");

        Assert.Equal(CopilotLocalCommandKind.FollowUpBehavior, command.Kind);
        Assert.True(command.AvailableWhileAgentRuns);
        Assert.Equal(
            ["steer", "queue"],
            command.Arguments!.Select(argument => argument.Value).ToArray());
        Assert.Equal(
            "queue",
            CopilotLocalCommandCatalog.Parse("/follow-up queue")!.Arguments);
    }

    [Fact]
    public void StateStoreRoundTripsQueuedDefaultAndOmitsSteerDefault()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"copilot-follow-up-preference-{Guid.NewGuid():N}");
        try
        {
            var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id,
                ActiveProfileId = "profile",
                Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
                DefaultFollowUpBehavior = CopilotFollowUpBehavior.Queue,
            };
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var persisted = JObject.Parse(File.ReadAllText(store.StateFilePath));
            var loaded = store.Load();

            Assert.Equal(
                (int)CopilotFollowUpBehavior.Queue,
                persisted[nameof(CopilotChatState.DefaultFollowUpBehavior)]!.Value<int>());
            Assert.Equal(CopilotFollowUpBehavior.Queue, loaded.DefaultFollowUpBehavior);

            loaded.DefaultFollowUpBehavior = CopilotFollowUpBehavior.Steer;
            var defaultDocument = JObject.Parse(store.Serialize(loaded));
            Assert.Null(defaultDocument[nameof(CopilotChatState.DefaultFollowUpBehavior)]);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StateInitializationNormalizesUnknownBehavior()
    {
        var config = new CopilotConfig();
        config.EnsureInitialized();
        var conversation = CopilotConversationRecord.CreateEmpty(string.Empty, string.Empty);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            DefaultFollowUpBehavior = (CopilotFollowUpBehavior)99,
        };

        Assert.True(state.EnsureInitialized(config));

        Assert.Equal(CopilotFollowUpBehavior.Steer, state.DefaultFollowUpBehavior);
    }
}

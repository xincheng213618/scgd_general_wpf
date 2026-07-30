using ColorVision.Copilot;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationArchiveTests
{
    [Theory]
    [InlineData("/archive", CopilotLocalCommandKind.ArchiveConversation)]
    [InlineData("/unarchive session", CopilotLocalCommandKind.UnarchiveConversation)]
    [InlineData("/archived", CopilotLocalCommandKind.UnarchiveConversation)]
    public void ArchiveCommandsUseRecoverableConversationWorkflow(
        string input,
        CopilotLocalCommandKind expectedKind)
    {
        var invocation = CopilotLocalCommandCatalog.Parse(input);

        Assert.NotNull(invocation);
        Assert.Equal(expectedKind, invocation.Command.Kind);
        Assert.False(invocation.Command.AvailableWhileAgentRuns);
    }

    [Fact]
    public void ActiveFilterAndArchivedLookupPreserveHiddenConversation()
    {
        var active = CreateConversation("active", "Active", archived: false);
        var archived = CreateConversation("archived", "Archived", archived: true);
        var conversations = new[] { archived, active };

        Assert.Equal([active], CopilotConversationArchiveService.GetActive(conversations));
        Assert.Same(
            archived,
            CopilotConversationArchiveService.FindUniqueArchived(conversations, archived.Id));
        Assert.Same(
            archived,
            CopilotConversationArchiveService.FindUniqueArchived(conversations, "Archived"));
        Assert.Contains(archived.Id, CopilotConversationArchiveService.FormatArchived(conversations));
        Assert.DoesNotContain(active.Id, CopilotConversationArchiveService.FormatArchived(conversations));
    }

    [Fact]
    public void DuplicateArchivedTitlesRequireAnId()
    {
        var first = CreateConversation("one", "Repeated", archived: true);
        var second = CreateConversation("two", "Repeated", archived: true);

        Assert.Null(CopilotConversationArchiveService.FindUniqueArchived(
            [first, second],
            "Repeated"));
        Assert.Same(first, CopilotConversationArchiveService.FindUniqueArchived(
            [first, second],
            first.Id));
    }

    [Fact]
    public void ArchivedEmptyConversationIsNeverReusedForNewWork()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.IsArchived = true;

        Assert.False(CopilotConversationService.IsReusableEmpty(conversation));
    }

    [Fact]
    public void ArchivedStateSurvivesRestartWithoutLosingMessagesOrDraft()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var conversation = CreateConversation("archive-id", "Archived", archived: true);
            conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "keep this message"));
            conversation.DraftText = "keep this draft";
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id,
                ActiveProfileId = "profile",
                Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            };
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var document = JObject.Parse(File.ReadAllText(store.StateFilePath));
            var restored = new CopilotChatStateStore(root).Load();
            var restoredConversation = Assert.Single(restored.Conversations);

            Assert.True(document[nameof(CopilotChatState.Conversations)]![0]![nameof(CopilotConversationRecord.IsArchived)]!.Value<bool>());
            Assert.True(restoredConversation.IsArchived);
            Assert.Equal("keep this message", Assert.Single(restoredConversation.Messages).Content);
            Assert.Equal("keep this draft", restoredConversation.DraftText);
            Assert.Equal(CopilotChatState.CurrentSchemaVersion, restored.SchemaVersion);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InitializationCreatesAnActiveConversationWhenAllAreArchived()
    {
        var config = new CopilotConfig();
        config.EnsureInitialized();
        var archived = CreateConversation("archive-id", "Archived", archived: true);
        var state = new CopilotChatState
        {
            ActiveConversationId = archived.Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { archived },
        };

        Assert.True(state.EnsureInitialized(config));

        Assert.Equal(2, state.Conversations.Count);
        Assert.True(archived.IsArchived);
        var active = Assert.Single(state.Conversations, conversation => !conversation.IsArchived);
        Assert.Equal(active.Id, state.ActiveConversationId);
    }

    private static CopilotConversationRecord CreateConversation(
        string id,
        string title,
        bool archived)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Id = id;
        conversation.Title = title;
        conversation.HasCustomTitle = true;
        conversation.IsArchived = archived;
        return conversation;
    }
}

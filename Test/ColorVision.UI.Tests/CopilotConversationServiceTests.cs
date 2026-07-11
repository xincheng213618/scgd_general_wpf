using ColorVision.Copilot;
using System.Collections.ObjectModel;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationServiceTests
{
    [Fact]
    public void CreateInsertsAfterPinnedConversations()
    {
        var pinned = CreateConversation("pinned", isPinned: true);
        var existing = CreateConversation("existing");
        var conversations = new ObservableCollection<CopilotConversationRecord> { pinned, existing };
        var profile = new CopilotProfileConfig { Id = "profile", Name = "Model" };

        var created = CopilotConversationService.Create(conversations, profile);

        Assert.Equal(1, conversations.IndexOf(created));
        Assert.Equal("profile", created.ProfileId);
        Assert.Same(pinned, conversations[0]);
    }

    [Fact]
    public void ResolveNewTargetReusesExistingEmptyConversation()
    {
        var history = CreateConversation("history");
        history.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "question"));
        var reusable = CreateConversation("empty");
        var conversations = new ObservableCollection<CopilotConversationRecord> { history, reusable };

        var result = CopilotConversationService.ResolveNewTarget(conversations, history, null);

        Assert.Same(reusable, result);
        Assert.Equal(2, conversations.Count);
        Assert.True(CopilotConversationService.IsHistory(history));
        Assert.False(CopilotConversationService.IsHistory(reusable));
    }

    [Fact]
    public void MoveToPreferredIndexKeepsPinnedBlockStable()
    {
        var firstPinned = CreateConversation("first", isPinned: true);
        var secondPinned = CreateConversation("second", isPinned: true);
        var normal = CreateConversation("normal");
        var conversations = new ObservableCollection<CopilotConversationRecord> { firstPinned, secondPinned, normal };

        normal.IsPinned = true;
        CopilotConversationService.MoveToPreferredIndex(conversations, normal);
        Assert.Same(normal, conversations[0]);

        normal.IsPinned = false;
        CopilotConversationService.MoveToPreferredIndex(conversations, normal);
        Assert.Same(firstPinned, conversations[0]);
        Assert.Same(secondPinned, conversations[1]);
        Assert.Same(normal, conversations[2]);
    }

    private static CopilotConversationRecord CreateConversation(string title, bool isPinned = false)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        conversation.SetCustomTitle(title);
        conversation.IsPinned = isPinned;
        return conversation;
    }
}

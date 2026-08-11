using ColorVision.Copilot;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotConversationRecencyTests
{
    [Fact]
    public void NewConversationInitializesAndPersistsIndependentRecency()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");

        Assert.Equal(conversation.CreatedAt, conversation.UpdatedAt);
        Assert.Equal(conversation.CreatedAt, conversation.RecencyAt);

        var startedAt = conversation.CreatedAt.AddMinutes(5);
        Assert.True(conversation.MarkTurnStarted(startedAt));
        var serialized = JsonConvert.SerializeObject(conversation);
        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(serialized);

        Assert.NotNull(restored);
        Assert.Equal(startedAt, restored.RecencyAt);
        Assert.Equal(startedAt, restored.UpdatedAt);
        Assert.False(restored.EnsureValid());
    }

    [Fact]
    public void LegacyConversationMigratesUpdatedTimeIntoRecency()
    {
        var createdAt = new DateTime(2026, 8, 8, 8, 0, 0, DateTimeKind.Local);
        var updatedAt = createdAt.AddHours(2);
        var conversation = new CopilotConversationRecord
        {
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

        Assert.Equal(default, conversation.RecencyAt);
        Assert.True(conversation.EnsureValid());

        Assert.Equal(updatedAt, conversation.RecencyAt);
        Assert.Equal(updatedAt, conversation.UpdatedAt);
        Assert.False(conversation.EnsureValid());
    }

    [Fact]
    public void NormalizationKeepsPinnedOrderAndSortsUnpinnedByRecency()
    {
        var origin = new DateTime(2026, 8, 8, 8, 0, 0, DateTimeKind.Local);
        var pinnedFirst = CreateConversation("pinned-first", origin.AddMinutes(1), isPinned: true);
        var pinnedSecond = CreateConversation("pinned-second", origin.AddMinutes(4), isPinned: true);
        var oldest = CreateConversation("oldest", origin.AddMinutes(1));
        var middle = CreateConversation("middle", origin.AddMinutes(2));
        var newest = CreateConversation("newest", origin.AddMinutes(3));
        var conversations = new ObservableCollection<CopilotConversationRecord>
        {
            pinnedFirst,
            oldest,
            pinnedSecond,
            middle,
            newest,
        };

        Assert.True(CopilotConversationService.NormalizeOrder(conversations));

        Assert.Equal(
            [pinnedFirst, pinnedSecond, newest, middle, oldest],
            conversations);
        Assert.False(CopilotConversationService.NormalizeOrder(conversations));
    }

    [Fact]
    public void BackgroundUpdateDoesNotChangeRecencyButTurnStartMovesConversation()
    {
        var origin = new DateTime(2026, 8, 8, 8, 0, 0, DateTimeKind.Local);
        var newer = CreateConversation("newer", origin.AddMinutes(2));
        var target = CreateConversation("target", origin.AddMinutes(1));
        var conversations = new ObservableCollection<CopilotConversationRecord> { newer, target };

        target.UpdatedAt = origin.AddMinutes(3);
        CopilotConversationService.MoveToPreferredIndex(conversations, target);

        Assert.Equal([newer, target], conversations);
        Assert.Equal(origin.AddMinutes(1), target.RecencyAt);

        Assert.True(CopilotConversationService.MarkTurnStarted(
            conversations,
            target,
            origin.AddMinutes(4)));

        Assert.Equal([target, newer], conversations);
        Assert.Equal(origin.AddMinutes(4), target.RecencyAt);
        Assert.Equal(origin.AddMinutes(4), target.UpdatedAt);
    }

    private static CopilotConversationRecord CreateConversation(
        string title,
        DateTime recencyAt,
        bool isPinned = false)
    {
        return new CopilotConversationRecord
        {
            Id = title,
            Title = title,
            CreatedAt = recencyAt.AddHours(-1),
            UpdatedAt = recencyAt,
            RecencyAt = recencyAt,
            IsPinned = isPinned,
        };
    }
}

using ColorVision.Copilot;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationSearchPreviewTests
{
    [Fact]
    public void HistoricalMessageMatchExplainsWhyConversationWasFound()
    {
        var conversation = CreateConversation();
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.User,
            "Investigate the camera calibration workflow"));

        Assert.True(CopilotConversationSearchPreview.TryBuild(
            conversation,
            ["camera", "workflow"],
            out var preview));
        Assert.StartsWith("历史消息 · ", preview, StringComparison.Ordinal);
        Assert.Contains("camera calibration workflow", preview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HiddenRequestPayloadNeverProducesASearchMatch()
    {
        var conversation = CreateConversation();
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.User,
            "Visible request")
        {
            RequestContent = "private injected attachment payload",
        });

        Assert.False(CopilotConversationSearchPreview.TryBuild(
            conversation,
            ["private"],
            out var preview));
        Assert.Empty(preview);
    }

    [Fact]
    public void AttachmentPathCanMatchWithoutBeingDisclosedInPreview()
    {
        var conversation = CreateConversation();
        conversation.Attachments.Add(new CopilotAttachmentItem
        {
            Type = CopilotAttachmentType.File,
            Title = "report.txt",
            Value = @"C:\sensitive\customer\report.txt",
        });

        Assert.True(CopilotConversationSearchPreview.TryBuild(
            conversation,
            ["sensitive"],
            out var preview));
        Assert.Equal("附件 · report.txt", preview);
        Assert.DoesNotContain(@"C:\sensitive", preview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TermsAcrossDifferentSourcesReportTheMatchKinds()
    {
        var conversation = CreateConversation();
        conversation.Title = "Camera investigation";
        conversation.Goal = CopilotConversationGoal.Create(
            "finish calibration",
            DateTimeOffset.UtcNow);

        Assert.True(CopilotConversationSearchPreview.TryBuild(
            conversation,
            ["camera", "calibration"],
            out var preview));
        Assert.Equal("匹配 · 标题、目标", preview);
    }

    [Fact]
    public void SearchPreviewIsBoundedAndNeverPersisted()
    {
        var conversation = CreateConversation();
        var content = "needle " + new string('x', CopilotConversationSearchPreview.MaximumPreviewCharacters) + "😀";
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, content));

        Assert.True(CopilotConversationSearchPreview.TryBuild(
            conversation,
            ["needle"],
            out var preview));
        Assert.True(preview.Length <= CopilotConversationSearchPreview.MaximumPreviewCharacters + 1);
        Assert.DoesNotContain('�', preview);

        conversation.SetSearchMatchPreview(preview);
        Assert.Equal(preview, conversation.ConversationListPreviewText);
        Assert.Null(JObject.FromObject(conversation)[nameof(CopilotConversationRecord.SearchMatchPreviewText)]);

        conversation.SetSearchMatchPreview(string.Empty);
        Assert.Equal(conversation.PreviewText, conversation.ConversationListPreviewText);
    }

    private static CopilotConversationRecord CreateConversation()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Session";
        conversation.HasCustomTitle = true;
        return conversation;
    }
}

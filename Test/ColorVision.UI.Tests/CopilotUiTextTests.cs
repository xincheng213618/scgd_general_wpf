using ColorVision.Copilot;
using System;

namespace ColorVision.UI.Tests;

public sealed class CopilotUiTextTests
{
    [Fact]
    public void ChatMessage_UsesEnglishUiLabels()
    {
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, "hello");
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);

        Assert.Equal("You", userMessage.Header);
        Assert.Equal("AI", assistantMessage.Header);
        Assert.Equal("Execution", assistantMessage.ExecutionHeader);
        Assert.Equal("Reasoning Details", assistantMessage.ReasoningHeader);

        assistantMessage.IsExecutionInProgress = true;
        assistantMessage.IsReasoningInProgress = true;

        Assert.Equal("Running", assistantMessage.ExecutionHeader);
        Assert.Equal("Reasoning", assistantMessage.ReasoningHeader);
    }

    [Fact]
    public void ConversationSummary_UsesEnglishEmptyAndAttachmentText()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Test Model");

        Assert.Equal("New Chat", conversation.Title);
        Assert.Equal("Click + to start a new chat, or type a question", conversation.PreviewText);

        conversation.Attachments.Add(CopilotAttachmentItem.CreateContext("context"));
        conversation.RefreshSummary();

        Assert.Equal("1 attachment mounted", conversation.PreviewText);

        conversation.Attachments.Add(CopilotAttachmentItem.CreateContext("more context"));
        conversation.RefreshSummary();

        Assert.Equal("2 attachments mounted", conversation.PreviewText);
    }

    [Fact]
    public void AttachmentBadges_UseEnglishLabels()
    {
        Assert.Equal("Context", CopilotAttachmentItem.CreateContext("context").BadgeText);
        Assert.Equal("File", CopilotAttachmentItem.CreateFile("C:\\temp\\sample.txt").BadgeText);
        Assert.Equal("Image", CopilotAttachmentItem.CreateImage("C:\\temp\\sample.png", "sample").BadgeText);
        Assert.Equal("Web", CopilotAttachmentItem.CreateWebPage("https://example.com", "Example", "content").BadgeText);
    }
}

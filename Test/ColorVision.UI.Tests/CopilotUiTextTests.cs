#pragma warning disable CA1707
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

    [Fact]
    public void ThinkingContent_HidesInternalPlanningAndFailedSearchTrace()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);

        message.MarkThinkingStarted();
        message.ExecutionContent = string.Join(Environment.NewLine + Environment.NewLine, new[]
        {
            "Analyzing task...",
            "Round 1: planning next step.",
            "[SearchFiles]" + Environment.NewLine + "Status: Failed" + Environment.NewLine + "Summary: Missing searchable roots.",
        });
        message.MarkThinkingCompleted();

        Assert.True(message.HasThinkingTrace);
        Assert.False(message.HasThinkingContent);
        Assert.StartsWith("已思考", message.ThinkingHeader);
        Assert.DoesNotContain("Analyzing task", message.ThinkingContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Round 1", message.ThinkingContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SearchFiles", message.ThinkingContent, StringComparison.OrdinalIgnoreCase);
    }
}

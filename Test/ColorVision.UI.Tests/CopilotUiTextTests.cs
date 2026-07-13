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
        Assert.True(message.IsThinkingInProgress);
        Assert.Equal("正在思考", message.ThinkingHeader);
        Assert.DoesNotContain("s", message.ThinkingHeader);
        message.ExecutionContent = string.Join(Environment.NewLine + Environment.NewLine, new[]
        {
            "Analyzing task...",
            "Round 1: planning next step.",
            "[SearchFiles]" + Environment.NewLine + "Status: Failed" + Environment.NewLine + "Summary: Missing searchable roots.",
        });
        message.MarkThinkingCompleted();
        Assert.False(message.IsThinkingInProgress);
        message.ThinkingStartedAt = new DateTime(2026, 1, 1, 0, 0, 0);
        message.ThinkingCompletedAt = message.ThinkingStartedAt.AddSeconds(64);

        Assert.True(message.HasThinkingTrace);
        Assert.False(message.HasThinkingContent);
        Assert.Equal("已处理 1m 4s", message.ThinkingHeader);
        Assert.DoesNotContain("Analyzing task", message.ThinkingContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Round 1", message.ThinkingContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SearchFiles", message.ThinkingContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgentActivity_HidesFailedSearchButKeepsDiagnostics()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        var trace = new CopilotAgentTraceEntry
        {
            CallId = "call-1",
            Round = 2,
            ToolName = "WebSearch",
            RuntimeName = "agent-framework",
            Access = CopilotToolAccess.ReadOnly,
            RiskLevel = CopilotToolRiskLevel.Low,
            ApprovalMode = CopilotToolApprovalMode.Never,
            State = CopilotToolExecutionState.Failed,
            FailureKind = CopilotToolFailureKind.NotFound,
            DurationMs = 1900,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            ArgumentSummary = "query=codexradar.com site information",
            ResultSummary = "Web search completed but returned no usable results.",
            ErrorMessage = "No result title and URL pairs could be extracted.",
        };

        message.UpsertAgentTrace(trace);

        Assert.Equal("搜索了网页 · 失败", trace.ActivityLabel);
        Assert.Equal("没有找到可用结果。", trace.ActivityDescription);
        Assert.Equal("1.9s", trace.ActivityDurationLabel);
        Assert.False(trace.IsVisibleInActivity);
        Assert.False(message.HasAgentTraceEntries);
        Assert.DoesNotContain(trace.ActivityLabel, message.ThinkingContent, StringComparison.Ordinal);
        Assert.Contains("Runtime: agent-framework", trace.DiagnosticDetails, StringComparison.Ordinal);
        Assert.Contains("Arguments: query=codexradar.com site information", trace.DiagnosticDetails, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentActivity_ShowsSuccessfulSearchCompactly()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        var trace = new CopilotAgentTraceEntry
        {
            CallId = "call-2",
            Round = 1,
            ToolName = "SearchFiles",
            State = CopilotToolExecutionState.Completed,
            DurationMs = 594,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            ResultSummary = "Found 3 matching files.",
        };

        message.UpsertAgentTrace(trace);

        Assert.True(trace.IsVisibleInActivity);
        Assert.True(message.HasAgentTraceEntries);
        Assert.Equal("搜索了文件", message.ThinkingContent);
        Assert.Equal("594ms", trace.ActivityDurationLabel);
    }
}

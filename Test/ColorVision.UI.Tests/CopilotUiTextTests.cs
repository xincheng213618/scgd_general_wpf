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
    public void ThinkingContent_HidesPersistedFailedSearchDiagnosticsButKeepsOtherFailures()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            ExecutionContent = string.Join(Environment.NewLine + Environment.NewLine, new[]
            {
                "[Round 1 · SearchFiles] Failed · 14ms" + Environment.NewLine
                    + "Runtime: agent-framework · Access: ReadOnly" + Environment.NewLine
                    + "Error: No searchable files were found.",
                "[Round 2 · ApplyTemplatePatch] Failed · 20ms" + Environment.NewLine
                    + "Error: The template revision changed.",
            }),
        };

        Assert.DoesNotContain("SearchFiles", message.ThinkingContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ApplyTemplatePatch", message.ThinkingContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("template revision changed", message.ThinkingContent, StringComparison.OrdinalIgnoreCase);
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
        Assert.False(message.HasExecutionFailures);
        Assert.Equal(string.Empty, message.ExecutionSummary);
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

    [Fact]
    public void AgentActivity_GroupsConsecutiveDatabaseQueries()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        for (var index = 0; index < 4; index++)
        {
            message.UpsertAgentTrace(CreateCompletedTrace($"db-{index}", "QueryDatabaseSql", 20 + index));
        }

        var group = Assert.Single(message.VisibleAgentTraceGroups);

        Assert.True(group.IsMultiple);
        Assert.Equal(4, group.Entries.Count);
        Assert.Equal("执行了多次数据库查询", group.ActivityLabel);
        Assert.Equal("✓", group.ActivityGlyph);
        Assert.Equal(string.Empty, group.ActivityDurationLabel);
        Assert.Equal(group.ActivityLabel, message.ThinkingContent);
        Assert.False(message.HasStandaloneThinkingTrace);
    }

    [Fact]
    public void AgentActivity_GroupsOnlyAdjacentToolsAndPreservesOrder()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        message.UpsertAgentTrace(CreateCompletedTrace("db-1", "QueryDatabaseSql", 20));
        message.UpsertAgentTrace(CreateCompletedTrace("shell-1", "RunShellCommand", 30));
        message.UpsertAgentTrace(CreateCompletedTrace("port-1", "InspectTcpPort", 40));
        message.UpsertAgentTrace(CreateCompletedTrace("db-2", "QueryDatabaseSql", 50));

        var groups = message.VisibleAgentTraceGroups;

        Assert.Equal(3, groups.Count);
        Assert.Equal("查询了数据库", groups[0].ActivityLabel);
        Assert.Equal("运行了多个命令", groups[1].ActivityLabel);
        Assert.Equal(2, groups[1].Entries.Count);
        Assert.Equal("查询了数据库", groups[2].ActivityLabel);
    }

    [Fact]
    public void AgentActivity_GroupReportsPartialFailureWithoutLosingEntryDetails()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        message.UpsertAgentTrace(CreateCompletedTrace("shell-1", "RunShellCommand", 30));
        message.UpsertAgentTrace(new CopilotAgentTraceEntry
        {
            CallId = "shell-2",
            Round = 2,
            ToolName = "RunShellCommand",
            State = CopilotToolExecutionState.Failed,
            FailureKind = CopilotToolFailureKind.Validation,
            DurationMs = 15,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            ErrorMessage = "Invalid command.",
        });

        var group = Assert.Single(message.VisibleAgentTraceGroups);

        Assert.True(group.IsFailure);
        Assert.Equal("运行了多个命令 · 部分失败", group.ActivityLabel);
        Assert.Equal("!", group.ActivityGlyph);
        Assert.Contains("Invalid command", group.Entries[1].DiagnosticDetails, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponseTimeline_PreservesMarkdownToolMarkdownOrderAndGroupsAdjacentCalls()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        message.BeginResponseTimeline();
        message.AppendResponseTimelineText("我先检查当前端口。\n\n");
        message.UpsertAgentTrace(CreateCompletedTrace("shell-1", "RunShellCommand", 30));
        message.RecordResponseTimelineTool("shell-1");
        message.UpsertAgentTrace(CreateCompletedTrace("port-1", "InspectTcpPort", 40));
        message.RecordResponseTimelineTool("port-1");
        message.AppendResponseTimelineText("检查完成，6666 端口当前未被占用。");

        var items = message.VisibleResponseTimelineItems;

        Assert.Equal(3, items.Count);
        Assert.True(items[0].IsMarkdown);
        Assert.Equal("我先检查当前端口。\n\n", items[0].Markdown);
        Assert.True(items[1].IsToolGroup);
        Assert.Equal("运行了多个命令", items[1].ToolGroup!.ActivityLabel);
        Assert.Equal(2, items[1].ToolGroup!.Entries.Count);
        Assert.True(items[2].IsMarkdown);
        Assert.Equal("检查完成，6666 端口当前未被占用。", items[2].Markdown);
        Assert.Equal("我先检查当前端口。\n\n检查完成，6666 端口当前未被占用。", message.Content);
        Assert.True(message.HasResponseTimeline);
        Assert.False(message.HasLegacyResponseLayout);
    }

    [Fact]
    public void ResponseTimeline_KeepsThinkingHeaderAndCollapsesToolActivityAfterCompletion()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        message.MarkThinkingStarted();
        message.BeginResponseTimeline();
        message.UpsertAgentTrace(CreateCompletedTrace("system-1", "InspectWindowsSystem", 25));
        message.RecordResponseTimelineTool("system-1");

        Assert.True(message.HasResponseTimeline);
        Assert.False(message.HasLegacyResponseLayout);
        Assert.True(message.HasThinkingTrace);
        Assert.True(message.IsThinkingExpanded);
        Assert.Equal("正在思考", message.ThinkingHeader);
        Assert.Equal("检查了系统", Assert.Single(message.VisibleResponseTimelineItems).ToolGroup!.ActivityLabel);

        message.MarkThinkingCompleted();

        Assert.False(message.IsThinkingExpanded);
        Assert.StartsWith("已处理", message.ThinkingHeader, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponseTimeline_ResetRemovesUnsupportedDraftButPreservesToolOrder()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        message.BeginResponseTimeline();
        message.AppendResponseTimelineText("这是一段不应继续显示的草稿。");
        message.UpsertAgentTrace(CreateCompletedTrace("db-1", "QueryDatabaseSql", 25));
        message.RecordResponseTimelineTool("db-1");

        message.ResetResponseTimelineText();
        message.AppendResponseTimelineText("这是重新验证后的回答。");

        var items = message.VisibleResponseTimelineItems;

        Assert.Equal(2, items.Count);
        Assert.True(items[0].IsToolGroup);
        Assert.Equal("查询了数据库", items[0].ToolGroup!.ActivityLabel);
        Assert.True(items[1].IsMarkdown);
        Assert.Equal("这是重新验证后的回答。", items[1].Markdown);
        Assert.DoesNotContain("草稿", message.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponseTimeline_StreamingTextUpdatesStableViewItem()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        message.BeginResponseTimeline();
        message.AppendResponseTimelineText("第一段");
        var timelineItem = Assert.Single(message.VisibleResponseTimelineItems);

        message.AppendResponseTimelineText("继续输出");

        Assert.Same(timelineItem, Assert.Single(message.VisibleResponseTimelineItems));
        Assert.Equal("第一段继续输出", timelineItem.Markdown);
    }

    [Fact]
    public void ResponseTimeline_InvalidPersistedOffsetsFallBackToLegacyLayout()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "answer")
        {
            UsesResponseTimeline = true,
        };
        message.ResponseTimelineEvents.Add(CopilotResponseTimelineEvent.Markdown(0, 99));

        Assert.True(message.EnsureValid());

        Assert.False(message.UsesResponseTimeline);
        Assert.Empty(message.ResponseTimelineEvents);
        Assert.False(message.HasResponseTimeline);
        Assert.True(message.HasLegacyResponseLayout);
        Assert.Equal("answer", message.Content);
    }

    private static CopilotAgentTraceEntry CreateCompletedTrace(string callId, string toolName, long durationMs)
    {
        return new CopilotAgentTraceEntry
        {
            CallId = callId,
            Round = 1,
            ToolName = toolName,
            State = CopilotToolExecutionState.Completed,
            DurationMs = durationMs,
            CompletedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}

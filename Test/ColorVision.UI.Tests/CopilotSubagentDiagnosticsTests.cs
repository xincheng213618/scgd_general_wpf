using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotSubagentDiagnosticsTests
{
    [Fact]
    public void CommandsExposeRoleAndRunDiagnosticsDuringAnActiveRequest()
    {
        var agents = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse("/agents runs 5"));
        var subagents = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse("/subagents roles"));
        var suggestions = CopilotLocalCommandCatalog.Suggest("/agents ");

        Assert.Equal(CopilotLocalCommandKind.Subagents, agents.Command.Kind);
        Assert.Equal("runs 5", agents.Arguments);
        Assert.True(agents.Command.AvailableWhileAgentRuns);
        Assert.Equal(CopilotLocalCommandKind.Subagents, subagents.Command.Kind);
        Assert.Contains(suggestions, item => item.Name == "/agents roles");
        Assert.Contains(suggestions, item => item.Name == "/agents runs");
    }

    [Theory]
    [InlineData(null, 0, 8)]
    [InlineData("", 0, 8)]
    [InlineData("roles", 1, 0)]
    [InlineData("ROLES", 1, 0)]
    [InlineData("runs", 2, 8)]
    [InlineData("runs 1", 2, 1)]
    [InlineData("runs 20", 2, 20)]
    [InlineData("runs 0", 3, 0)]
    [InlineData("runs 21", 3, 0)]
    [InlineData("runs all", 3, 0)]
    [InlineData("roles 2", 3, 0)]
    public void CommandArgumentsAreBounded(
        string? arguments,
        int expectedAction,
        int expectedLimit)
    {
        var request = CopilotSubagentDiagnostics.ParseCommand(arguments);

        Assert.Equal((CopilotSubagentDiagnosticAction)expectedAction, request.Action);
        Assert.Equal(expectedLimit, request.Limit);
    }

    [Fact]
    public void OverviewDescribesTheActualRequestScopedReadOnlyRoles()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Agent diagnostics";

        var report = CopilotSubagentDiagnostics.Format(conversation, string.Empty);

        Assert.StartsWith("子代理 · Agent diagnostics", report);
        Assert.Contains("请求级只读委派", report);
        Assert.Contains("并发硬上限 2", report);
        Assert.Contains("单次硬上限 16,384 tokens", report);
        Assert.Contains("请求合计硬上限 32,768 tokens", report);
        Assert.Contains("- explore · Explore · 工作区只读 · 子模式 Code", report);
        Assert.Contains("来源：ColorVision [builtin] v9 · tool DelegateExplore", report);
        Assert.Contains("SearchFiles, GrepText, ReadLocalFile, ListDirectory", report);
        Assert.Contains("- scout · Scout · 公共网页只读 · 子模式 Web", report);
        Assert.Contains("来源：ColorVision [builtin] v2 · tool DelegateScout", report);
        Assert.Contains("WebSearch, FetchUrl", report);
        Assert.Contains("不是可切换或可恢复的独立会话", report);
        Assert.Contains("当前会话没有可见的子代理运行轨迹", report);
    }

    [Fact]
    public void RunsShowBoundedNewestMetadataWithoutPromptOrAnswerContent()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Delegation";
        var older = new CopilotChatMessage(CopilotChatRole.Assistant, "Older answer");
        older.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            ToolName = "DelegateScout",
            State = CopilotToolExecutionState.Completed,
            DelegatedRoleId = "scout",
            DelegatedRunId = "scout-old",
            DelegatedStopReason = CopilotAgentStopReason.Completed,
            DelegatedRequestTokenBudget = 4_096,
            DelegatedConsumedTokens = 1_024,
            DelegatedProviderCalls = 1,
            DelegatedToolCalls = 2,
            ArgumentSummary = "secret prompt",
            ResultSummary = "secret answer",
        });
        var newer = new CopilotChatMessage(CopilotChatRole.Assistant, "Newer answer");
        newer.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            ToolName = "DelegateExplore",
            State = CopilotToolExecutionState.Completed,
            DelegatedRunId = "explore-new",
            DelegatedStopReason = CopilotAgentStopReason.Completed,
            DelegatedRequestTokenBudget = 8_192,
            DelegatedConsumedTokens = 2_048,
            DelegatedProviderCalls = 2,
            DelegatedToolCalls = 5,
            DelegatedRegisteredToolCount = 4,
            DelegatedAvailableToolCount = 3,
            DelegatedQueueDurationMs = 125,
            DurationMs = 1_500,
            StartedAtUtc = new DateTimeOffset(2026, 7, 31, 4, 0, 0, TimeSpan.Zero),
        });
        conversation.Messages.Add(older);
        conversation.Messages.Add(newer);

        var runs = CopilotSubagentDiagnostics.CaptureRuns(conversation);
        var report = CopilotSubagentDiagnostics.Format(conversation, "runs 1");

        Assert.Equal(2, runs.Count);
        Assert.Equal("explore-new", runs[0].RunId);
        Assert.Equal("explore", runs[0].RoleId);
        Assert.Contains("显示 1 / 2 次（新到旧）", report);
        Assert.Contains("#1 · explore · explore-new · state=Completed · stop=Completed", report);
        Assert.Contains("耗时 1.5s · 排队 125ms · tokens 2,048/8,192 · 模型 2 · 工具 5 · 工具面 3/4", report);
        Assert.Contains("另有 1 次较早运行未显示", report);
        Assert.DoesNotContain("scout-old", report);
        Assert.DoesNotContain("secret prompt", report);
        Assert.DoesNotContain("secret answer", report);
    }

    [Fact]
    public void RunningDelegateIsVisibleBeforeItsRunUsageReturns()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            ToolName = "DelegateExplore",
            State = CopilotToolExecutionState.Running,
        });
        conversation.Messages.Add(assistant);

        var report = CopilotSubagentDiagnostics.Format(conversation, "runs");

        Assert.Contains("#1 · explore · ID 待回传 · state=Running", report);
        Assert.Contains("等待子运行回传用量", report);
    }

    [Fact]
    public void InvalidArgumentsReturnUsageWithoutReadingConversationMetadata()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Sensitive title";

        var report = CopilotSubagentDiagnostics.Format(conversation, "runs 100");

        Assert.Equal(CopilotSubagentDiagnostics.Usage, report);
        Assert.DoesNotContain("Sensitive title", report);
    }
}

#pragma warning disable CA1707
using ColorVision.Copilot;
using System;
using System.Linq;

namespace ColorVision.UI.Tests;

public sealed class CopilotToolIntentPolicyTests
{
    [Fact]
    public void AutoMode_WorkspaceReadToolsRemainAvailableWithoutForcingConceptSearch()
    {
        var request = new CopilotAgentRequest
        {
            UserText = "畸变校正是怎么实现的？",
            Mode = CopilotAgentMode.Auto,
            SearchRootPaths = new[] { @"C:\workspace" },
        };

        Assert.True(new CopilotSearchFilesTool().CanHandle(request));
        Assert.True(new CopilotGrepTextTool().CanHandle(request));
        Assert.True(new CopilotReadLocalFileTool().CanHandle(request));
        Assert.True(new CopilotListDirectoryTool().CanHandle(request));
        Assert.True(new CopilotDelegateExploreTool().CanHandle(request));
        Assert.False(new CopilotSearchDocsTool().CanHandle(request));
        Assert.False(new CopilotWebSearchTool().CanHandle(request));
        Assert.False(new CopilotFetchUrlTool().CanHandle(request));
    }

    [Fact]
    public void AutoMode_ExplicitProjectQuestionExposesLocalSearchTools()
    {
        var request = new CopilotAgentRequest
        {
            UserText = "这个项目里的畸变校正代码在哪里实现？",
            Mode = CopilotAgentMode.Auto,
            SearchRootPaths = new[] { @"C:\workspace" },
        };

        Assert.True(new CopilotSearchFilesTool().CanHandle(request));
        Assert.True(new CopilotGrepTextTool().CanHandle(request));
    }

    [Fact]
    public void AutoMode_StableCoreToolsDoNotChangeWithPromptWording()
    {
        var registry = CopilotToolRegistry.CreateDefault();
        var prompts = new[]
        {
            "检查当前系统的版本",
            "数据库里现在有多少数据",
            "查看最近的应用错误日志",
            "解释一下畸变校正",
        };
        var expectedNames = new[]
        {
            "GetRecentLog",
            "QueryFlowExecutionStats",
            "QueryDatabaseSql",
            "ExecuteDatabaseSql",
            "InspectWindowsSystem",
            "InspectWindowsServices",
            "InspectTcpPort",
            "RunShellCommand",
        };
        string[]? firstCoreNames = null;

        foreach (var prompt in prompts)
        {
            var tools = registry.FindTools(new CopilotAgentRequest
            {
                UserText = prompt,
                Mode = CopilotAgentMode.Auto,
                History = [new CopilotRequestMessage("assistant", "previous answer")],
            });

            Assert.All(expectedNames, name => Assert.Contains(tools, tool => tool.Name == name));
            var coreNames = tools.OfType<ICopilotAgentDrivenTool>().Select(tool => tool.Name).ToArray();
            firstCoreNames ??= coreNames;
            Assert.Equal(firstCoreNames, coreNames);
        }
    }

    [Theory]
    [InlineData("请联网搜索最新版本", true, false)]
    [InlineData("https://example.com 这里实现了什么", true, true)]
    public void AutoMode_ExplicitWebIntentExposesOnlyRelevantWebTool(string prompt, bool expectSearch, bool expectFetch)
    {
        var request = new CopilotAgentRequest
        {
            UserText = prompt,
            Mode = CopilotAgentMode.Auto,
        };

        Assert.Equal(expectSearch, new CopilotWebSearchTool().CanHandle(request));
        Assert.Equal(expectFetch, new CopilotFetchUrlTool().CanHandle(request));
    }

    [Theory]
    [InlineData("不要联网，只解释 https://example.com 的 URL 结构")]
    [InlineData("do not browse; explain the text https://example.com")]
    public void AutoMode_ExplicitWebOptOutSuppressesSearchAndFetch(string prompt)
    {
        var request = new CopilotAgentRequest
        {
            UserText = prompt,
            Mode = CopilotAgentMode.Auto,
        };

        Assert.False(new CopilotWebSearchTool().CanHandle(request));
        Assert.False(new CopilotFetchUrlTool().CanHandle(request));
    }

    [Fact]
    public void AutoMode_ShortFollowUpAfterWebRunRetainsReadOnlyWebTools()
    {
        var request = new CopilotAgentRequest
        {
            UserText = "Pro20x的额度有多少",
            Mode = CopilotAgentMode.Auto,
            History = [new CopilotRequestMessage("user", "https://codexradar.com/ 寻找里面有价值的信息")],
            SessionCheckpoint = CreatePreviousWebCheckpoint(),
        };

        var tools = CopilotToolRegistry.CreateDefault().FindTools(request);

        Assert.Contains(tools, tool => tool.Name == "FetchUrl");
        Assert.Contains(tools, tool => tool.Name == "WebSearch");
        Assert.Contains(tools, tool => tool.Name == "RunShellCommand" && tool.Capability.ApprovalMode == CopilotToolApprovalMode.Always);
        Assert.Contains(tools, tool => tool.Name == "ExecuteDatabaseSql" && tool.Capability.ApprovalMode == CopilotToolApprovalMode.Always);
    }

    [Fact]
    public void AutoMode_ShortFollowUpKeepsStableCoreAgentTools()
    {
        var request = new CopilotAgentRequest
        {
            UserText = "继续",
            Mode = CopilotAgentMode.Auto,
            History = [new CopilotRequestMessage("assistant", "previous database answer")],
            SessionCheckpoint = CreatePreviousToolCheckpoint("QueryDatabaseSql"),
        };

        var tools = CopilotToolRegistry.CreateDefault().FindTools(request);

        Assert.Contains(tools, tool => tool.Name == "QueryDatabaseSql");
        Assert.Contains(tools, tool => tool.Name == "GetRecentLog");
        Assert.Contains(tools, tool => tool.Name == "InspectTcpPort");
        Assert.Contains(tools, tool => tool.Name == "RunShellCommand" && tool.Capability.ApprovalMode == CopilotToolApprovalMode.Always);
    }

    [Theory]
    [InlineData("另外，畸变校正是什么？")]
    [InlineData("换个话题，介绍一下色彩空间")]
    [InlineData("这个项目的代码在哪里实现？")]
    public void AutoMode_NewTopicAfterWebRunDoesNotRetainWebTools(string prompt)
    {
        var request = new CopilotAgentRequest
        {
            UserText = prompt,
            Mode = CopilotAgentMode.Auto,
            History = [new CopilotRequestMessage("user", "https://codexradar.com/")],
            SessionCheckpoint = CreatePreviousWebCheckpoint(),
        };

        var tools = CopilotToolRegistry.CreateDefault().FindTools(request);

        Assert.DoesNotContain(tools, tool => tool.Name is "FetchUrl" or "WebSearch");
    }

    private static CopilotAgentSessionCheckpoint CreatePreviousWebCheckpoint() => CreatePreviousToolCheckpoint("FetchUrl");

    private static CopilotAgentSessionCheckpoint CreatePreviousToolCheckpoint(string toolName)
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var startedAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        journal.Observe(CopilotAgentEvent.FromToolResult(new CopilotToolResult
        {
            ToolName = toolName,
            Success = true,
            Summary = "Collected one read-only observation.",
        }, new CopilotToolExecutionInfo
        {
            CallId = "previous-tool",
            ToolName = toolName,
            Access = CopilotToolAccess.ReadOnly,
            ApprovalMode = CopilotToolApprovalMode.Never,
            Idempotency = CopilotToolIdempotency.Idempotent,
            State = CopilotToolExecutionState.Completed,
            StartedAtUtc = startedAt,
            CompletedAtUtc = DateTimeOffset.UtcNow,
        }));
        journal.RecordStop(CopilotAgentStopReason.Completed);

        return new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "test-profile",
            SerializedSessionJson = "{\"state\":{}}",
            TaskEventJournal = journal.Snapshot(),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}

#pragma warning disable CA1707
using ColorVision.Copilot;
using System;

namespace ColorVision.UI.Tests;

public sealed class CopilotToolIntentPolicyTests
{
    [Fact]
    public void AutoMode_OrdinaryConceptQuestionDoesNotExposeSearchTools()
    {
        var request = new CopilotAgentRequest
        {
            UserText = "畸变校正是怎么实现的？",
            Mode = CopilotAgentMode.Auto,
            SearchRootPaths = new[] { @"C:\workspace" },
        };

        Assert.False(new CopilotSearchFilesTool().CanHandle(request));
        Assert.False(new CopilotGrepTextTool().CanHandle(request));
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

    [Theory]
    [InlineData("请联网搜索最新版本", true, false)]
    [InlineData("https://example.com 这里实现了什么", false, true)]
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
        Assert.DoesNotContain(tools, tool => tool.Capability.Access == CopilotToolAccess.Write);
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

    private static CopilotAgentSessionCheckpoint CreatePreviousWebCheckpoint()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        var startedAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        journal.Observe(CopilotAgentEvent.FromToolResult(new CopilotToolResult
        {
            ToolName = "FetchUrl",
            Success = true,
            Summary = "Fetched one web resource.",
        }, new CopilotToolExecutionInfo
        {
            CallId = "previous-fetch",
            ToolName = "FetchUrl",
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

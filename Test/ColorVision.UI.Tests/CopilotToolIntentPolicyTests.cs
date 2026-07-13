#pragma warning disable CA1707
using ColorVision.Copilot;

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
}

using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public class CopilotSearchDocsToolTests
{
    private static CopilotAgentRequest CreateRequest(string userText, CopilotAgentMode mode = CopilotAgentMode.Auto)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Mode = mode,
            Profile = new CopilotProfileConfig(),
        };
    }

    [Fact]
    public void CanHandle_WithWorkflowWhyQuestion_ReturnsTrue()
    {
        var tool = new CopilotSearchDocsTool();
        var request = CreateRequest("流程为什么跑不起来");

        Assert.True(tool.CanHandle(request));
    }

    [Fact]
    public void CanHandle_WithPluginFailureQuestion_ReturnsTrue()
    {
        var tool = new CopilotSearchDocsTool();
        var request = CreateRequest("插件加载失败怎么办");

        Assert.True(tool.CanHandle(request));
    }

    [Fact]
    public void CanHandle_WithShortcutQuestion_ReturnsTrue()
    {
        var tool = new CopilotSearchDocsTool();
        var request = CreateRequest("运行快捷键是什么");

        Assert.True(tool.CanHandle(request));
    }

    [Fact]
    public void CanHandle_WithGenericProgrammingQuestion_ReturnsFalse()
    {
        var tool = new CopilotSearchDocsTool();
        var request = CreateRequest("解释一下 C# async await");

        Assert.False(tool.CanHandle(request));
    }

    [Fact]
    public void CanHandle_InChatMode_ReturnsFalse()
    {
        var tool = new CopilotSearchDocsTool();
        var request = CreateRequest("流程为什么跑不起来", CopilotAgentMode.Chat);

        Assert.False(tool.CanHandle(request));
    }
}
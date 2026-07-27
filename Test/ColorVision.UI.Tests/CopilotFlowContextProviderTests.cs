using ColorVision.Copilot;
using ColorVision.Engine.FlowProcessing.Integration;

namespace ColorVision.UI.Tests;

public sealed class CopilotFlowContextProviderTests
{
    [Fact]
    public async Task GenericPromptOnFlowSurfaceDoesNotCaptureFullFlowSnapshot()
    {
        var captureCount = 0;
        var provider = CreateProvider(
            () => captureCount++,
            isCurrentSurface: true);

        var context = await provider.CaptureAsync(
            new CopilotContextRequest
            {
                Scope = CopilotContextScope.Agent,
                UserText = "写一个 Python 脚本，打印 hello world",
            },
            CancellationToken.None);

        Assert.Null(context);
        Assert.Equal(0, captureCount);
    }

    [Theory]
    [InlineData("这个流程为什么失败")]
    [InlineData("检查 workflow 节点")]
    public async Task FlowIntentCapturesFullFlowSnapshot(string prompt)
    {
        var captureCount = 0;
        var provider = CreateProvider(
            () => captureCount++,
            isCurrentSurface: true);

        var context = await provider.CaptureAsync(
            new CopilotContextRequest
            {
                Scope = CopilotContextScope.Agent,
                UserText = prompt,
            },
            CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal(1, captureCount);
    }

    [Fact]
    public async Task WorkspaceAuditOnFlowSurfaceDoesNotCaptureFullFlowSnapshot()
    {
        var captureCount = 0;
        var provider = CreateProvider(
            () => captureCount++,
            isCurrentSurface: true);
        var plan = CopilotAgentRequestFactory.Prepare(
            "全面审计 ColorVision/Copilot 的取消与超时链路，至少检查 40 个相关代码位置。",
            CopilotAgentMode.Auto,
            new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: null,
                attachments: null));

        var context = await provider.CaptureAsync(
            plan.ContextRequest,
            CancellationToken.None);

        Assert.True(plan.ContextRequest.RequiresWorkspaceEvidence);
        Assert.Null(context);
        Assert.Equal(0, captureCount);
    }

    private static CopilotFlowContextProvider CreateProvider(
        Action onCapture,
        bool isCurrentSurface)
    {
        return new CopilotFlowContextProvider(
            _ =>
            {
                onCapture();
                return Task.FromResult<CopilotFlowContextSnapshot?>(new CopilotFlowContextSnapshot
                {
                    FlowName = "test-flow",
                });
            },
            isActive: () => true,
            isCurrentSurface: () => isCurrentSurface);
    }
}

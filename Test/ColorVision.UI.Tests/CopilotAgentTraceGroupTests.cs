using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTraceGroupTests
{
    [Fact]
    public void CancelledWorkspaceApplyIsNotReportedAsMultiplePartialFailures()
    {
        var groups = CopilotAgentTraceGroup.Create(
        [
            CreateTrace("PreviewWorkspacePatchEnvelope", CopilotToolExecutionState.Completed),
            CreateTrace("ApplyWorkspacePatchEnvelope", CopilotToolExecutionState.Cancelled),
        ]);

        var group = Assert.Single(groups);
        Assert.Equal("处理了文件修改 · 已取消", group.ActivityLabel);
    }

    [Fact]
    public void FailedWorkspaceStageRemainsVisibleAsAPartialFailure()
    {
        var groups = CopilotAgentTraceGroup.Create(
        [
            CreateTrace("PreviewWorkspacePatchEnvelope", CopilotToolExecutionState.Completed),
            CreateTrace("ApplyWorkspacePatchEnvelope", CopilotToolExecutionState.Failed),
        ]);

        var group = Assert.Single(groups);
        Assert.Equal("处理了文件修改 · 部分失败", group.ActivityLabel);
    }

    private static CopilotAgentTraceEntry CreateTrace(
        string toolName,
        CopilotToolExecutionState state)
    {
        return new CopilotAgentTraceEntry
        {
            ToolName = toolName,
            State = state,
        };
    }
}

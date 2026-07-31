using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotSteeringAdmissionTests
{
    [Theory]
    [InlineData(
        CopilotSteeringAdmissionReason.InvalidInput,
        "为空、过长")]
    [InlineData(
        CopilotSteeringAdmissionReason.PendingUserQuestion,
        "先回答问题")]
    [InlineData(
        CopilotSteeringAdmissionReason.NoActiveTask,
        "已结束或已切换任务")]
    [InlineData(
        CopilotSteeringAdmissionReason.QueueFull,
        "缓冲区已满")]
    [InlineData(
        CopilotSteeringAdmissionReason.RuntimeUnavailable,
        "未能送达")]
    public void FailureTextExplainsRejectionAndPreservesComposer(
        CopilotSteeringAdmissionReason reason,
        string expectedExplanation)
    {
        var text = CopilotChatViewModel.GetSteeringAdmissionFailureText(
            new CopilotSteeringAdmissionResult(reason));

        Assert.Contains(expectedExplanation, text, StringComparison.Ordinal);
        Assert.Contains("输入已保留", text, StringComparison.Ordinal);
    }
}

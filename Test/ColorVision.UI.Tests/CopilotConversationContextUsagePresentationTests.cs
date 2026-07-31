using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationContextUsagePresentationTests
{
    [Fact]
    public void ShowsTokenAndMessagePressureWithRemainingThreshold()
    {
        var usage = new CopilotConversationContextUsage(
            UsagePercent: 74,
            WeightUsagePercent: 70,
            MessageUsagePercent: 74,
            ActiveMessageCount: 74,
            ActiveWeight: 2_800,
            MaximumMessages: 100,
            MaximumWeight: 4_000);

        var presentation = CopilotConversationContextUsagePresenter.Create(
            usage,
            autoCompactionEnabled: true,
            autoCompactThresholdPercent: 85);

        Assert.Equal("历史 74%", presentation.Label);
        Assert.False(presentation.IsUnderPressure);
        Assert.Contains("700/1,000 Token", presentation.ToolTip);
        Assert.Contains("74/100 条消息", presentation.ToolTip);
        Assert.Contains("当前还剩 11 个百分点", presentation.ToolTip);
    }

    [Fact]
    public void WarnsBeforeAndAtTheAutomaticCompactionThreshold()
    {
        var approaching = CopilotConversationContextUsagePresenter.Create(
            CreateUsage(75),
            autoCompactionEnabled: true,
            autoCompactThresholdPercent: 85);
        var reached = CopilotConversationContextUsagePresenter.Create(
            CreateUsage(85),
            autoCompactionEnabled: true,
            autoCompactThresholdPercent: 85);

        Assert.True(approaching.IsUnderPressure);
        Assert.Contains("当前还剩 10 个百分点", approaching.ToolTip);
        Assert.True(reached.IsUnderPressure);
        Assert.Contains("已达到 85% 自动压缩阈值", reached.ToolTip);
        Assert.Contains("有完整新对话时", reached.ToolTip);
    }

    [Fact]
    public void ExplainsWhenAutomaticCompactionIsDisabled()
    {
        var presentation = CopilotConversationContextUsagePresenter.Create(
            CreateUsage(80),
            autoCompactionEnabled: false,
            autoCompactThresholdPercent: 85);

        Assert.True(presentation.IsUnderPressure);
        Assert.Contains("自动压缩已关闭", presentation.ToolTip);
        Assert.Contains("设置的 Agent 页启用", presentation.ToolTip);
    }

    private static CopilotConversationContextUsage CreateUsage(int percent) =>
        new(
            UsagePercent: percent,
            WeightUsagePercent: percent,
            MessageUsagePercent: 10,
            ActiveMessageCount: 10,
            ActiveWeight: percent * 40L,
            MaximumMessages: 100,
            MaximumWeight: 4_000);
}

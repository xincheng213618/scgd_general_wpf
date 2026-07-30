using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotLocalCommandHelpTests
{
    [Fact]
    public void HelpCommandAcceptsAnOptionalCommandAndRunsDuringAgentWork()
    {
        var overview = CopilotLocalCommandCatalog.Parse("/help");
        var detail = CopilotLocalCommandCatalog.Parse("/help permissions");

        Assert.NotNull(overview);
        Assert.Equal(CopilotLocalCommandKind.Help, overview.Command.Kind);
        Assert.Empty(overview.Arguments);
        Assert.True(overview.Command.AvailableWhileAgentRuns);
        Assert.NotNull(detail);
        Assert.Same(overview.Command, detail.Command);
        Assert.Equal("permissions", detail.Arguments);
    }

    [Fact]
    public void EveryFixedCommandDeclaresUsageBeginningWithItsName()
    {
        Assert.Equal(38, CopilotLocalCommandCatalog.All.Count);
        foreach (var command in CopilotLocalCommandCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(command.Usage));
            Assert.True(
                command.Usage.StartsWith(command.Name, StringComparison.OrdinalIgnoreCase),
                $"{command.Name} usage must begin with its command name.");
        }
    }

    [Fact]
    public void OverviewGroupsAndRendersEveryFixedCommandExactlyOnce()
    {
        var report = CopilotLocalCommandHelp.Format(null);
        var lines = report.Split(Environment.NewLine);

        Assert.Contains("Copilot 命令 · 38", report);
        Assert.Contains("状态与诊断", report);
        Assert.Contains("工作区与 Agent", report);
        Assert.Contains("会话与输出", report);
        Assert.Contains("模型与推理", report);
        Assert.Contains("◎ Agent 运行中可立即执行；· 当前任务结束后执行。", report);
        Assert.Contains("动态 Skill 不计入固定命令", report);

        foreach (var command in CopilotLocalCommandCatalog.All)
        {
            var availability = command.AvailableWhileAgentRuns ? '◎' : '·';
            Assert.Single(
                lines,
                line => line == $"{availability} {command.Usage} — {command.Description}");
        }
    }

    [Theory]
    [InlineData("permissions", "/permissions [status|ask|auto]", "Agent 运行中：可立即执行")]
    [InlineData("/diff", "/diff [both|staged|unstaged]", "Agent 运行中：当前任务结束后执行")]
    [InlineData("mention", "/mention [查询]", "Agent 运行中：当前任务结束后执行")]
    [InlineData("rewind", "/rewind [N]", "仅会话回溯分支")]
    [InlineData("personality", "/personality [friendly|pragmatic|none]", "默认沟通风格")]
    [InlineData("EFFORT", "/effort [auto|off|on|high|max]", "同 /reasoning")]
    public void DetailAcceptsNamesWithOrWithoutSlashAndPreservesAliases(
        string query,
        string expectedUsage,
        string expectedText)
    {
        var report = CopilotLocalCommandHelp.Format(query);

        Assert.StartsWith(expectedUsage, report);
        Assert.Contains(expectedText, report);
        Assert.Contains("参数：可选", report);
    }

    [Fact]
    public void QueueHelpDescribesImmediateReadOnlyCommandWithoutArguments()
    {
        var report = CopilotLocalCommandHelp.Format("queue");

        Assert.StartsWith("/queue", report);
        Assert.Contains("等待执行的后续请求", report);
        Assert.Contains("参数：无", report);
        Assert.Contains("Agent 运行中：可立即执行", report);
    }

    [Fact]
    public void ApproveHelpKeepsNativeReviewExplicit()
    {
        var report = CopilotLocalCommandHelp.Format("approve");

        Assert.StartsWith("/approve [N]", report);
        Assert.Contains("原生审查窗口", report);
        Assert.Contains("参数：可选", report);
        Assert.Contains("Agent 运行中：可立即执行", report);
    }

    [Fact]
    public void HistoryHelpDescribesIdleComposerRecovery()
    {
        var report = CopilotLocalCommandHelp.Format("history");

        Assert.StartsWith("/history", report);
        Assert.Contains("恢复到输入框", report);
        Assert.Contains("参数：无", report);
        Assert.Contains("Agent 运行中：当前任务结束后执行", report);
    }

    [Fact]
    public void UnknownCommandReturnsARecoveryHint()
    {
        var report = CopilotLocalCommandHelp.Format("missing");

        Assert.Contains("未找到命令“/missing”", report);
        Assert.Contains("输入 /help 查看全部命令", report);
    }
}

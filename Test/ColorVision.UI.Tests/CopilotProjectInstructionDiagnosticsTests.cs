using ColorVision.Copilot;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotProjectInstructionDiagnosticsTests
{
    [Theory]
    [InlineData("", (int)CopilotProjectInstructionCommandAction.List, 0)]
    [InlineData("list", (int)CopilotProjectInstructionCommandAction.List, 0)]
    [InlineData("open 2", (int)CopilotProjectInstructionCommandAction.Open, 2)]
    [InlineData("open 0", (int)CopilotProjectInstructionCommandAction.Invalid, 0)]
    [InlineData("open two", (int)CopilotProjectInstructionCommandAction.Invalid, 0)]
    [InlineData("edit 1", (int)CopilotProjectInstructionCommandAction.Invalid, 0)]
    public void ParserRequiresAnExplicitPositiveOpenPosition(
        string arguments,
        int expectedAction,
        int expectedPosition)
    {
        var request = CopilotProjectInstructionDiagnostics.ParseCommand(arguments);

        Assert.Equal((CopilotProjectInstructionCommandAction)expectedAction, request.Action);
        Assert.Equal(expectedPosition, request.Position);
    }

    [Fact]
    public void ReportShowsEffectiveOrderAndMetadataWithoutInstructionBodies()
    {
        var root = Path.Combine(Path.GetTempPath(), "copilot-instruction-report");
        var rootInstructions = new CopilotProjectInstructionDocument
        {
            Path = Path.Combine(root, "AGENTS.md"),
            Content = "private root instruction",
        };
        var pathRule = new CopilotProjectInstructionDocument
        {
            Path = Path.Combine(root, ".claude", "rules", "tests.md"),
            Content = "private path rule",
            IsTruncated = true,
        };
        var localOverlay = new CopilotProjectInstructionDocument
        {
            Path = Path.Combine(root, "src", "CLAUDE.local.md"),
            Content = "private local overlay",
        };
        var snapshot = new CopilotProjectInstructionSnapshot(
            root,
            Path.Combine(root, "src", "Program.cs"),
            [rootInstructions, pathRule, localOverlay]);

        var report = CopilotProjectInstructionDiagnostics.Format(snapshot, hasActiveAgentRun: true);

        Assert.Contains("Copilot 项目指令 · 3", report, StringComparison.Ordinal);
        Assert.Contains("#1 · AGENTS.md · 共享指令", report, StringComparison.Ordinal);
        Assert.Contains("#2 · tests.md · Claude 路径规则", report, StringComparison.Ordinal);
        Assert.Contains("#3 · CLAUDE.local.md · 私有局部覆盖", report, StringComparison.Ordinal);
        Assert.True(
            report.IndexOf("#1", StringComparison.Ordinal)
            < report.IndexOf("#2", StringComparison.Ordinal));
        Assert.True(
            report.IndexOf("#2", StringComparison.Ordinal)
            < report.IndexOf("#3", StringComparison.Ordinal));
        Assert.Contains("src" + Path.DirectorySeparatorChar + "Program.cs", report, StringComparison.Ordinal);
        Assert.Contains("已截断", report, StringComparison.Ordinal);
        Assert.Contains("显式本地路径也可能改变路径规则匹配", report, StringComparison.Ordinal);
        Assert.Contains("当前运行中的任务已固定请求启动时的指令快照", report, StringComparison.Ordinal);
        Assert.Contains("不是自动生成的跨会话记忆", report, StringComparison.Ordinal);
        Assert.DoesNotContain(rootInstructions.Content, report, StringComparison.Ordinal);
        Assert.DoesNotContain(pathRule.Content, report, StringComparison.Ordinal);
        Assert.DoesNotContain(localOverlay.Content, report, StringComparison.Ordinal);
        Assert.Same(
            pathRule,
            CopilotProjectInstructionDiagnostics.FindByPosition(snapshot.Documents, 2));
        Assert.Null(CopilotProjectInstructionDiagnostics.FindByPosition(snapshot.Documents, 4));
    }

    [Fact]
    public void ReportSkipsInvalidDocumentsAndExplainsEmptyState()
    {
        var invalid = new CopilotProjectInstructionDocument
        {
            Path = @"C:\workspace\AGENTS.md",
            Content = string.Empty,
        };
        var report = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                @"C:\workspace",
                string.Empty,
                [invalid]),
            hasActiveAgentRun: false);

        Assert.Contains("Copilot 项目指令 · 0", report, StringComparison.Ordinal);
        Assert.Contains("使用 /init", report, StringComparison.Ordinal);
        Assert.Contains("/memory 不会写入文件", report, StringComparison.Ordinal);
        Assert.DoesNotContain("当前运行中的任务", report, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogExposesMemoryAndInstructionsAliasesDuringAgentRuns()
    {
        var memory = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse("/memory open 1"));
        var instructions = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse("/instructions"));

        Assert.Equal(CopilotLocalCommandKind.ProjectInstructions, memory.Command.Kind);
        Assert.Equal("open 1", memory.Arguments);
        Assert.True(memory.Command.AvailableWhileAgentRuns);
        Assert.Equal(CopilotLocalCommandKind.ProjectInstructions, instructions.Command.Kind);
        Assert.True(instructions.Command.AvailableWhileAgentRuns);
    }
}

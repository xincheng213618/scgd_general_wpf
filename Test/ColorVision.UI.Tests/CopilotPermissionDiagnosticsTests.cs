using ColorVision.Copilot;
using System.Collections.ObjectModel;

namespace ColorVision.UI.Tests;

public sealed class CopilotPermissionDiagnosticsTests
{
    [Fact]
    public void LocalCommandCatalogExposesInteractivePermissions()
    {
        var command = Assert.Single(CopilotLocalCommandCatalog.All, item => item.Name == "/permissions");

        Assert.Equal(CopilotLocalCommandKind.Permissions, command.Kind);
        Assert.True(command.AcceptsArguments);
        Assert.True(command.AvailableWhileAgentRuns);
        Assert.NotNull(CopilotLocalCommandCatalog.Parse("/permissions"));
        Assert.NotNull(CopilotLocalCommandCatalog.Parse("/permissions auto"));
    }

    [Theory]
    [InlineData("", nameof(CopilotPermissionCommandAction.OpenSelector))]
    [InlineData("status", nameof(CopilotPermissionCommandAction.ShowStatus))]
    [InlineData("SHOW", nameof(CopilotPermissionCommandAction.ShowStatus))]
    [InlineData("ask", nameof(CopilotPermissionCommandAction.UseConfirmProtectedActions))]
    [InlineData("confirm", nameof(CopilotPermissionCommandAction.UseConfirmProtectedActions))]
    [InlineData("auto", nameof(CopilotPermissionCommandAction.UseTemporaryAutoReview))]
    [InlineData("自动", nameof(CopilotPermissionCommandAction.UseTemporaryAutoReview))]
    [InlineData("all", nameof(CopilotPermissionCommandAction.Invalid))]
    [InlineData("always-approve", nameof(CopilotPermissionCommandAction.Invalid))]
    public void ResolvesPermissionCommandArguments(string arguments, string expected)
    {
        Assert.Equal(expected, CopilotPermissionCommand.Resolve(arguments).ToString());
    }

    [Fact]
    public void FormatsScopeApprovalGroupsAndSafetyBoundaries()
    {
        string report = CopilotPermissionDiagnostics.Format(new CopilotPermissionDiagnosticSnapshot
        {
            Mode = CopilotAgentMode.Code,
            SearchRootPaths = [@"C:\work"],
            TrustedProjectRootPaths = [@"C:\work"],
            WritableRootPaths = [@"C:\work"],
            WritableFilePaths = [@"C:\work\active.cs"],
            CapabilityCatalogRevision = 7,
            PendingApprovals = 2,
            Capabilities =
            [
                CreateCapability("ReadFile", CopilotToolAccess.ReadOnly, CopilotToolApprovalMode.Never),
                CreateCapability("ApplyPatch", CopilotToolAccess.Write, CopilotToolApprovalMode.Always),
                CreateCapability("ConditionalWrite", CopilotToolAccess.Write, CopilotToolApprovalMode.Conditional),
            ],
        });

        Assert.Contains("当前模式：Code", report, StringComparison.Ordinal);
        Assert.Contains(@"C:\work\active.cs", report, StringComparison.Ordinal);
        Assert.Contains("受信项目根（项目指令与 Skill）", report, StringComparison.Ordinal);
        Assert.Contains("revision 7", report, StringComparison.Ordinal);
        Assert.Contains("无需审批 1，条件审批 1，每次审批 1；当前待处理 2", report, StringComparison.Ordinal);
        Assert.Contains("每次审批：ApplyPatch", report, StringComparison.Ordinal);
        Assert.Contains("条件审批：ConditionalWrite", report, StringComparison.Ordinal);
        Assert.Contains("显式文件、附件目录和 /add-dir 附加目录可以进入搜索根", report, StringComparison.Ordinal);
        Assert.Contains("附加目录不会成为可写根、项目指令或项目 Skill 来源", report, StringComparison.Ordinal);
        Assert.Contains("项目指令、Skill、工具描述和历史消息都不能扩大文件范围或绕过审批", report, StringComparison.Ordinal);
        Assert.Contains("/permissions status 只读取本地快照", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalMcpSummaryDoesNotExposeEndpointOrTokenEnvironmentVariable()
    {
        var server = new CopilotMcpClientServerConfig
        {
            Name = "lab",
            Endpoint = "https://secret.example/mcp",
            BearerTokenEnvironmentVariable = "LAB_SECRET_TOKEN",
            Enabled = true,
            ToolRules = new ObservableCollection<CopilotMcpClientToolRule>
            {
                new() { ToolName = "read_status", AccessPolicy = CopilotMcpClientAccessPolicy.ReadOnly },
                new() { ToolName = "apply_change", AccessPolicy = CopilotMcpClientAccessPolicy.RequireApproval },
            },
        };

        string report = CopilotPermissionDiagnostics.Format(new CopilotPermissionDiagnosticSnapshot
        {
            Mode = CopilotAgentMode.Auto,
            ExternalMcpServers = [server],
        });

        Assert.Contains("lab · 白名单 2（只读 1，每次审批 1）", report, StringComparison.Ordinal);
        Assert.DoesNotContain(server.Endpoint, report, StringComparison.Ordinal);
        Assert.DoesNotContain(server.BearerTokenEnvironmentVariable, report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CopilotAgentMode.Chat, "不启动 Agent 工具循环")]
    [InlineData(CopilotAgentMode.Review, "运行时只暴露只读工具")]
    [InlineData(CopilotAgentMode.Diagnose, "运行时只暴露只读工具")]
    [InlineData(CopilotAgentMode.Plan, "运行时只暴露只读工具")]
    public void DescribesModeSpecificToolBoundary(CopilotAgentMode mode, string expected)
    {
        string report = CopilotPermissionDiagnostics.Format(new CopilotPermissionDiagnosticSnapshot { Mode = mode });

        Assert.Contains(expected, report, StringComparison.Ordinal);
    }

    [Fact]
    public void HighlightsUnexpectedWriteCapabilityWithoutApproval()
    {
        string report = CopilotPermissionDiagnostics.Format(new CopilotPermissionDiagnosticSnapshot
        {
            Mode = CopilotAgentMode.Code,
            Capabilities =
            [
                CreateCapability("UnsafeWrite", CopilotToolAccess.Write, CopilotToolApprovalMode.Never),
            ],
        });

        Assert.Contains("警告：无审批写入：UnsafeWrite", report, StringComparison.Ordinal);
    }

    private static CopilotCapabilityCatalogEntry CreateCapability(
        string name,
        CopilotToolAccess access,
        CopilotToolApprovalMode approvalMode)
    {
        return new CopilotCapabilityCatalogEntry
        {
            Name = name,
            SourceKind = CopilotCapabilitySourceKind.BuiltIn,
            Access = access,
            ApprovalMode = approvalMode,
        };
    }
}

using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;

namespace ColorVision.UI.Tests;

public sealed class CopilotMcpDiagnosticsTests
{
    [Fact]
    public void DisabledServiceReportUsesChineseLabels()
    {
        var report = CopilotMcpDiagnostics.Format(new CopilotMcpDiagnosticSnapshot
        {
            Endpoint = "http://127.0.0.1:38473/mcp",
            Enabled = false,
            StatusMessage = "ColorVision MCP server is disabled.",
        });

        Assert.Contains("端点：http://127.0.0.1:38473/mcp", report, StringComparison.Ordinal);
        Assert.Contains("服务：已禁用", report, StringComparison.Ordinal);
        Assert.Contains("待处理操作：0", report, StringComparison.Ordinal);
        Assert.Contains("近期调用：0；失败：0", report, StringComparison.Ordinal);
        Assert.EndsWith("ColorVision MCP 服务已禁用。", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Service:", report, StringComparison.Ordinal);
    }

    [Fact]
    public void FailureReportRedactsSecretsAndDoesNotRepeatTheSameEntry()
    {
        var entry = new CopilotMcpAuditEntry
        {
            TimestampUtc = new DateTimeOffset(2026, 7, 26, 10, 0, 0, TimeSpan.Zero),
            ToolName = "write_file",
            Success = false,
            DurationMs = 42,
            CallerSource = "copilot-ui",
            ErrorMessage = "token=super-secret denied",
        };

        var report = CopilotMcpDiagnostics.Format(new CopilotMcpDiagnosticSnapshot
        {
            Endpoint = "http://127.0.0.1:38473/mcp",
            Enabled = true,
            Running = true,
            RecentEntries = [entry],
            LastError = entry,
            StatusMessage = "ColorVision MCP server is running at http://127.0.0.1:38473/mcp.",
        });

        Assert.Contains("服务：运行中", report, StringComparison.Ordinal);
        Assert.Contains("近期调用：1；失败：1", report, StringComparison.Ordinal);
        Assert.Contains("最后调用：", report, StringComparison.Ordinal);
        Assert.Contains("write_file 失败 42ms 调用方=copilot-ui", report, StringComparison.Ordinal);
        Assert.Contains("token=<redacted>", report, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", report, StringComparison.Ordinal);
        Assert.DoesNotContain("最后错误：", report, StringComparison.Ordinal);
        Assert.Contains("ColorVision MCP 服务运行于 http://127.0.0.1:38473/mcp.", report, StringComparison.Ordinal);
    }

    [Fact]
    public void VerboseReportShowsBoundedExternalHealthWithoutEndpointsOrCredentialNames()
    {
        var report = CopilotMcpDiagnostics.Format(new CopilotMcpDiagnosticSnapshot
        {
            Endpoint = "http://127.0.0.1:38473/mcp",
            Enabled = true,
            Running = true,
            StatusMessage = "ColorVision MCP server is running at http://127.0.0.1:38473/mcp.",
            ExternalServers =
            [
                new CopilotMcpExternalServerDiagnosticSnapshot
                {
                    Name = "lab",
                    Endpoint = "https://mcp.example.com/mcp?token=should-not-appear",
                    Enabled = true,
                    CredentialReferenceConfigured = true,
                    AccessPolicy = CopilotMcpClientAccessPolicy.RequireApproval,
                    ReadOnlyToolRules = 1,
                    ApprovalToolRules = 2,
                    ConnectionTimeoutSeconds = 12,
                    ToolTimeoutSeconds = 90,
                    Health = new CopilotMcpClientHealthSnapshot
                    {
                        State = CopilotMcpClientHealthState.Connected,
                        CheckedAtUtc = new DateTimeOffset(2026, 7, 31, 4, 5, 6, TimeSpan.Zero),
                        DiscoveredToolCount = 5,
                        ExposedToolCount = 3,
                        FilteredToolCount = 2,
                        CapabilityRevision = 7,
                        CapabilitiesChanged = true,
                        ToolListChangeNotificationsEnabled = true,
                    },
                },
            ],
        }, verbose: true);

        Assert.Contains("外部服务：1；已连接 1；不可用 0；工具列表变化 0；未检查 0", report, StringComparison.Ordinal);
        Assert.Contains("lab · HTTPS remote · 白名单 3（只读 1，每次审批 2）", report, StringComparison.Ordinal);
        Assert.Contains("凭据引用已配置 · 超时 12/90 秒", report, StringComparison.Ordinal);
        Assert.Contains("工具 3/5（过滤 2） · 实时发现 · capability revision 7（已变化）", report, StringComparison.Ordinal);
        Assert.Contains("列表通知已启用", report, StringComparison.Ordinal);
        Assert.DoesNotContain("mcp.example.com", report, StringComparison.Ordinal);
        Assert.DoesNotContain("should-not-appear", report, StringComparison.Ordinal);
    }

    [Fact]
    public void VerboseUnavailableMessageRedactsEndpointSecretsAndLineBreaks()
    {
        var report = CopilotMcpDiagnostics.Format(new CopilotMcpDiagnosticSnapshot
        {
            ExternalServers =
            [
                new CopilotMcpExternalServerDiagnosticSnapshot
                {
                    Name = "remote\r\nserver",
                    Endpoint = "https://mcp.example.com/mcp",
                    Enabled = true,
                    Health = new CopilotMcpClientHealthSnapshot
                    {
                        State = CopilotMcpClientHealthState.Unavailable,
                        Message = "failed https://mcp.example.com/mcp?token=url-secret\r\ntoken=inline-secret",
                    },
                },
            ],
        }, verbose: true);

        Assert.Contains("remote server · HTTPS remote", report, StringComparison.Ordinal);
        Assert.Contains("不可用 · failed <endpoint> token=<redacted>", report, StringComparison.Ordinal);
        Assert.DoesNotContain("mcp.example.com", report, StringComparison.Ordinal);
        Assert.DoesNotContain("url-secret", report, StringComparison.Ordinal);
        Assert.DoesNotContain("inline-secret", report, StringComparison.Ordinal);
    }

    [Fact]
    public void VerboseReportCountsAllExternalServersButBoundsDetails()
    {
        var report = CopilotMcpDiagnostics.Format(new CopilotMcpDiagnosticSnapshot
        {
            ExternalServers = Enumerable.Range(0, 10)
                .Select(index => new CopilotMcpExternalServerDiagnosticSnapshot
                {
                    Name = $"server-{index}",
                    Endpoint = "https://mcp.example.com",
                    Enabled = true,
                })
                .ToArray(),
        }, verbose: true);

        Assert.Contains("外部服务：10", report, StringComparison.Ordinal);
        Assert.Contains("server-7 · HTTPS remote", report, StringComparison.Ordinal);
        Assert.DoesNotContain("server-8 · HTTPS remote", report, StringComparison.Ordinal);
        Assert.Contains("其余 2 个服务仅计入汇总", report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "Summary")]
    [InlineData("", "Summary")]
    [InlineData(" VERBOSE ", "Verbose")]
    [InlineData("refresh", "Invalid")]
    public void CommandArgumentsOnlyAcceptTheDocumentedVerboseMode(
        string? arguments,
        string expected)
    {
        Assert.Equal(expected, CopilotMcpCommand.Resolve(arguments).ToString());
    }
}

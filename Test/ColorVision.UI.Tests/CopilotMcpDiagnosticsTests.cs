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
}

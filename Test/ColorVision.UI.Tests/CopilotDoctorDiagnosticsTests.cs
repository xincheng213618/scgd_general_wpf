using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotDoctorDiagnosticsTests
{
    [Fact]
    public void DoctorCommandIsLocalAndAvailableDuringAnActiveRun()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/doctor");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Doctor, invocation.Command.Kind);
        Assert.Empty(invocation.Arguments);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/doctor");
        Assert.Null(CopilotLocalCommandCatalog.Parse("/doctor fix"));
    }

    [Fact]
    public void HealthyReportDistinguishesConfigurationFromConnectionTesting()
    {
        var report = CopilotDoctorDiagnostics.Format(new CopilotDoctorDiagnosticSnapshot
        {
            ProfileLabel = "Production",
            ProfileConfigured = true,
            MaximumQueuedAgentRuns = 3,
            McpListenerEnabled = true,
            McpListenerRunning = true,
            HookSurfaceValid = true,
            EffectiveHookCount = 2,
            ExtensionSourceCount = 1,
        });

        Assert.Contains("结论：未发现阻塞问题 · 错误 0 · 警告 0", report, StringComparison.Ordinal);
        Assert.Contains("[OK] 模型配置：Production 配置完整；本次未主动联网验证。", report, StringComparison.Ordinal);
        Assert.Contains("[OK] 会话保存：当前没有持久化失败提示。", report, StringComparison.Ordinal);
        Assert.Contains("[OK] Agent 宿主：任务宿主可调度 · 排队 0/3。", report, StringComparison.Ordinal);
        Assert.Contains("不调用模型、工具或 MCP，不联网，也不自动修改配置", report, StringComparison.Ordinal);
        Assert.Contains("不显示 API Key、模型地址、MCP Endpoint、token 环境变量", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportClassifiesActionableFailuresWithoutPrintingRemoteDetails()
    {
        var report = CopilotDoctorDiagnostics.Format(new CopilotDoctorDiagnosticSnapshot
        {
            ProfileLabel = "Broken\r\nProfile",
            StatePersistenceNotice = "会话保存失败；\r\n仍保留在内存中。",
            QueuedAgentRuns = 3,
            MaximumQueuedAgentRuns = 3,
            McpListenerEnabled = true,
            McpListenerRunning = true,
            RecentMcpFailureCount = 2,
            EnabledExternalMcpServers = 4,
            ConnectedExternalMcpServers = ["ready"],
            UnavailableExternalMcpServers = ["lab\r\nserver"],
            ChangedExternalMcpServers = ["changed"],
            UncheckedExternalMcpServers = ["new"],
            HookSurfaceValid = true,
            EffectiveHookCount = 3,
            ExtensionIssueCount = 1,
            RecentHookFailureCount = 2,
            TrackedSkillCount = 4,
            ExplicitOnlySkillCount = 1,
            PendingApprovals = 3,
        });

        Assert.Contains("结论：存在需要先处理的错误 · 错误 1 · 警告 5", report, StringComparison.Ordinal);
        Assert.Contains("[ERROR] 模型配置：Broken Profile 尚未完成", report, StringComparison.Ordinal);
        Assert.Contains("[WARN] 会话保存：会话保存失败； 仍保留在内存中。", report, StringComparison.Ordinal);
        Assert.Contains("[WARN] Agent 宿主：排队已满（3/3）", report, StringComparison.Ordinal);
        Assert.Contains("不可用 lab server", report, StringComparison.Ordinal);
        Assert.Contains("扩展激活问题 1 个 · 最近失败或超时 2 次", report, StringComparison.Ordinal);
        Assert.Contains("[INFO] 待确认操作：当前有 3 个操作等待用户确认。", report, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlockedPersistenceRequiresCompatibleApplicationInsteadOfRetry()
    {
        var report = CopilotDoctorDiagnostics.Format(new CopilotDoctorDiagnosticSnapshot
        {
            ProfileConfigured = true,
            StatePersistenceBlocked = true,
            StatePersistenceNotice = "检测到更高版本的会话记录。",
            MaximumQueuedAgentRuns = 3,
            HookSurfaceValid = true,
        });

        Assert.Contains("[ERROR] 会话保存：检测到更高版本的会话记录。", report, StringComparison.Ordinal);
        Assert.Contains("更新至兼容版本并重新打开应用；不要用旧版本覆盖现有会话记录。", report, StringComparison.Ordinal);
        Assert.DoesNotContain("“重试保存”", report, StringComparison.Ordinal);
    }
}

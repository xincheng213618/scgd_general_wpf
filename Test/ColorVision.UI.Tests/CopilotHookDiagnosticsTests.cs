using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotHookDiagnosticsTests
{
    [Fact]
    public void LocalCommandCatalogExposesReadOnlyHooksSnapshot()
    {
        var command = Assert.Single(CopilotLocalCommandCatalog.All, item => item.Name == "/hooks");

        Assert.Equal(CopilotLocalCommandKind.Hooks, command.Kind);
        Assert.True(command.AvailableWhileAgentRuns);
        Assert.NotNull(CopilotLocalCommandCatalog.Parse("/hooks"));
        Assert.Null(CopilotLocalCommandCatalog.Parse("/hooks all"));
    }

    [Fact]
    public void ReportCombinesEffectiveSourcesExtensionActivationAndRecentRuns()
    {
        var registry = new CopilotToolExecutionHookRegistry();
        using var registration = registry.Register(
            "extension:test.extension:hook:policy",
            new NoOpHook(),
            "^Probe$",
            order: 25);
        var report = CopilotHookDiagnostics.Format(new CopilotHookDiagnosticSnapshot
        {
            HookSurface = new CopilotToolExecutor(registry).GetHookSurfaceSnapshot(),
            ExtensionSources =
            [
                new CopilotAgentExtensionSourceSnapshot
                {
                    SourceId = "test.extension",
                    SourceName = "Test extension",
                    SourceVersion = "1.2.3",
                    ActiveHookCount = 1,
                    DeclaredHookCount = 1,
                    Hooks =
                    [
                        new CopilotAgentExtensionHookSnapshot
                        {
                            SourceId = "extension:test.extension:hook:policy",
                            Name = "Policy",
                            ToolNamePattern = "^Probe$",
                            Order = 25,
                            IsActive = true,
                        },
                    ],
                },
            ],
            RecentToolExecutions =
            [
                new CopilotToolExecutionAuditEntry
                {
                    ToolName = "Probe",
                    State = CopilotToolExecutionState.Denied,
                    StartedAtUtc = new DateTimeOffset(2026, 7, 30, 8, 0, 0, TimeSpan.Zero),
                    HookRuns =
                    [
                        CopilotToolExecutionHookRun.Create(
                            "extension:test.extension:hook:policy",
                            CopilotToolExecutionHookPhase.PermissionRequest,
                            CopilotToolExecutionHookState.Completed,
                            1),
                        CopilotToolExecutionHookRun.Create(
                            "builtin:write-tool-policy",
                            CopilotToolExecutionHookPhase.BeforeExecute,
                            CopilotToolExecutionHookState.Completed,
                            1),
                        CopilotToolExecutionHookRun.Create(
                            "extension:test.extension:hook:policy",
                            CopilotToolExecutionHookPhase.BeforeExecute,
                            CopilotToolExecutionHookState.Denied,
                            2,
                            "module_policy_denied"),
                    ],
                },
            ],
        });

        Assert.Contains("/hooks · 工具 Hook 快照", report);
        Assert.Contains("生效定义：2 个 · revision 1 · fingerprint ", report);
        Assert.Contains("builtin:write-tool-policy · matcher * · order -2147483648", report);
        Assert.Contains("extension:test.extension:hook:policy · matcher ^Probe$ · order 25", report);
        Assert.Contains("模块来源：1 个 · Hook 1/1 个已生效/声明", report);
        Assert.Contains("Test extension · v1.2.3 · source test.extension · hooks 1/1", report);
        Assert.Contains("extension:test.extension:hook:policy · active · matcher ^Probe$ · order 25", report);
        Assert.Contains("最近健康度：1 次工具调用 · 3 次 Hook 运行（完成 2，拒绝 1，失败 0，超时 0，取消 0，跳过 0）", report);
        Assert.Contains("Probe/Denied · permission · extension:test.extension:hook:policy · completed · 1 ms", report);
        Assert.Contains("Probe/Denied · before · extension:test.extension:hook:policy · denied · 2 ms · module_policy_denied", report);
        Assert.Contains("不显示工具参数、结果正文或审批内容", report);
    }

    [Fact]
    public void InvalidSurfaceAndExtensionIssueRemainVisibleWithoutRunHistory()
    {
        var report = CopilotHookDiagnostics.Format(new CopilotHookDiagnosticSnapshot
        {
            ExtensionIssues =
            [
                new CopilotAgentExtensionIssue
                {
                    SourceId = "test.extension",
                    Message = "Hook activation failed.\r\nSensitive detail stays inline.",
                },
            ],
        });

        Assert.Contains("生效定义：无有效运行时快照", report);
        Assert.Contains("test.extension: Hook activation failed. Sensitive detail stays inline.", report);
        Assert.Contains("尚无可显示的逐 Hook 运行记录", report);
        Assert.DoesNotContain("failed.\r\nSensitive", report);
    }

    private sealed class NoOpHook : ICopilotToolExecutionHook
    {
        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CopilotToolExecutionHookDecision.Proceed);
        }

        public Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}

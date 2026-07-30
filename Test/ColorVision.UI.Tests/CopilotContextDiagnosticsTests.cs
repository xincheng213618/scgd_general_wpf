using ColorVision.Copilot;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotContextDiagnosticsTests
{
    [Fact]
    public void TrustedProjectRootUsesTheFullNormalizedPath()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "copilot-context", "Default"));

        var report = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            AgentContextEnabled = true,
            TrustedProjectRootPaths = [root],
        });

        Assert.Contains($"  - {root}", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"{Environment.NewLine}  - Default{Environment.NewLine}", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DriveRootKeepsItsDirectorySeparator()
    {
        var root = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()));
        Assert.False(string.IsNullOrWhiteSpace(root));

        var report = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            AgentContextEnabled = true,
            TrustedProjectRootPaths = [root!],
        });

        Assert.Contains($"  - {root}", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EffectiveHookSourcesAndExtensionActivationAreInspectable()
    {
        var registry = new CopilotToolExecutionHookRegistry();
        using var registration = registry.Register(
            "extension:test.extension:hook:policy",
            new NoOpHook(),
            "^Probe$",
            order: 25);

        var report = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            AgentContextEnabled = true,
            ToolHookSurface = new CopilotToolExecutor(registry).GetHookSurfaceSnapshot(),
            AgentExtensions =
            [
                new CopilotAgentExtensionSourceSnapshot
                {
                    SourceId = "test.extension",
                    SourceName = "Test extension",
                    SourceVersion = "1.0.0",
                    DeclaredHookCount = 1,
                    ActiveHookCount = 1,
                },
            ],
        });

        Assert.Contains("工具 Hook：2 个已生效 · revision 1 · fingerprint ", report);
        Assert.Contains("builtin:write-tool-policy · matcher * · order -2147483648", report);
        Assert.Contains("extension:test.extension:hook:policy · matcher ^Probe$ · order 25", report);
        Assert.Contains("业务模块扩展：1 个来源", report);
        Assert.Contains("Hook 1/1 个已激活/声明", report);
        Assert.Contains("Test extension · v1.0.0", report);
        Assert.Contains("hooks 1/1", report);
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

using ColorVision.Copilot;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotContextDiagnosticsTests
{
    [Fact]
    public void AutoCompactionPolicyIsInspectable()
    {
        var report = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            AutoCompactConversationHistory = true,
            AutoCompactThresholdPercent = 85,
            AutoCompactInstructionsCharacters = 128,
        });

        Assert.Contains("自动压缩：已开启 · 活动历史达到 85% 时在发送前压缩；失败时保留原请求", report);
        Assert.Contains("压缩重点：已配置 128 字符长期要求", report);
    }

    [Theory]
    [InlineData(CopilotResponsePersonality.None, "回答风格：无（none）")]
    [InlineData(CopilotResponsePersonality.Friendly, "回答风格：友好（friendly）")]
    [InlineData(CopilotResponsePersonality.Pragmatic, "回答风格：务实（pragmatic）")]
    public void EffectiveRequestPresentationIsInspectable(
        CopilotResponsePersonality personality,
        string expectedPersonality)
    {
        var source = new CopilotProfileConfig();
        source.UseSystemPromptOverride("base prompt");
        var requestProfile = CopilotResponsePresentationGuidance.CreateRequestProfile(source, personality);

        var report = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            ResponsePersonality = personality,
            SystemPromptCharacters = requestProfile.EffectiveSystemPrompt.Length,
        });

        Assert.Contains(expectedPersonality, report);
        Assert.Contains(
            $"有效系统提示：{requestProfile.EffectiveSystemPrompt.Length:N0} 字符（已应用宿主响应规则）",
            report);
        Assert.True(requestProfile.EffectiveSystemPrompt.Length > source.EffectiveSystemPrompt.Length);
    }

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

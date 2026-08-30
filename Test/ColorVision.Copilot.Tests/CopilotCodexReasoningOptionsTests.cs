using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexReasoningOptionsTests
{
    [Fact]
    public void NoneReasoningIsOnlyAcceptedForPlanMode()
    {
        Assert.False(CopilotCodexReasoningEffortSelection.TryParse("none", out _));
        Assert.True(CopilotCodexReasoningEffortSelection.TryParsePlanMode("none", out var effort));
        Assert.Equal(CopilotCodexReasoningEffort.None, effort);
    }

    [Fact]
    public void ReasoningDiagnosticsExposeValuesSourcesAndResponsesScope()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredModelReasoningEffort = CopilotCodexReasoningEffort.XHigh,
            HasModelReasoningEffortOverride = true,
            ModelReasoningEffortSource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredPlanModeReasoningEffort = CopilotCodexReasoningEffort.None,
            HasPlanModeReasoningEffortOverride = true,
            PlanModeReasoningEffortSource = CopilotProjectInstructionConfigSources.TrustedProject,
            ConfiguredModelReasoningSummary = CopilotCodexReasoningSummary.Concise,
            HasModelReasoningSummaryOverride = true,
            ModelReasoningSummarySource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredModelSupportsReasoningSummaries = false,
            HasModelSupportsReasoningSummariesOverride = true,
            ModelSupportsReasoningSummariesSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            CodexReasoningEffort = options.ConfiguredModelReasoningEffort,
            HasCodexReasoningEffortOverride = true,
            CodexReasoningEffortSourceLabel = options.ModelReasoningEffortSourceLabel,
            CodexReasoningSummary = options.ConfiguredModelReasoningSummary,
            HasCodexReasoningSummaryOverride = true,
            CodexReasoningSummarySourceLabel = options.ModelReasoningSummarySourceLabel,
            CodexModelSupportsReasoningSummaries = options.ConfiguredModelSupportsReasoningSummaries,
            HasCodexModelSupportsReasoningSummariesOverride = true,
            CodexModelSupportsReasoningSummariesSourceLabel = options.ModelSupportsReasoningSummariesSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex model_reasoning_effort：xhigh", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex plan_mode_reasoning_effort：none", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_reasoning_summary：concise", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_supports_reasoning_summaries：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.ModelReasoningEffortSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.PlanModeReasoningEffortSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("推理强度：xhigh", contextReport, StringComparison.Ordinal);
        Assert.Contains("推理摘要：concise", contextReport, StringComparison.Ordinal);
        Assert.Contains("推理元数据能力：false", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_reasoning_effort：xhigh", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex plan_mode_reasoning_effort：none", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_reasoning_summary：concise", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_supports_reasoning_summaries：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("阻断", memoryReport, StringComparison.Ordinal);
        Assert.Contains("阻断", contextReport, StringComparison.Ordinal);
        Assert.Contains("阻断", debugReport, StringComparison.Ordinal);
        Assert.Contains("仅 Agent 官方 OpenAI Responses 生效", memoryReport, StringComparison.Ordinal);
        Assert.Contains("仅 Agent 官方 OpenAI Responses 生效", contextReport, StringComparison.Ordinal);
        Assert.Contains("仅 Agent 官方 OpenAI Responses 生效", debugReport, StringComparison.Ordinal);
    }
}

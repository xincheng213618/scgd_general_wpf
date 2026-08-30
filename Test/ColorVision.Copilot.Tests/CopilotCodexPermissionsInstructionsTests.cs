using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexPermissionsInstructionsTests
{
    [Fact]
    public void DisabledSnapshotOmitsOnlyModelVisiblePermissionsGuidance()
    {
        var enabledRequest = CreateRequest(includePermissionsInstructions: true);
        var disabledRequest = CreateRequest(includePermissionsInstructions: false);
        var environment = new CopilotAgentEnvironmentContext();

        string enabledHarness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            enabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: false,
            agentModeEnabled: false);
        string disabledHarness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            disabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: false,
            agentModeEnabled: false);
        var contextBuilder = new CopilotAgentContextBuilder();
        string enabledAnswer = contextBuilder.BuildPreparedUserMessageContent(
            enabledRequest,
            Array.Empty<CopilotToolResult>());
        string disabledAnswer = contextBuilder.BuildPreparedUserMessageContent(
            disabledRequest,
            Array.Empty<CopilotToolResult>());

        Assert.Contains("Codex sandbox_mode=read-only", enabledHarness, StringComparison.Ordinal);
        Assert.Contains("Codex approval_policy=never", enabledHarness, StringComparison.Ordinal);
        Assert.Contains("Codex approvals_reviewer=auto_review", enabledHarness, StringComparison.Ordinal);
        Assert.Contains("auto_review.policy", enabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex sandbox_mode=read-only", disabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex approval_policy=never", disabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex approvals_reviewer=auto_review", disabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("auto_review.policy", disabledHarness, StringComparison.Ordinal);
        Assert.Contains("Codex sandbox_mode=read-only", enabledAnswer, StringComparison.Ordinal);
        Assert.Contains("Codex approval_policy=never", enabledAnswer, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex sandbox_mode=read-only", disabledAnswer, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex approval_policy=never", disabledAnswer, StringComparison.Ordinal);

        Assert.False(CopilotToolRegistry.IsAllowedForCodexSandboxPolicy(
            new CopilotSetThemeTool(),
            disabledRequest));
        Assert.False(CopilotCodexApprovalPolicySelection.AllowsApprovalPrompt(
            disabledRequest.CodexApprovalPolicy,
            CopilotApprovalPromptCategory.SandboxApproval));
    }

    [Fact]
    public void DiagnosticsExplainThePromptOnlyBoundary()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredIncludePermissionsInstructions = false,
            HasIncludePermissionsInstructionsOverride = true,
            IncludePermissionsInstructionsSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            ProfileLabel = "Profile",
            Mode = CopilotAgentMode.Code,
            CodexIncludePermissionsInstructions = false,
            HasCodexIncludePermissionsInstructionsOverride = true,
            CodexIncludePermissionsInstructionsSourceLabel = options.IncludePermissionsInstructionsSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex include_permissions_instructions：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.IncludePermissionsInstructionsSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("沙箱、审批、工具过滤与执行策略保持强制", memoryReport, StringComparison.Ordinal);
        Assert.Contains("权限说明：省略", contextReport, StringComparison.Ordinal);
        Assert.Contains("沙箱、审批、工具过滤与执行策略保持强制", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex include_permissions_instructions：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("沙箱、审批、工具过滤与执行策略保持强制", debugReport, StringComparison.Ordinal);
    }

    private static CopilotAgentRequest CreateRequest(bool includePermissionsInstructions) => new()
    {
        Profile = CopilotProfileConfig.CreateDefault(),
        UserText = "Inspect the workspace permissions.",
        Mode = CopilotAgentMode.Code,
        CodexSandboxMode = CopilotCodexSandboxMode.ReadOnly,
        CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Never),
        CodexApprovalsReviewer = CopilotCodexApprovalsReviewer.AutoReview,
        CodexAutoReviewPolicy = "Review this exact protected call.",
        CodexIncludePermissionsInstructions = includePermissionsInstructions,
    };

}

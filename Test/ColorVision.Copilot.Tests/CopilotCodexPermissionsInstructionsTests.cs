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

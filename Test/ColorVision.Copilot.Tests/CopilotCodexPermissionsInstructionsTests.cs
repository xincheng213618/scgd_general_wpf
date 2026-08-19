using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexPermissionsInstructionsTests
{
    [Fact]
    public void ClosestTrustedValueIsFrozenIntoTheSubmittedRequest()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                include_permissions_instructions = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "include_permissions_instructions = false");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Inspect the workspace permissions.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(projectConfigPath, "include_permissions_instructions = true");
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.False(submitted.ConfiguredIncludePermissionsInstructions);
            Assert.True(submitted.HasIncludePermissionsInstructionsOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submitted.IncludePermissionsInstructionsSource);
            Assert.False(submittedPlan.CodexIncludePermissionsInstructions);
            Assert.False(submittedRequest.CodexIncludePermissionsInstructions);
            Assert.True(refreshed.ConfiguredIncludePermissionsInstructions);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

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

        Assert.Contains("Codex include_permissions_instructions：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.IncludePermissionsInstructionsSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("沙箱、审批、工具过滤与执行策略保持强制", memoryReport, StringComparison.Ordinal);
        Assert.Contains("权限说明：省略", contextReport, StringComparison.Ordinal);
        Assert.Contains("沙箱、审批、工具过滤与执行策略保持强制", contextReport, StringComparison.Ordinal);
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

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-permissions-instructions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}

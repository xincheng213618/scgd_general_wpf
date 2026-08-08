using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotCodexApprovalsReviewerTests
{
    [Fact]
    public void ClosestTrustedReviewerIsParsedAndFrozenIntoTurnSnapshots()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                approvals_reviewer = "user"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(projectConfigPath, "approvals_reviewer = \"auto_review\"");

            var submittedContext = CreateHostContext(globalRoot, projectRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Edit the workspace after approval.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });

            File.WriteAllText(projectConfigPath, "approvals_reviewer = \"user\"");
            var refreshedContext = CreateHostContext(globalRoot, projectRoot);
            var queuedFollowUp = new CopilotQueuedFollowUp(
                "run-1",
                "conversation-1",
                "Conversation",
                "Continue.",
                CopilotAgentMode.Code,
                CopilotProfileConfig.CreateDefault(),
                submittedContext);
            var queuedContext = queuedFollowUp.CreateExecutionContext(
                CopilotConversationHistorySnapshot.Empty);

            var options = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.True(options.HasApprovalsReviewerOverride);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, options.ApprovalsReviewerSource);
            Assert.Equal(CopilotCodexApprovalsReviewer.AutoReview, options.ConfiguredApprovalsReviewer);
            Assert.Equal(CopilotCodexApprovalsReviewer.AutoReview, submittedPlan.CodexApprovalsReviewer);
            Assert.Equal(CopilotCodexApprovalsReviewer.AutoReview, submittedRequest.CodexApprovalsReviewer);
            Assert.Equal(
                CopilotCodexApprovalsReviewer.User,
                refreshedContext.ProjectInstructionDiscoveryOptions.ConfiguredApprovalsReviewer);
            Assert.Equal(
                CopilotCodexApprovalsReviewer.AutoReview,
                queuedContext.ProjectInstructionDiscoveryOptions.ConfiguredApprovalsReviewer);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedAndMalformedProjectValuesCannotChangeTheCodexHomeReviewer()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                approvals_reviewer = "user"

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "approvals_reviewer = \"auto_review\"");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(CopilotCodexApprovalsReviewer.User, untrusted.ConfiguredApprovalsReviewer);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ApprovalsReviewerSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "approvals_reviewer = \"guardian_subagent\"");
            var malformed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(malformed.HasApprovalsReviewerOverride);
            Assert.Equal(CopilotCodexApprovalsReviewer.Unspecified, malformed.ConfiguredApprovalsReviewer);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ExplicitReviewerControlsEligibleAutomaticReviewWithoutGrantingPermission()
    {
        string workspacePath = Path.GetFullPath(Path.GetTempPath());
        var tool = new CopilotShellCommandTool();
        var autoReview = CreateRequest(
            workspacePath,
            CopilotCodexApprovalsReviewer.AutoReview,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest));
        var userReview = CreateRequest(
            workspacePath,
            CopilotCodexApprovalsReviewer.User,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest));
        userReview.AccessContext.PrepareFullAccess(
            userReview.ConversationId,
            workspacePath,
            userReview.TaskId,
            DateTimeOffset.UtcNow.AddMinutes(5));

        Assert.True(CopilotAgentAccessPolicy.CanAutoReview(autoReview, tool, workspacePath));
        Assert.False(autoReview.AccessContext.AllowsUnattendedProtectedActions);
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(userReview, tool, workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(autoReview, tool, workspacePath));

        autoReview.AccessContext.PrepareFullAccess(
            autoReview.ConversationId,
            Path.Combine(workspacePath, $"stale-{Guid.NewGuid():N}"),
            autoReview.TaskId,
            DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.True(CopilotAgentAccessPolicy.CanAutoReview(autoReview, tool, workspacePath));
        Assert.False(autoReview.AccessContext.AllowsUnattendedProtectedActions);

        var never = CreateRequest(
            workspacePath,
            CopilotCodexApprovalsReviewer.AutoReview,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Never));
        var untrusted = CreateRequest(
            workspacePath,
            CopilotCodexApprovalsReviewer.AutoReview,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Untrusted));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(never, tool, workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(untrusted, tool, workspacePath));
    }

    [Fact]
    public async Task ExplicitAutomaticDenialClosesTheExactPendingAction()
    {
        string workspacePath = Path.GetFullPath(Path.GetTempPath());
        var tool = new CopilotShellCommandTool();
        var request = CreateRequest(
            workspacePath,
            CopilotCodexApprovalsReviewer.AutoReview,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest));
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var handle = coordinator.RequestApproval(
            tool,
            request,
            CreateShellInput(workspacePath),
            $"call-{Guid.NewGuid():N}",
            CancellationToken.None,
            userReviewVisible: false);

        try
        {
            Assert.False(handle.Action.IsUserReviewVisible);
            Assert.DoesNotContain(
                handle.Action,
                CopilotMcpConfirmationStore.Instance.GetPendingActionsForConversation(
                    request.ConversationId));
            Assert.True(coordinator.RejectAfterAutomaticReview(
                handle,
                request,
                tool,
                workspacePath,
                "The command could send private data to an untrusted destination.",
                out var message), message);
            var decision = await handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(CopilotFrameworkApprovalDecisionKind.Rejected, decision.Kind);
            Assert.Equal(CopilotFrameworkApprovalDecisionSource.AutomaticReview, decision.Source);
            Assert.Equal("automatic_review_denied", decision.FailureCode);
            Assert.Contains("private data", decision.Reason, StringComparison.Ordinal);
            Assert.Equal("automatic-review", handle.Action.ApprovalDecisionSource);
            Assert.Equal(ConfirmableActionStatus.Rejected, handle.Action.Status);
            Assert.DoesNotContain(
                handle.Action,
                CopilotMcpConfirmationStore.Instance.GetPendingActions());
        }
        finally
        {
            coordinator.Cancel(handle);
        }
    }

    [Fact]
    public void ReviewerDiagnosticsAndInstructionsExposeFrozenRouting()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredApprovalsReviewer = CopilotCodexApprovalsReviewer.AutoReview,
            HasApprovalsReviewerOverride = true,
            ApprovalsReviewerSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string projectReport = CopilotProjectInstructionDiagnostics.Format(
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
            CodexApprovalsReviewer = CopilotCodexApprovalsReviewer.AutoReview,
            HasCodexApprovalsReviewerOverride = true,
            CodexApprovalsReviewerSourceLabel = options.ApprovalsReviewerSourceLabel,
        });
        var request = CreateRequest(
            Path.GetFullPath(Path.GetTempPath()),
            CopilotCodexApprovalsReviewer.AutoReview,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest));
        string harness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            request,
            [new CopilotShellCommandTool()],
            new CopilotAgentEnvironmentContext(),
            taskLedgerEnabled: false,
            agentModeEnabled: true);

        Assert.Contains("Codex approvals_reviewer：auto_review", projectReport, StringComparison.Ordinal);
        Assert.Contains("审批复核者：auto_review", contextReport, StringComparison.Ordinal);
        Assert.Contains("approvals_reviewer=auto_review is frozen", harness, StringComparison.Ordinal);
        Assert.Contains("materially safer path", harness, StringComparison.Ordinal);
    }

    private static CopilotAgentHostContextSnapshot CreateHostContext(
        string globalRoot,
        string projectRoot) => new(
        activeDocumentPath: null,
        projectRoot,
        attachments: null,
        liveContext: null,
        conversationHistory: null,
        additionalReadRootPaths: null,
        globalInstructionRootPath: globalRoot);

    private static CopilotAgentRequest CreateRequest(
        string workspacePath,
        CopilotCodexApprovalsReviewer reviewer,
        CopilotCodexApprovalPolicy approvalPolicy) => new()
    {
        ConversationId = "approvals-reviewer-conversation",
        TaskId = "approvals-reviewer-task",
        WorkspacePath = workspacePath,
        UserText = "Run the requested shell command.",
        TaskIntentText = "Run and verify the requested shell command.",
        Mode = CopilotAgentMode.Code,
        CodexApprovalPolicy = approvalPolicy,
        CodexApprovalsReviewer = reviewer,
        SearchRootPaths = [workspacePath],
        WritableLocalRootPaths = [workspacePath],
        PreferredShell = CopilotShellKind.PowerShell,
    };

    private static CopilotAgentToolInput CreateShellInput(string workspacePath) => new()
    {
        Arguments = new Dictionary<string, object?>
        {
            ["command"] = "Write-Output safe",
            ["shell"] = "powershell",
            ["workingDirectory"] = workspacePath,
            ["timeoutSeconds"] = 60,
        },
    };

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"copilot-approvals-reviewer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}

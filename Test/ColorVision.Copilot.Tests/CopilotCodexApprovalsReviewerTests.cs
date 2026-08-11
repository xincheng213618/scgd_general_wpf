using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

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
    public void GuardianApprovalGateFreezesUserReviewWithoutChangingApprovalPolicyOrSandbox()
    {
        const string privatePolicy = "PRIVATE-GUARDIAN-POLICY: approve bounded validation only.";
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [features]
                guardian_approval = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                $"""
                approval_policy = "on-request"
                approvals_reviewer = "auto_review"

                [auto_review]
                policy = "{privatePolicy}"

                [features]
                guardian_approval = false
                """);

            var submittedContext = CreateHostContext(globalRoot, projectRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Run the bounded validation after approval.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });

            File.WriteAllText(
                projectConfigPath,
                """
                approval_policy = "on-request"
                approvals_reviewer = "auto_review"

                [features]
                guardian_approval = true
                """);
            var refreshed = CreateHostContext(globalRoot, projectRoot)
                .ProjectInstructionDiscoveryOptions;
            var options = submittedContext.ProjectInstructionDiscoveryOptions;

            Assert.False(options.ConfiguredGuardianApprovalEnabled);
            Assert.True(options.HasGuardianApprovalEnabledOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                options.GuardianApprovalEnabledSource);
            Assert.Equal(CopilotCodexApprovalsReviewer.AutoReview, options.ConfiguredApprovalsReviewer);
            Assert.Equal(CopilotCodexApprovalsReviewer.User, options.EffectiveApprovalsReviewer);
            Assert.Equal(
                CopilotCodexApprovalPolicyMode.OnRequest,
                options.ConfiguredApprovalPolicy.Mode);
            Assert.False(submittedPlan.CodexGuardianApprovalEnabled);
            Assert.Equal(CopilotCodexApprovalsReviewer.User, submittedPlan.CodexApprovalsReviewer);
            Assert.Empty(submittedPlan.CodexAutoReviewPolicy);
            Assert.False(submittedRequest.CodexGuardianApprovalEnabled);
            Assert.Equal(CopilotCodexApprovalsReviewer.User, submittedRequest.CodexApprovalsReviewer);
            Assert.Empty(submittedRequest.CodexAutoReviewPolicy);
            Assert.Equal(options.ConfiguredSandboxMode, submittedPlan.CodexSandboxMode);
            Assert.Equal(options.ConfiguredApprovalPolicy, submittedPlan.CodexApprovalPolicy);
            Assert.True(refreshed.ConfiguredGuardianApprovalEnabled);
            Assert.Equal(CopilotCodexApprovalsReviewer.AutoReview, refreshed.EffectiveApprovalsReviewer);
            Assert.False(submittedPlan.CodexGuardianApprovalEnabled);
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

                [features]
                guardian_approval = false

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                """
                approvals_reviewer = "auto_review"

                [features]
                guardian_approval = true
                """);

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(CopilotCodexApprovalsReviewer.User, untrusted.ConfiguredApprovalsReviewer);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ApprovalsReviewerSource);
            Assert.False(untrusted.ConfiguredGuardianApprovalEnabled);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.GuardianApprovalEnabledSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                """
                approvals_reviewer = "guardian_subagent"

                [features]
                guardian_approval = "false"
                """);
            var malformed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(malformed.HasApprovalsReviewerOverride);
            Assert.Equal(CopilotCodexApprovalsReviewer.Unspecified, malformed.ConfiguredApprovalsReviewer);
            Assert.False(malformed.HasGuardianApprovalEnabledOverride);
            Assert.True(malformed.ConfiguredGuardianApprovalEnabled);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ClosestTrustedMultilineAutoReviewPolicyIsParsedAndFrozen()
    {
        const string globalPolicy = "# Global reviewer policy\nDeny every remote side effect.";
        const string projectPolicy = "# Project reviewer policy\nApprove only bounded local validation.\nDeny deployment.";
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $""""
                [auto_review]
                policy = '''
                {globalPolicy}
                '''

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """");
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                """"
                [auto_review]
                policy = '''
                # Project reviewer policy
                Approve only bounded local validation.
                Deny deployment.
                '''
                """");

            var submittedContext = CreateHostContext(globalRoot, projectRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Run the bounded validation.",
                CopilotAgentMode.Code,
                submittedContext);
            var submittedRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CopilotProfileConfig.CreateDefault(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });

            File.WriteAllText(
                projectConfigPath,
                "[auto_review]" + Environment.NewLine + "policy = \"Refreshed policy\"");
            var refreshedContext = CreateHostContext(globalRoot, projectRoot);
            var queuedFollowUp = new CopilotQueuedFollowUp(
                "run-policy",
                "conversation-policy",
                "Conversation",
                "Continue.",
                CopilotAgentMode.Code,
                CopilotProfileConfig.CreateDefault(),
                submittedContext);
            var queuedContext = queuedFollowUp.CreateExecutionContext(
                CopilotConversationHistorySnapshot.Empty);

            var options = submittedContext.ProjectInstructionDiscoveryOptions;
            Assert.True(options.HasAutoReviewPolicyOverride);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, options.AutoReviewPolicySource);
            Assert.Equal(projectPolicy, options.ConfiguredAutoReviewPolicy);
            Assert.Equal(projectPolicy, submittedPlan.CodexAutoReviewPolicy);
            Assert.Equal(projectPolicy, submittedRequest.CodexAutoReviewPolicy);
            Assert.Equal(
                "Refreshed policy",
                refreshedContext.ProjectInstructionDiscoveryOptions.ConfiguredAutoReviewPolicy);
            Assert.Equal(
                projectPolicy,
                queuedContext.ProjectInstructionDiscoveryOptions.ConfiguredAutoReviewPolicy);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void BlankAndUntrustedAutoReviewPoliciesAreIgnored()
    {
        const string globalPolicy = "Keep the global automatic-review policy.";
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $""""
                [auto_review]
                policy = "{globalPolicy}"

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """");
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "[auto_review]" + Environment.NewLine + "policy = \"Replace the global policy.\"");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal(globalPolicy, untrusted.ConfiguredAutoReviewPolicy);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.AutoReviewPolicySource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                """"
                [auto_review]
                policy = '''

                '''
                """");
            var blank = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(blank.HasAutoReviewPolicyOverride);
            Assert.Empty(blank.ConfiguredAutoReviewPolicy);
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
        var guardianDisabled = CreateRequest(
            workspacePath,
            CopilotCodexApprovalsReviewer.AutoReview,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest),
            guardianApprovalEnabled: false);
        var legacyReviewer = CreateRequest(
            workspacePath,
            CopilotCodexApprovalsReviewer.Unspecified,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest));
        legacyReviewer.AccessContext.PrepareFullAccess(
            legacyReviewer.ConversationId,
            workspacePath,
            legacyReviewer.TaskId,
            DateTimeOffset.UtcNow.AddMinutes(5));
        var guardianDisabledLegacyReviewer = CreateRequest(
            workspacePath,
            CopilotCodexApprovalsReviewer.Unspecified,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest),
            guardianApprovalEnabled: false);
        guardianDisabledLegacyReviewer.AccessContext.PrepareFullAccess(
            guardianDisabledLegacyReviewer.ConversationId,
            workspacePath,
            guardianDisabledLegacyReviewer.TaskId,
            DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(never, tool, workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(untrusted, tool, workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(guardianDisabled, tool, workspacePath));
        Assert.False(CopilotCodexApprovalsReviewerSelection.IsExplicitAutoReview(guardianDisabled));
        Assert.True(CopilotAgentAccessPolicy.CanAutoReview(legacyReviewer, tool, workspacePath));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(
            guardianDisabledLegacyReviewer,
            tool,
            workspacePath));
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
    public async Task AutomaticReviewUsesTheToolCallWorkspaceSnapshot()
    {
        string turnWorkspacePath = CreateTemporaryDirectory();
        string stepWorkspacePath = CreateTemporaryDirectory();
        var tool = new CopilotShellCommandTool();
        var request = CreateRequest(
            turnWorkspacePath,
            CopilotCodexApprovalsReviewer.AutoReview,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest));
        var input = CreateShellInput(stepWorkspacePath);
        var executionScope = CopilotExecutionScope.ForAgentRequest(request)
            .WithWorkspace(stepWorkspacePath)
            .BindToolCall(
                tool.Name,
                $"call-{Guid.NewGuid():N}",
                CopilotAgentToolInputExactBinding.CreateExecutionSignature(tool.Name, input));
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var handle = coordinator.RequestApproval(
            tool,
            request,
            input,
            executionScope.ProviderCallId,
            CancellationToken.None,
            executionScope,
            userReviewVisible: false);

        try
        {
            string evidence = CopilotAutomaticApprovalReviewer.BuildEvidencePrompt(
                request,
                tool,
                handle.Action,
                "The tool call requires exact approval for its current workspace.",
                handle.Action.ReviewDetails);

            Assert.Equal(stepWorkspacePath, handle.Action.RequestContext.WorkspacePath);
            Assert.Equal(stepWorkspacePath, handle.Action.RequestContext.Scope.WorkspacePath);
            Assert.Contains($"Workspace: {stepWorkspacePath}", evidence, StringComparison.Ordinal);
            Assert.DoesNotContain($"Workspace: {turnWorkspacePath}", evidence, StringComparison.Ordinal);
            Assert.Contains("Approval trigger:", evidence, StringComparison.Ordinal);
            Assert.Contains("requires exact approval", evidence, StringComparison.Ordinal);
        }
        finally
        {
            coordinator.Cancel(handle);
            var decision = await handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(CopilotFrameworkApprovalDecisionKind.Cancelled, decision.Kind);
            Directory.Delete(turnWorkspacePath, recursive: true);
            Directory.Delete(stepWorkspacePath, recursive: true);
        }
    }

    [Fact]
    public async Task UnavailableAutomaticReviewClosesWithoutRecordingAPolicyDenial()
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
            Assert.True(coordinator.CloseAfterAutomaticReviewUnavailable(
                handle,
                request,
                tool,
                workspacePath,
                "The reviewer timed out before returning a decision.",
                out var message), message);
            var decision = await handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(CopilotFrameworkApprovalDecisionKind.Rejected, decision.Kind);
            Assert.Equal(CopilotFrameworkApprovalDecisionSource.AutomaticReview, decision.Source);
            Assert.Equal("automatic_review_unavailable", decision.FailureCode);
            Assert.Contains("does not establish that the action is unsafe", decision.Reason, StringComparison.Ordinal);
            Assert.Contains("unavailable", decision.FormatStatus(tool.Name), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("automatic-review-unavailable", handle.Action.ApprovalDecisionSource);
            Assert.Equal(ConfirmableActionStatus.Rejected, handle.Action.Status);
            var auditEntry = Assert.Single(CopilotMcpAuditLogger.GetRecentEntries(200), entry =>
                string.Equals(entry.ActionId, handle.Action.ActionId, StringComparison.Ordinal)
                && string.Equals(entry.ToolName, "action_rejected", StringComparison.Ordinal));
            Assert.Equal("automatic-review-unavailable", auditEntry.ApprovalDecisionSource);
            Assert.Contains("unavailable", auditEntry.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
    public async Task ReviewerProviderTimeoutIsUnavailableButCallerCancellationStillPropagates()
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
        using var client = new ProviderTimeoutChatClient();

        try
        {
            var unavailable = await new CopilotAutomaticApprovalReviewer().ReviewAsync(
                client,
                request,
                tool,
                handle.Action,
                string.Empty,
                CancellationToken.None);

            Assert.Equal(CopilotAutomaticApprovalReviewVerdict.Unavailable, unavailable.Verdict);
            Assert.Contains("超时", unavailable.Reason, StringComparison.Ordinal);
            Assert.Contains("执行保持关闭", unavailable.Reason, StringComparison.Ordinal);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new CopilotAutomaticApprovalReviewer().ReviewAsync(
                    client,
                    request,
                    tool,
                    handle.Action,
                    string.Empty,
                    cancellation.Token));
        }
        finally
        {
            coordinator.Cancel(handle);
        }
    }

    [Fact]
    public void ReviewerDiagnosticsAndInstructionsExposeFrozenRouting()
    {
        const string privatePolicy = "PRIVATE-POLICY-SENTINEL: approve only signed local validation.";
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredApprovalsReviewer = CopilotCodexApprovalsReviewer.AutoReview,
            HasApprovalsReviewerOverride = true,
            ApprovalsReviewerSource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredAutoReviewPolicy = privatePolicy,
            HasAutoReviewPolicyOverride = true,
            AutoReviewPolicySource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexAutoReviewPolicyCharacters = privatePolicy.Length,
            HasCodexAutoReviewPolicyOverride = true,
            CodexAutoReviewPolicySourceLabel = options.AutoReviewPolicySourceLabel,
        });
        string effectiveReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });
        var request = CreateRequest(
            Path.GetFullPath(Path.GetTempPath()),
            CopilotCodexApprovalsReviewer.AutoReview,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest),
            privatePolicy);
        string harness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            request,
            [new CopilotShellCommandTool()],
            new CopilotAgentEnvironmentContext(),
            taskLedgerEnabled: false,
            agentModeEnabled: true);
        string reviewerPrompt = CopilotAutomaticApprovalReviewer.BuildSystemPrompt(request);
        string defaultReviewerPrompt = CopilotAutomaticApprovalReviewer.BuildSystemPrompt(
            CreateRequest(
                Path.GetFullPath(Path.GetTempPath()),
                CopilotCodexApprovalsReviewer.AutoReview,
                CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest)));
        string invalidReviewerPrompt = CopilotAutomaticApprovalReviewer.BuildSystemPrompt(
            CreateRequest(
                Path.GetFullPath(Path.GetTempPath()),
                CopilotCodexApprovalsReviewer.AutoReview,
                CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest),
                "PRIVATE\0POLICY"));
        var guardianDisabledOptions = options with
        {
            ConfiguredGuardianApprovalEnabled = false,
            HasGuardianApprovalEnabledOverride = true,
            GuardianApprovalEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string guardianDisabledProjectReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                guardianDisabledOptions,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string guardianDisabledContextReport = CopilotContextDiagnostics.Format(
            new CopilotContextDiagnosticSnapshot
            {
                ProfileLabel = "Profile",
                Mode = CopilotAgentMode.Code,
                CodexApprovalsReviewer = CopilotCodexApprovalsReviewer.AutoReview,
                HasCodexApprovalsReviewerOverride = true,
                CodexApprovalsReviewerSourceLabel = options.ApprovalsReviewerSourceLabel,
                CodexGuardianApprovalEnabled = false,
                HasCodexGuardianApprovalEnabledOverride = true,
                CodexGuardianApprovalEnabledSourceLabel = guardianDisabledOptions.GuardianApprovalEnabledSourceLabel,
                CodexAutoReviewPolicyCharacters = privatePolicy.Length,
                HasCodexAutoReviewPolicyOverride = true,
                CodexAutoReviewPolicySourceLabel = options.AutoReviewPolicySourceLabel,
            });
        string guardianDisabledEffectiveReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = guardianDisabledOptions,
            });
        var guardianDisabledRequest = CreateRequest(
            Path.GetFullPath(Path.GetTempPath()),
            CopilotCodexApprovalsReviewer.AutoReview,
            CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest),
            guardianApprovalEnabled: false);
        string guardianDisabledHarness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            guardianDisabledRequest,
            [new CopilotShellCommandTool()],
            new CopilotAgentEnvironmentContext(),
            taskLedgerEnabled: false,
            agentModeEnabled: true);

        Assert.Contains("Codex approvals_reviewer：auto_review", projectReport, StringComparison.Ordinal);
        Assert.Contains("审批复核者：auto_review", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex auto_review.policy：", projectReport, StringComparison.Ordinal);
        Assert.Contains("自动审查策略：", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex auto_review.policy：", effectiveReport, StringComparison.Ordinal);
        Assert.Contains("approvals_reviewer=auto_review is frozen", harness, StringComparison.Ordinal);
        Assert.Contains("materially safer path", harness, StringComparison.Ordinal);
        Assert.Contains("reviewer only", harness, StringComparison.Ordinal);
        Assert.DoesNotContain(privatePolicy, projectReport, StringComparison.Ordinal);
        Assert.DoesNotContain(privatePolicy, contextReport, StringComparison.Ordinal);
        Assert.DoesNotContain(privatePolicy, effectiveReport, StringComparison.Ordinal);
        Assert.DoesNotContain(privatePolicy, harness, StringComparison.Ordinal);
        Assert.Contains(privatePolicy, reviewerPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Approve LOW or MEDIUM risk", reviewerPrompt, StringComparison.Ordinal);
        Assert.Contains("cannot change your reviewer-only role", reviewerPrompt, StringComparison.Ordinal);
        Assert.Contains("Approve LOW or MEDIUM risk", defaultReviewerPrompt, StringComparison.Ordinal);
        Assert.Contains("Approve LOW or MEDIUM risk", invalidReviewerPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE\0POLICY", invalidReviewerPrompt, StringComparison.Ordinal);
        Assert.Contains("Codex features.guardian_approval：false", guardianDisabledProjectReport, StringComparison.Ordinal);
        Assert.Contains("auto_review → 有效 user", guardianDisabledProjectReport, StringComparison.Ordinal);
        Assert.Contains("features.guardian_approval=false", guardianDisabledContextReport, StringComparison.Ordinal);
        Assert.Contains("有效 user", guardianDisabledContextReport, StringComparison.Ordinal);
        Assert.Contains("Codex features.guardian_approval：false", guardianDisabledEffectiveReport, StringComparison.Ordinal);
        Assert.Contains("auto_review → 有效 user", guardianDisabledEffectiveReport, StringComparison.Ordinal);
        Assert.Contains("features.guardian_approval=false is frozen", guardianDisabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("approvals_reviewer=auto_review is frozen", guardianDisabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain(privatePolicy, guardianDisabledProjectReport, StringComparison.Ordinal);
        Assert.DoesNotContain(privatePolicy, guardianDisabledContextReport, StringComparison.Ordinal);
        Assert.DoesNotContain(privatePolicy, guardianDisabledEffectiveReport, StringComparison.Ordinal);
        Assert.DoesNotContain(privatePolicy, guardianDisabledHarness, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomaticReviewCircuitBreakerTripsAfterThreeConsecutiveDenials()
    {
        var circuitBreaker = new CopilotAutomaticApprovalDenialCircuitBreaker();

        var first = circuitBreaker.Observe(CopilotAutomaticApprovalReviewVerdict.Deny);
        var second = circuitBreaker.Observe(CopilotAutomaticApprovalReviewVerdict.Deny);
        var third = circuitBreaker.Observe(CopilotAutomaticApprovalReviewVerdict.Deny);

        Assert.False(first.IsTripped);
        Assert.False(second.IsTripped);
        Assert.True(third.IsTripped);
        Assert.Equal(3, third.ConsecutiveDenials);
        Assert.Equal(3, third.DenialsInWindow);
        Assert.Equal(3, third.ReviewsInWindow);
        Assert.Contains("本轮已中断", third.FormatUserMessage(), StringComparison.Ordinal);
        Assert.Contains("no denied action was executed or retried", third.FormatDiagnostic(), StringComparison.Ordinal);
    }

    [Fact]
    public void NonDenialsResetTheConsecutiveCountButRollingTenDenialsStillTrip()
    {
        var circuitBreaker = new CopilotAutomaticApprovalDenialCircuitBreaker();
        for (var index = 0; index < 9; index++)
        {
            Assert.False(circuitBreaker.Observe(CopilotAutomaticApprovalReviewVerdict.Deny).IsTripped);
            Assert.False(circuitBreaker.Observe(
                index % 2 == 0
                    ? CopilotAutomaticApprovalReviewVerdict.Approve
                    : CopilotAutomaticApprovalReviewVerdict.Unavailable).IsTripped);
        }

        var tripped = circuitBreaker.Observe(CopilotAutomaticApprovalReviewVerdict.Deny);

        Assert.True(tripped.IsTripped);
        Assert.Equal(1, tripped.ConsecutiveDenials);
        Assert.Equal(10, tripped.DenialsInWindow);
        Assert.Equal(19, tripped.ReviewsInWindow);
    }

    [Fact]
    public void RollingReviewWindowEvictsOldDenialsAndUnavailableIsNotADenial()
    {
        var circuitBreaker = new CopilotAutomaticApprovalDenialCircuitBreaker();
        for (var index = 0; index < 9; index++)
        {
            circuitBreaker.Observe(CopilotAutomaticApprovalReviewVerdict.Deny);
            circuitBreaker.Observe(CopilotAutomaticApprovalReviewVerdict.Approve);
        }
        for (var index = 0; index < 33; index++)
            circuitBreaker.Observe(CopilotAutomaticApprovalReviewVerdict.Unavailable);

        var snapshot = circuitBreaker.Observe(CopilotAutomaticApprovalReviewVerdict.Deny);

        Assert.False(snapshot.IsTripped);
        Assert.Equal(1, snapshot.ConsecutiveDenials);
        Assert.Equal(9, snapshot.DenialsInWindow);
        Assert.Equal(CopilotAutomaticApprovalDenialCircuitBreaker.ReviewWindowSize, snapshot.ReviewsInWindow);
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
        CopilotCodexApprovalPolicy approvalPolicy,
        string autoReviewPolicy = "",
        bool guardianApprovalEnabled = true) => new()
    {
        ConversationId = "approvals-reviewer-conversation",
        TaskId = "approvals-reviewer-task",
        WorkspacePath = workspacePath,
        Profile = CopilotProfileConfig.CreateDefault(),
        UserText = "Run the requested shell command.",
        TaskIntentText = "Run and verify the requested shell command.",
        Mode = CopilotAgentMode.Code,
        CodexApprovalPolicy = approvalPolicy,
        CodexApprovalsReviewer = reviewer,
        CodexGuardianApprovalEnabled = guardianApprovalEnabled,
        CodexAutoReviewPolicy = autoReviewPolicy,
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

    private sealed class ProviderTimeoutChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ChatResponse>(
                new OperationCanceledException("The reviewer provider timed out."));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}

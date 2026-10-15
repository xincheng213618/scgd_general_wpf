using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using ColorVision.Solution.Explorer;
using Microsoft.Extensions.AI;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotCodexApprovalsReviewerTests
{
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

    [Theory]
    [InlineData("deny", 1, CopilotAgentControlIntent.None)]
    [InlineData("quota", 1, CopilotAgentControlIntent.None)]
    [InlineData("provider_canceled", 3, CopilotAgentControlIntent.None)]
    [InlineData("provider_canceled", 1, CopilotAgentControlIntent.Pause)]
    [InlineData("provider_canceled", 1, CopilotAgentControlIntent.Cancel)]
    public async Task RuntimeAutomaticReviewPreservesOfficialBillingWithoutExecutingClosedAction(
        string outcome, int reviewAttempts, CopilotAgentControlIntent controlIntent)
    {
        var controlled = controlIntent != CopilotAgentControlIntent.None;
        using var workspace = new ReviewerWorkspaceScope();
        using var provider = new BilledApprovalChatClient(outcome);
        var tool = new ApprovalBillingProbe();
        var catalog = new CopilotCapabilityCatalog();
        catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "approval-billing-tests", "Approval billing tests", [tool]);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(new CopilotToolRegistry([tool]),
            new CopilotAgentContextBuilder(), new CopilotToolExecutor(), _ => provider,
            new NoReviewExternalTools(), catalog, new CopilotAgentSkillUsageStore(workspace.Root),
            new CopilotAutomaticApprovalReviewer(), new CopilotAutomaticApprovalOverrideStore());
        var request = new CopilotAgentRequest
        {
            ConversationId = "approval-billing-conversation", TaskId = "approval-billing-task", WorkspacePath = workspace.Root,
            Profile = new CopilotProfileConfig
            {
                ProviderType = CopilotProviderType.OpenAICompatible, VendorType = CopilotVendorType.Custom,
                BaseUrl = "https://example.test/v1", ApiKey = "test-key", Model = "approval-billing-model",
            },
            UserText = "Review the requested protected operation and report whether it can proceed.",
            TaskIntentText = "Review the requested protected operation and report whether it can proceed.",
            Mode = CopilotAgentMode.Code, HarnessFeatures = CopilotAgentHarnessFeatures.None,
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest),
            CodexApprovalsReviewer = CopilotCodexApprovalsReviewer.AutoReview, CodexGuardianApprovalEnabled = true,
            RunControl = controlled ? new CopilotAgentRunControl() : null,
            RunBudgetOverride = new CopilotAgentRunBudgetOverride
            {
                RequestTokenBudget = 32_768, MaxToolCalls = 2, MaxAgentPasses = 1, TotalDuration = TimeSpan.FromSeconds(10),
            },
        };
        Assert.False(request.Profile.IsLocalCodex);
        Assert.True(CopilotAgentAccessPolicy.CanAutoReview(request, tool, workspace.Root));
        var events = new List<CopilotAgentEvent>();
        ConfirmableAction? action = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var result = await runtime.RunAsync(request, item =>
            {
                events.Add(item);
                if (item.ToolExecution?.State == CopilotToolExecutionState.AwaitingApproval)
                    action = Assert.Single(CopilotMcpConfirmationStore.Instance.GetPendingActions(), pending =>
                        pending.ActionId == item.ToolResult?.Approval?.ActionId);
                if (controlled && item.ProviderRetry != null)
                {
                    Assert.Equal(1, item.ProviderRetry.FailedAttempt);
                    Assert.Equal(40, events.Last(item => item.Budget != null).Budget!.ReportedTotalTokens);
                    Assert.Equal(1, provider.ReviewCalls);
                    Assert.Equal(1, provider.StreamingCalls);
                    Assert.Equal(0, tool.ExecutionCount);
                    Assert.True(controlIntent == CopilotAgentControlIntent.Pause
                        ? request.RunControl!.RequestPause()
                        : request.RunControl!.RequestCancel());
                    cancellation.Cancel();
                }
            }, cancellation.Token);

            var expectedTotal = (controlled ? 20 : 35) + reviewAttempts * 20;
            Assert.Equal(expectedTotal, result.Budget.ReportedTotalTokens);
            Assert.Equal((controlled ? 12 : 22) + reviewAttempts * 12, result.Budget.ReportedInputTokens);
            Assert.Equal((controlled ? 8 : 13) + reviewAttempts * 8, result.Budget.ReportedOutputTokens);
            Assert.Equal((controlled ? 3 : 5) + reviewAttempts * 3, result.Budget.ReportedCachedInputTokens);
            Assert.Equal(expectedTotal, result.Budget.ConsumedTokens);
            Assert.False(result.Budget.UsedEstimatedUsage);
            Assert.Equal(reviewAttempts + (controlled ? 1 : 2), result.Budget.ProviderCalls);
            Assert.Equal(reviewAttempts, provider.ReviewCalls);
            Assert.Equal(controlled ? 1 : 2, provider.StreamingCalls);
            Assert.Equal(controlled ? 1 : reviewAttempts - 1, events.Count(item => item.ProviderRetry != null));
            Assert.Equal(controlled, cancellation.IsCancellationRequested);
            Assert.Equal(0, tool.ExecutionCount);
            var closed = Assert.Single(result.StepRecords);
            Assert.Equal(controlled ? CopilotToolExecutionState.Cancelled : CopilotToolExecutionState.Denied, closed.Execution.State);
            Assert.Equal(controlled ? "approval_cancelled" : outcome == "deny" ? "automatic_review_denied" : "automatic_review_unavailable", closed.Observation.FailureCode);
            Assert.NotNull(action);
            Assert.Equal(controlled ? ConfirmableActionStatus.Cancelled : ConfirmableActionStatus.Rejected, action.Status);
            if (!controlled)
                Assert.Equal(outcome == "deny" ? "automatic-review" : "automatic-review-unavailable", action.ApprovalDecisionSource);
            Assert.DoesNotContain(action, CopilotMcpConfirmationStore.Instance.GetPendingActions());
            Assert.Equal(!controlled, provider.ReceivedClosedToolResult);
            if (controlled)
            {
                var expectedStopReason = controlIntent == CopilotAgentControlIntent.Pause
                    ? CopilotAgentStopReason.Paused : CopilotAgentStopReason.Cancelled;
                Assert.Equal(expectedStopReason, result.StopReason);
                Assert.Contains(result.TaskEventJournal.Events, item =>
                    item.Type == CopilotAgentTaskEventType.RunStopped && item.State == expectedStopReason.ToString());
                if (controlIntent == CopilotAgentControlIntent.Cancel)
                    Assert.Null(result.SessionCheckpoint);
                else
                {
                    Assert.NotNull(result.SessionCheckpoint);
                    Assert.False(string.IsNullOrWhiteSpace(result.SessionCheckpoint.SerializedSessionJson));
                    Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item =>
                        item.Type == CopilotAgentTaskEventType.RunStopped && item.State == CopilotAgentStopReason.Paused.ToString());
                }
            }
            Assert.Equal(expectedTotal, result.Usage.TotalTokens);
            Assert.Equal(result.Budget.ReportedInputTokens, result.Usage.InputTokens);
            Assert.Equal(result.Budget.ReportedOutputTokens, result.Usage.OutputTokens);
            Assert.Equal(result.Budget.ReportedCachedInputTokens, result.Usage.CachedInputTokens);
        }
        finally
        {
            if (action != null)
                new CopilotFrameworkApprovalCoordinator().Cancel(action.ActionId, "Approval billing test cleanup.");
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
            AgentContextEnabled = true,
            CodexApprovalsReviewer = CopilotCodexApprovalsReviewer.AutoReview,
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
                AgentContextEnabled = true,
                CodexApprovalsReviewer = CopilotCodexApprovalsReviewer.AutoReview,
                CodexGuardianApprovalEnabled = false,
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

        Assert.DoesNotContain("Codex approvals_reviewer", projectReport, StringComparison.Ordinal);
        Assert.Contains("有效复核者 auto_review", contextReport, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex auto_review.policy", projectReport, StringComparison.Ordinal);
        Assert.DoesNotContain("自动审查策略：", contextReport, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex auto_review.policy", effectiveReport, StringComparison.Ordinal);
        Assert.Contains("自动复核策略：", effectiveReport, StringComparison.Ordinal);
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
        Assert.DoesNotContain("Codex features.guardian_approval", guardianDisabledProjectReport, StringComparison.Ordinal);
        Assert.DoesNotContain("auto_review", guardianDisabledProjectReport, StringComparison.Ordinal);
        Assert.Contains("自动审批复核：关闭", guardianDisabledContextReport, StringComparison.Ordinal);
        Assert.Contains("有效复核者 user", guardianDisabledContextReport, StringComparison.Ordinal);
        Assert.Contains("guardian_approval：false", guardianDisabledEffectiveReport, StringComparison.Ordinal);
        Assert.Contains("approvals_reviewer（有效）：user", guardianDisabledEffectiveReport, StringComparison.Ordinal);
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

    private sealed class ReviewerWorkspaceScope : IDisposable
    {
        private static readonly FieldInfo Instance = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previous = Instance.GetValue(null);
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotReviewerBilling-").FullName;

        public ReviewerWorkspaceScope()
        {
            var manager = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
            var explorer = (SolutionExplorer)RuntimeHelpers.GetUninitializedObject(typeof(SolutionExplorer));
            typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.DirectoryInfo))!.SetValue(explorer, new DirectoryInfo(Root));
            typeof(SolutionManager).GetField("_CurrentSolutionExplorer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(manager, explorer);
            Instance.SetValue(null, manager);
        }

        public void Dispose()
        {
            Instance.SetValue(null, _previous);
            var resolved = Path.GetFullPath(Root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(resolved), ignoreCase: true);
            Assert.StartsWith("CopilotReviewerBilling-", Path.GetFileName(resolved), StringComparison.Ordinal);
            Directory.Delete(resolved, recursive: true);
        }
    }

    private sealed class NoReviewExternalTools : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new CopilotExternalToolLease());
    }

    private sealed class ApprovalBillingProbe : ICopilotAgentDrivenTool, ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation
    {
        public string Name => "ApprovalBillingProbe";
        public string Description => "A protected deterministic operation used to verify the approval boundary.";
        public CopilotToolCapabilityDescriptor Capability => CopilotToolCapabilityDescriptor.ProtectedWrite(CopilotToolIdempotency.NonIdempotent);
        public int ExecutionCount { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;
        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput input)
            => new("Review the protected operation", "The operation must remain closed when automatic review denies it or fails.")
            {
                ReviewDetails = "Complete operation: run the deterministic ApprovalBillingProbe once, with no arguments.",
            };
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken token)
            => throw new InvalidOperationException("Protected execution must use the approved entry point.");
        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ExecutionCount++;
            return Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true, Summary = "Protected operation executed." });
        }
    }

    private sealed class BilledApprovalChatClient(string outcome) : IChatClient
    {
        public int ReviewCalls { get; private set; }
        public int StreamingCalls { get; private set; }
        public bool ReceivedClosedToolResult { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Assert.False(cancellationToken.IsCancellationRequested);
            Assert.Empty(options!.Tools!);
            Assert.Contains("Complete operation:", string.Concat(messages.Select(message => message.Text)), StringComparison.Ordinal);
            ReviewCalls++;
            if (outcome == "deny")
                return Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant,
                    "VERDICT: DENY\nRISK: LOW\nREASON: Keep the controlled protected operation closed."))
                {
                    FinishReason = ChatFinishReason.Stop, Usage = CreateUsage(12, 8, 3),
                });
            Exception failure = outcome == "quota"
                ? new CopilotProviderPayloadException("The reviewer quota is exhausted.", "insufficient_quota", false, string.Empty, new CopilotTokenUsage(12, 8, 20, 3))
                : new OperationCanceledException("The reviewer provider canceled its own request.");
            CopilotTokenBudgetChatClient.PreserveSettledFailureUsage(failure, new CopilotTokenUsage(12, 8, 20, 3));
            return Task.FromException<ChatResponse>(failure);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            cancellationToken.ThrowIfCancellationRequested();
            StreamingCalls++;
            if (StreamingCalls == 1)
            {
                var function = Assert.Single(options!.Tools!.OfType<AIFunction>(), item => item.Name ==
                    CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.ToFunctionName("ApprovalBillingProbe"));
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    [new FunctionCallContent("approval-billing-call", function.Name, new Dictionary<string, object?>()), new UsageContent(CreateUsage(12, 8, 3))])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
            }
            else
            {
                Assert.Equal(2, StreamingCalls);
                ReceivedClosedToolResult = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>()
                    .Any(result => result.CallId == "approval-billing-call");
                Assert.True(ReceivedClosedToolResult);
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    [new TextContent("The protected operation stayed closed."), new UsageContent(CreateUsage(10, 5, 2))])
                {
                    FinishReason = ChatFinishReason.Stop,
                };
            }
        }

        private static UsageDetails CreateUsage(int input, int output, int cached) => new()
        {
            InputTokenCount = input, OutputTokenCount = output, TotalTokenCount = input + output, CachedInputTokenCount = cached,
        };
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
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

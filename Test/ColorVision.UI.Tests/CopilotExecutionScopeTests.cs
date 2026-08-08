using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotExecutionScopeTests
{
    [Fact]
    public void EquivalentAgentScopeNormalizesIdentifiersAndWorkspace()
    {
        var first = CopilotExecutionScope.ForAgentRequest(new CopilotAgentRequest
        {
            ConversationId = " conversation-1 ",
            TaskId = " task-1 ",
            WorkspacePath = @"C:\ColorVision\Scope\.",
        });
        var second = CopilotExecutionScope.ForAgentRequest(new CopilotAgentRequest
        {
            ConversationId = "conversation-1",
            TaskId = "task-1",
            WorkspacePath = @"C:\ColorVision\Scope",
        });

        Assert.True(first.MatchesAuthorizationScope(second));
        Assert.Equal(first.AuthorizationScopeId, second.AuthorizationScopeId);
        Assert.Equal("task-1", first.RunId);
        Assert.StartsWith("workspace:", first.WorkspaceIdentity, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentAuthorizationRejectsConversationTaskRunWorkspaceChannelOrCapabilityDrift()
    {
        var request = new CopilotAgentRequest
        {
            ConversationId = "conversation-1",
            TaskId = "task-1",
            WorkspacePath = @"C:\ColorVision\Scope",
        };
        var baseline = CopilotExecutionScope.ForAgentRequest(request, runId: "run-1")
            .WithRuntimeSnapshot("workspace-snapshot-a", capabilityRevision: 17);

        Assert.False(baseline.MatchesAuthorizationScope(
            CopilotExecutionScope.ForAgentRequest(new CopilotAgentRequest
            {
                ConversationId = "conversation-2",
                TaskId = request.TaskId,
                WorkspacePath = request.WorkspacePath,
            }, runId: "run-1").WithRuntimeSnapshot("workspace-snapshot-a", 17)));
        Assert.False(baseline.MatchesAuthorizationScope(
            CopilotExecutionScope.ForAgentRequest(new CopilotAgentRequest
            {
                ConversationId = request.ConversationId,
                TaskId = "task-2",
                WorkspacePath = request.WorkspacePath,
            }, runId: "run-1").WithRuntimeSnapshot("workspace-snapshot-a", 17)));
        Assert.False(baseline.MatchesAuthorizationScope(
            CopilotExecutionScope.ForAgentRequest(request, runId: "run-2")
                .WithRuntimeSnapshot("workspace-snapshot-a", 17)));
        Assert.False(baseline.MatchesAuthorizationScope(
            CopilotExecutionScope.ForAgentRequest(new CopilotAgentRequest
            {
                ConversationId = request.ConversationId,
                TaskId = request.TaskId,
                WorkspacePath = @"C:\ColorVision\Other",
            }, runId: "run-1").WithRuntimeSnapshot("workspace-snapshot-a", 17)));
        Assert.False(baseline.MatchesAuthorizationScope(
            baseline.WithAuthorizationChannel(CopilotExecutionAuthorizationChannel.AgentFrameworkApproved)));
        Assert.False(baseline.MatchesAuthorizationScope(
            baseline.WithRuntimeSnapshot("workspace-snapshot-a", capabilityRevision: 18)));
    }

    [Fact]
    public void WorkspaceSnapshotIsAuditMetadataUntilCapabilityContractChanges()
    {
        var baseline = CopilotExecutionScope.ForAgentRequest(new CopilotAgentRequest
        {
            ConversationId = "conversation-1",
            TaskId = "task-1",
            WorkspacePath = @"C:\ColorVision\Scope",
        }).WithRuntimeSnapshot("snapshot-a", capabilityRevision: 4);
        var changedSnapshot = baseline.WithRuntimeSnapshot("snapshot-b", capabilityRevision: 4);

        Assert.True(baseline.MatchesAuthorizationScope(changedSnapshot));
        Assert.NotEqual(baseline.ScopeId, changedSnapshot.ScopeId);
    }

    [Fact]
    public void McpSessionIdentityIsOneWayAndCannotCrossSessions()
    {
        var first = CopilotExecutionScope.ForExternalMcpSession(
            new string('a', 64),
            "mcp-session://caller",
            @"C:\ColorVision\Scope");
        var second = CopilotExecutionScope.ForExternalMcpSession(
            new string('b', 64),
            "mcp-session://caller",
            @"C:\ColorVision\Scope");

        Assert.StartsWith("mcp:", first.SessionIdentity, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('a', 64), first.SessionIdentity, StringComparison.Ordinal);
        Assert.False(first.MatchesAuthorizationScope(second));
    }

    [Fact]
    public void ToolBindingRequiresExactToolCallIdAndExecutionSignature()
    {
        var scope = CopilotExecutionScope.ForAgentRequest(new CopilotAgentRequest
        {
            ConversationId = "conversation-1",
            TaskId = "task-1",
            WorkspacePath = @"C:\ColorVision\Scope",
        });
        var baseline = scope.BindToolCall("WriteFile", "call-1", "signature-1");

        Assert.True(baseline.MatchesOperationBinding(
            scope.BindToolCall("WriteFile", "call-1", "signature-1")));
        Assert.False(baseline.MatchesOperationBinding(
            scope.BindToolCall("WriteFile", "call-2", "signature-1")));
        Assert.False(baseline.MatchesOperationBinding(
            scope.BindToolCall("WriteFile", "call-1", "signature-2")));
        Assert.False(baseline.MatchesOperationBinding(
            scope.BindToolCall("RunCommand", "call-1", "signature-1")));
        Assert.False(baseline.MatchesOperationBinding(scope));
    }

    [Fact]
    public void ChildRunPreservesTraceAndRecordsParentWithoutInheritingAuthorization()
    {
        var parent = CopilotExecutionScope.ForAgentRequest(new CopilotAgentRequest
        {
            ConversationId = "conversation-1",
            TaskId = "task-1",
            WorkspacePath = @"C:\ColorVision\Scope",
        }, runId: "run-parent");

        var child = parent.DeriveChild("run-child");

        Assert.Equal(parent.TraceId, child.TraceId);
        Assert.Equal(parent.RunId, child.ParentRunId);
        Assert.Equal(parent.WorkspaceIdentity, child.WorkspaceIdentity);
        Assert.False(parent.MatchesAuthorizationScope(child));
    }

    [Fact]
    public void AgentRuntimeScopeAndTaskJournalShareTheSameRunId()
    {
        var request = new CopilotAgentRequest
        {
            ConversationId = "conversation-1",
            TaskId = "legacy-task-id",
            WorkspacePath = @"C:\ColorVision\Scope",
        };

        var scope = CopilotExecutionScope.ForAgentRun(request);
        var journal = new CopilotAgentTaskEventJournalBuilder(runId: scope.RunId);

        Assert.Equal(scope.RunId, journal.RunId);
        Assert.True(CopilotAgentTaskEventIds.IsKey(scope.RunId, "run", 32));
    }

    [Fact]
    public void AgentRunPreservesAValidPreboundRuntimeScope()
    {
        var request = new CopilotAgentRequest
        {
            ConversationId = "conversation-1",
            TaskId = "task-1",
            WorkspacePath = @"C:\ColorVision\Scope",
        };
        var preboundScope = CopilotExecutionScope.ForAgentRequest(
                request,
                runId: CopilotAgentTaskEventIds.CreateRunId())
            .WithRuntimeSnapshot("snapshot-1", capabilityRevision: 17);
        request.RuntimeExecutionScope = preboundScope;

        Assert.Same(preboundScope, CopilotExecutionScope.ForAgentRun(request));
    }

    [Fact]
    public void SubagentAndFinalizationRequestsDeriveFreshRuntimeScopes()
    {
        var parentRequest = new CopilotAgentRequest
        {
            ConversationId = "conversation-1",
            TaskId = "task-1",
            WorkspacePath = @"C:\ColorVision\Scope",
            UserText = "Inspect the workspace.",
            Profile = new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1",
                Model = "test-model",
                MaxTokens = 4_096,
            },
            SearchRootPaths = [@"C:\ColorVision\Scope"],
            TrustedProjectRootPaths = [@"C:\ColorVision\Scope"],
            ConfiguredDeveloperInstructions = "Keep configured guidance.",
            CodexWebSearchMode = CopilotCodexWebSearchMode.Cached,
            ToolOutputTokenLimitOverride = 12_000,
            CodexReasoningEffort = CopilotCodexReasoningEffort.XHigh,
            CodexReasoningSummary = CopilotCodexReasoningSummary.Concise,
            CodexServiceTier = "fast",
            CodexModelVerbosity = CopilotCodexModelVerbosity.High,
            Mode = CopilotAgentMode.Code,
        };
        var parentScope = CopilotExecutionScope.ForAgentRequest(
                parentRequest,
                runId: CopilotAgentTaskEventIds.CreateRunId())
            .WithAuthorizationChannel(CopilotExecutionAuthorizationChannel.AgentFrameworkApproved)
            .WithRuntimeSnapshot("snapshot-1", capabilityRevision: 17)
            .BindToolCall("DelegateExplore", "call-1", "signature-1");
        parentRequest.RuntimeExecutionScope = parentScope;
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);

        var childRequest = CopilotSubagentRunner.CreateChildRequest(
            parentRequest,
            role,
            new CopilotSubagentRunRequest
            {
                RunId = "legacy-coordinator-run-id",
                Task = "Inspect one source file.",
                RequestTokenBudget = 16_384,
            });
        var childScope = childRequest.RuntimeExecutionScope;

        AssertDerivedRunScope(parentScope, childScope);
        Assert.Same(childScope, CopilotExecutionScope.ForAgentRun(childRequest));
        Assert.Equal(parentRequest.ConfiguredDeveloperInstructions, childRequest.ConfiguredDeveloperInstructions);
        Assert.Equal(parentRequest.CodexWebSearchMode, childRequest.CodexWebSearchMode);
        Assert.Equal(parentRequest.ToolOutputTokenLimitOverride, childRequest.ToolOutputTokenLimitOverride);
        Assert.Equal(parentRequest.CodexReasoningEffort, childRequest.CodexReasoningEffort);
        Assert.Equal(parentRequest.CodexReasoningSummary, childRequest.CodexReasoningSummary);
        Assert.Equal(parentRequest.CodexServiceTier, childRequest.CodexServiceTier);
        Assert.Equal(parentRequest.CodexModelVerbosity, childRequest.CodexModelVerbosity);

        var finalizationRequest = Assert.IsType<CopilotAgentRequest>(
            CopilotSubagentRunner.CreateBudgetFinalizationRequest(
                childRequest,
                role,
                new CopilotAgentRunResult
                {
                    StopReason = CopilotAgentStopReason.BudgetExhausted,
                    Budget = new CopilotAgentBudgetSnapshot
                    {
                        RequestTokenBudget = 16_384,
                        ConsumedTokens = 8_192,
                        RequestTokenBudgetExhausted = true,
                    },
                    StepRecords =
                    [
                        new CopilotAgentStepRecord
                        {
                            ToolCall = new CopilotToolCall
                            {
                                ToolName = "ReadLocalFile",
                            },
                            Observation = new CopilotToolObservation
                            {
                                Success = true,
                                Summary = "Verified source evidence.",
                                Content = "L1: verified source evidence",
                            },
                        },
                    ],
                },
                totalTokenBudget: 16_384,
                elapsed: TimeSpan.Zero));
        var finalizationScope = finalizationRequest.RuntimeExecutionScope;

        AssertDerivedRunScope(childScope, finalizationScope);
        Assert.Same(finalizationScope, CopilotExecutionScope.ForAgentRun(finalizationRequest));
        Assert.Equal(childRequest.ConfiguredDeveloperInstructions, finalizationRequest.ConfiguredDeveloperInstructions);
        Assert.Equal(childRequest.CodexWebSearchMode, finalizationRequest.CodexWebSearchMode);
        Assert.Equal(childRequest.ToolOutputTokenLimitOverride, finalizationRequest.ToolOutputTokenLimitOverride);
        Assert.Equal(childRequest.CodexReasoningEffort, finalizationRequest.CodexReasoningEffort);
        Assert.Equal(childRequest.CodexReasoningSummary, finalizationRequest.CodexReasoningSummary);
        Assert.Equal(childRequest.CodexServiceTier, finalizationRequest.CodexServiceTier);
        Assert.Equal(childRequest.CodexModelVerbosity, finalizationRequest.CodexModelVerbosity);
        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.CanUseMinimalDelegatedFinalizationInstructions(
            finalizationRequest,
            [],
            taskLedgerEnabled: false,
            agentModeEnabled: false));
        var finalizationHarness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            finalizationRequest,
            [],
            CopilotAgentEnvironmentContext.Capture(finalizationRequest),
            taskLedgerEnabled: false,
            agentModeEnabled: false);
        Assert.Contains("Keep configured guidance.", finalizationHarness, StringComparison.Ordinal);
        Assert.True(
            finalizationHarness.IndexOf("# Configured Codex developer instructions", StringComparison.Ordinal)
                < finalizationHarness.IndexOf(
                    "The no-tools role boundary and evidence-only finalization contract remain authoritative.",
                    StringComparison.Ordinal));
    }

    private static void AssertDerivedRunScope(
        CopilotExecutionScope parent,
        CopilotExecutionScope child)
    {
        Assert.True(CopilotAgentTaskEventIds.IsKey(child.RunId, "run", 32));
        Assert.NotEqual(parent.RunId, child.RunId);
        Assert.Equal(parent.RunId, child.ParentRunId);
        Assert.Equal(parent.SourceKind, child.SourceKind);
        Assert.Equal(parent.AuthorizationChannel, child.AuthorizationChannel);
        Assert.Equal(parent.SessionIdentity, child.SessionIdentity);
        Assert.Equal(parent.ConversationId, child.ConversationId);
        Assert.Equal(parent.TaskId, child.TaskId);
        Assert.Equal(parent.CallerIdentity, child.CallerIdentity);
        Assert.Equal(parent.WorkspacePath, child.WorkspacePath);
        Assert.Equal(parent.WorkspaceSnapshotId, child.WorkspaceSnapshotId);
        Assert.Equal(parent.TraceId, child.TraceId);
        Assert.Equal(parent.CapabilityRevision, child.CapabilityRevision);
        Assert.False(child.HasToolCallBinding);
    }
}

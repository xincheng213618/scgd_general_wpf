using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotDelegateSubagentToolTests
{
    [Fact]
    public async Task CompletedAnswerIsReportedAsSuccessful()
    {
        var tool = new CopilotDelegateExploreTool(new StubRunner(new CopilotSubagentResult
        {
            Answer = "Verified finding.",
            StopReason = CopilotAgentStopReason.Completed,
            HasSuccessfulEvidence = true,
            UsedPreselectedEvidence = true,
            ToolNames = ["ReadLocalFile"],
            Budget = new CopilotAgentBudgetSnapshot
            {
                ToolCalls = 1,
                ProviderCalls = 3,
                PeakEstimatedInputTokens = 12_000,
                ProviderRetryCount = 2,
                ProviderRateLimitRetryCount = 1,
                ProviderRetryDelayMs = 1_500,
                ProviderFirstContentTimeoutCount = 1,
                ProviderStreamInactivityTimeoutCount = 1,
                ProviderResponseCount = 2,
                ProviderFirstResponseLatencyTotalMs = 900,
                ProviderFirstResponseLatencyMaxMs = 550,
                ProviderCallDurationTotalMs = 2_800,
                ProviderStreamChunkCount = 5,
                ProviderStreamInterChunkLatencyCount = 3,
                ProviderStreamInterChunkLatencyTotalMs = 240,
                ProviderStreamInterChunkLatencyMaxMs = 120,
                ContextRecoveryCount = 2,
                ContextRecoveryEstimatedInputTokensBefore = 40_000,
                ContextRecoveryEstimatedInputTokensAfter = 18_000,
                RegisteredToolCount = 48,
                AvailableToolCount = 3,
                AvailableToolDefinitionCharacters = 2_048,
                HarnessInstructionCharacters = 6_144,
            },
        }));

        var result = await tool.ExecuteAsync(Request(), Input(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CopilotToolFailureKind.None, result.FailureKind);
        Assert.Contains("Verified finding.", result.Content, StringComparison.Ordinal);
        Assert.Contains("preselected_evidence: true", result.Content, StringComparison.Ordinal);
        Assert.Equal(CopilotAgentStopReason.Completed, result.DelegatedRunUsage?.StopReason);
        Assert.Equal(3, result.DelegatedRunUsage?.ProviderCalls);
        Assert.Equal(12_000, result.DelegatedRunUsage?.PeakEstimatedInputTokens);
        Assert.Equal(2, result.DelegatedRunUsage?.ProviderRetryCount);
        Assert.Equal(1, result.DelegatedRunUsage?.ProviderRateLimitRetryCount);
        Assert.Equal(1_500, result.DelegatedRunUsage?.ProviderRetryDelayMs);
        Assert.Equal(1, result.DelegatedRunUsage?.ProviderFirstContentTimeoutCount);
        Assert.Equal(1, result.DelegatedRunUsage?.ProviderStreamInactivityTimeoutCount);
        Assert.Equal(2, result.DelegatedRunUsage?.ProviderResponseCount);
        Assert.Equal(900, result.DelegatedRunUsage?.ProviderFirstResponseLatencyTotalMs);
        Assert.Equal(550, result.DelegatedRunUsage?.ProviderFirstResponseLatencyMaxMs);
        Assert.Equal(2_800, result.DelegatedRunUsage?.ProviderCallDurationTotalMs);
        Assert.Equal(5, result.DelegatedRunUsage?.ProviderStreamChunkCount);
        Assert.Equal(3, result.DelegatedRunUsage?.ProviderStreamInterChunkLatencyCount);
        Assert.Equal(240, result.DelegatedRunUsage?.ProviderStreamInterChunkLatencyTotalMs);
        Assert.Equal(120, result.DelegatedRunUsage?.ProviderStreamInterChunkLatencyMaxMs);
        Assert.Equal(2, result.DelegatedRunUsage?.ContextRecoveryCount);
        Assert.Equal(40_000, result.DelegatedRunUsage?.ContextRecoveryEstimatedInputTokensBefore);
        Assert.Equal(18_000, result.DelegatedRunUsage?.ContextRecoveryEstimatedInputTokensAfter);
        Assert.Equal(48, result.DelegatedRunUsage?.RegisteredToolCount);
        Assert.Equal(3, result.DelegatedRunUsage?.AvailableToolCount);
        Assert.Equal(2_048, result.DelegatedRunUsage?.AvailableToolDefinitionCharacters);
        Assert.Equal(6_144, result.DelegatedRunUsage?.HarnessInstructionCharacters);
        var delegatedAnswer = Assert.IsType<CopilotDelegatedAnswer>(result.DelegatedAnswer);
        Assert.Equal("Verified finding.", delegatedAnswer.Text);
        Assert.Equal(CopilotAgentStopReason.Completed, delegatedAnswer.StopReason);
        Assert.True(delegatedAnswer.HasSuccessfulEvidence);
        Assert.False(delegatedAnswer.WasTruncated);
    }

    [Fact]
    public async Task CompletedTextWithoutSuccessfulToolEvidenceIsRejected()
    {
        var tool = new CopilotDelegateExploreTool(new StubRunner(new CopilotSubagentResult
        {
            Answer = "```tool_call\n{\"tool\":\"ReadFile\"}\n```",
            StopReason = CopilotAgentStopReason.Completed,
        }));

        var result = await tool.ExecuteAsync(Request(), Input(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Contains("without successful request-scoped tool evidence", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("successful_tool_evidence: false", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BudgetExhaustedAnswerIsPreservedButNotReportedAsSuccessful()
    {
        var tool = new CopilotDelegateExploreTool(new StubRunner(new CopilotSubagentResult
        {
            Answer = "Partial observation.",
            StopReason = CopilotAgentStopReason.BudgetExhausted,
        }));

        var result = await tool.ExecuteAsync(Request(), Input(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Contains("部分结果", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Partial observation.", result.Content, StringComparison.Ordinal);
        Assert.Contains("evidence only", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(CopilotAgentStopReason.BudgetExhausted, result.DelegatedRunUsage?.StopReason);
        Assert.Equal(CopilotAgentStopReason.BudgetExhausted, result.DelegatedAnswer?.StopReason);
    }

    [Fact]
    public async Task CancelledAnswerUsesCancelledFailureKind()
    {
        var tool = new CopilotDelegateExploreTool(new StubRunner(new CopilotSubagentResult
        {
            Answer = "Interrupted observation.",
            StopReason = CopilotAgentStopReason.Cancelled,
        }));

        var result = await tool.ExecuteAsync(Request(), Input(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Cancelled, result.FailureKind);
    }

    [Fact]
    public async Task CompletedRunCanResumeItsSerializedSessionWithinTheSameParentRequest()
    {
        var firstCheckpoint = CreateCheckpoint("first");
        var secondCheckpoint = CreateCheckpoint("second");
        var runner = new StubRunner(
            CompletedResult("Initial evidence.", firstCheckpoint),
            CompletedResult("Follow-up evidence.", secondCheckpoint, sessionResumed: true));
        var tool = new CopilotDelegateExploreTool(runner);
        var request = Request();

        var first = await tool.ExecuteAsync(request, Input(), CancellationToken.None);
        var firstRunId = Assert.IsType<CopilotDelegatedRunUsage>(first.DelegatedRunUsage).RunId;
        var second = await tool.ExecuteAsync(request, Input(firstRunId), CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.NotEqual(firstRunId, second.DelegatedRunUsage?.RunId);
        Assert.Equal(firstRunId, second.DelegatedRunUsage?.ResumeFromRunId);
        Assert.Contains($"resumed_from: {firstRunId}", second.Content, StringComparison.Ordinal);
        Assert.Contains(
            $"\"resumed_from\":\"{firstRunId}\"",
            CopilotFrameworkToolResultFormatter.Format(new CopilotToolExecutionOutcome
            {
                Result = second,
                Execution = new CopilotToolExecutionInfo { ToolName = "DelegateExplore" },
            }),
            StringComparison.Ordinal);
        Assert.Equal(2, runner.Requests.Count);
        Assert.Equal(firstRunId, runner.Requests[1].ResumeFromRunId);
        Assert.Same(firstCheckpoint, runner.Requests[1].ResumeCheckpoint);
        Assert.Contains("resume_succeeded: true", second.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResumeHandleCannotCrossParentRequests()
    {
        var runner = new StubRunner(CompletedResult("Initial evidence.", CreateCheckpoint("first")));
        var tool = new CopilotDelegateExploreTool(runner);
        var first = await tool.ExecuteAsync(Request(), Input(), CancellationToken.None);
        var firstRunId = Assert.IsType<CopilotDelegatedRunUsage>(first.DelegatedRunUsage).RunId;

        var rejected = await tool.ExecuteAsync(Request(), Input(firstRunId), CancellationToken.None);

        Assert.False(rejected.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, rejected.FailureKind);
        Assert.Contains("not a completed run from this parent request", rejected.ErrorMessage, StringComparison.Ordinal);
        Assert.Single(runner.Requests);
    }

    [Fact]
    public async Task CompletedRunWithoutCheckpointFailsClosedOnResume()
    {
        var runner = new StubRunner(CompletedResult("Initial evidence.", checkpoint: null));
        var tool = new CopilotDelegateExploreTool(runner);
        var request = Request();
        var first = await tool.ExecuteAsync(request, Input(), CancellationToken.None);
        var firstRunId = Assert.IsType<CopilotDelegatedRunUsage>(first.DelegatedRunUsage).RunId;

        var rejected = await tool.ExecuteAsync(request, Input(firstRunId), CancellationToken.None);

        Assert.False(rejected.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, rejected.FailureKind);
        Assert.Contains("did not produce a structurally valid resumable checkpoint", rejected.ErrorMessage, StringComparison.Ordinal);
        Assert.Single(runner.Requests);
    }

    [Fact]
    public async Task ActiveRunAndDifferentRoleHandlesFailClosed()
    {
        var request = Request();
        var coordinator = new CopilotSubagentCoordinator(request);
        var lease = await coordinator.TryAcquireAsync(
            CopilotSubagentRoleCatalog.ExploreRoleId,
            CancellationToken.None);
        Assert.NotNull(lease);
        var checkpoint = CreateCheckpoint("active");
        try
        {
            Assert.False(coordinator.TryResolveCompletedRun(
                CopilotSubagentRoleCatalog.ExploreRoleId,
                lease.RunId,
                out _,
                out var activeFailureKind,
                out var activeError));
            Assert.Equal(CopilotToolFailureKind.Conflict, activeFailureKind);
            Assert.Contains("still active", activeError, StringComparison.Ordinal);
            coordinator.RecordCompleted(
                CopilotSubagentRoleCatalog.ExploreRoleId,
                lease.RunId,
                checkpoint);
        }
        finally
        {
            lease.Dispose();
        }

        Assert.False(coordinator.TryResolveCompletedRun(
            CopilotSubagentRoleCatalog.ScoutRoleId,
            lease.RunId,
            out _,
            out var roleFailureKind,
            out var roleError));
        Assert.Equal(CopilotToolFailureKind.Validation, roleFailureKind);
        Assert.Contains("belongs to role 'explore'", roleError, StringComparison.Ordinal);
        Assert.True(coordinator.TryResolveCompletedRun(
            CopilotSubagentRoleCatalog.ExploreRoleId,
            lease.RunId,
            out var resolvedCheckpoint,
            out _,
            out _));
        Assert.Same(checkpoint, resolvedCheckpoint);
    }

    [Fact]
    public async Task FreshFallbackAfterRequestedResumeIsRejectedAndCannotBeChained()
    {
        var runner = new StubRunner(
            CompletedResult("Initial evidence.", CreateCheckpoint("first")),
            CompletedResult("Fresh fallback evidence.", CreateCheckpoint("fallback")));
        var tool = new CopilotDelegateExploreTool(runner);
        var request = Request();
        var first = await tool.ExecuteAsync(request, Input(), CancellationToken.None);
        var firstRunId = Assert.IsType<CopilotDelegatedRunUsage>(first.DelegatedRunUsage).RunId;

        var rejected = await tool.ExecuteAsync(request, Input(firstRunId), CancellationToken.None);

        Assert.False(rejected.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, rejected.FailureKind);
        Assert.Contains("did not resume", rejected.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resume_succeeded: false", rejected.Content, StringComparison.Ordinal);
        var rejectedRunId = Assert.IsType<CopilotDelegatedRunUsage>(rejected.DelegatedRunUsage).RunId;
        var chained = await tool.ExecuteAsync(request, Input(rejectedRunId), CancellationToken.None);
        Assert.False(chained.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, chained.FailureKind);
        Assert.Contains("not a completed run", chained.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(2, runner.Requests.Count);
    }

    [Fact]
    public void ChildRequestInjectsTheResolvedCheckpointWithoutVisibleHistoryReplay()
    {
        var checkpoint = CreateCheckpoint("source");
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
        var parent = Request();
        var child = CopilotSubagentRunner.CreateChildRequest(
            parent,
            role,
            new CopilotSubagentRunRequest
            {
                RunId = "explore-child",
                ResumeFromRunId = "explore-source",
                ResumeCheckpoint = checkpoint,
                Task = "Continue the bounded investigation.",
                RequestTokenBudget = CopilotAgentRunBudget.MinimumRequestTokenBudget,
            });

        Assert.Same(checkpoint, child.SessionCheckpoint);
        Assert.Empty(child.History);
    }

    [Fact]
    public void SameRoleFollowUpKeepsCheckpointRuntimeSurfacesCompatible()
    {
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
        var parent = Request();
        var firstChild = CopilotSubagentRunner.CreateChildRequest(
            parent,
            role,
            new CopilotSubagentRunRequest
            {
                RunId = "explore-first",
                Task = "Inspect the workspace and return one verified finding.",
                RequestTokenBudget = CopilotAgentRunBudget.MinimumRequestTokenBudget,
            });
        var tools = role.CreateTools();
        var toolNames = tools.Select(tool => tool.Name).ToArray();
        var catalog = new CopilotCapabilityCatalog();
        var capabilitySnapshot = catalog.PublishSource(
            CopilotCapabilitySourceKind.BuiltIn,
            role.Id,
            "ColorVision " + role.DisplayName,
            tools);
        var toolExecutor = new CopilotToolExecutor();
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            firstChild.Profile,
            "{}",
            capabilitySnapshot,
            availableToolNames: toolNames,
            environmentContext: CopilotAgentEnvironmentContext.Capture(firstChild),
            taskIntentText: firstChild.TaskIntentText,
            hookSurfaceSnapshot: toolExecutor.GetHookSurfaceSnapshot());
        Assert.NotNull(checkpoint);
        var resumedChild = CopilotSubagentRunner.CreateChildRequest(
            parent,
            role,
            new CopilotSubagentRunRequest
            {
                RunId = "explore-follow-up",
                ResumeFromRunId = "explore-first",
                ResumeCheckpoint = checkpoint,
                Task = "Continue from the prior evidence and verify the remaining call path.",
                RequestTokenBudget = CopilotAgentRunBudget.MinimumRequestTokenBudget,
            });

        var compatibility = checkpoint.EvaluateFor(
            resumedChild.Profile,
            capabilitySnapshot,
            toolNames,
            CopilotAgentEnvironmentContext.Capture(resumedChild),
            toolExecutor.GetHookSurfaceSnapshot());

        Assert.True(compatibility.CanResume);
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.Compatible, compatibility.Kind);
        Assert.Empty(resumedChild.History);
    }

    [Fact]
    public void ResumedChildCannotUseThePreselectedEvidenceShortcut()
    {
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Code,
            Profile = new CopilotProfileConfig(),
            UserText = "Read Coordinator.cs and Explore.cs.",
            TaskIntentText = "Review the two named files.",
            ReadableLocalFilePaths = [@"C:\workspace\Coordinator.cs", @"C:\workspace\Explore.cs"],
            PreferBatchReadLocalFiles = true,
            SessionCheckpoint = CreateCheckpoint("resume"),
        };

        Assert.False(CopilotSubagentRunner.CanUsePreselectedEvidence(request, role));
    }

    [Fact]
    public async Task IncompatibleCheckpointFailsBeforeCreatingAProviderClient()
    {
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
        var runner = new CopilotSubagentRunner(_ => throw new InvalidOperationException("provider must not be created"));

        var result = await runner.RunAsync(
            Request(),
            role,
            new CopilotSubagentRunRequest
            {
                RunId = "explore-incompatible",
                ResumeFromRunId = "explore-source",
                ResumeCheckpoint = CreateCheckpoint("wrong-profile"),
                Task = "Continue the bounded investigation.",
                RequestTokenBudget = CopilotAgentRunBudget.MinimumRequestTokenBudget,
            },
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Interrupted, result.StopReason);
        Assert.False(result.SessionResumed);
        Assert.Contains("ProfileChanged", result.ResumeFailureReason, StringComparison.Ordinal);
        Assert.Null(result.SessionCheckpoint);
    }

    private static CopilotAgentRequest Request()
    {
        return new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            Profile = new CopilotProfileConfig(),
            UserText = @"只读审计 C:\workspace，列出 1 条可验证的问题；不要修改文件。",
            SearchRootPaths = [@"C:\workspace"],
        };
    }

    private static CopilotAgentToolInput Input(string? resumeFromRunId = null)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["task"] = "Inspect the workspace and return one verified finding.",
        };
        if (!string.IsNullOrWhiteSpace(resumeFromRunId))
            arguments["resume_from"] = resumeFromRunId;
        return new CopilotAgentToolInput
        {
            Arguments = arguments,
        };
    }

    private static CopilotSubagentResult CompletedResult(
        string answer,
        CopilotAgentSessionCheckpoint? checkpoint,
        bool sessionResumed = false) => new()
    {
        Answer = answer,
        StopReason = CopilotAgentStopReason.Completed,
        HasSuccessfulEvidence = true,
        ToolNames = ["ReadLocalFile"],
        SessionResumed = sessionResumed,
        SessionCheckpoint = checkpoint,
    };

    private static CopilotAgentSessionCheckpoint CreateCheckpoint(string marker)
    {
        var checkpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "profile-" + marker,
            SerializedSessionJson = "{\"marker\":\"" + marker + "\"}",
        };
        Assert.True(checkpoint.IsStructurallyValid());
        return checkpoint;
    }

    private sealed class StubRunner(params CopilotSubagentResult[] results) : ICopilotSubagentRunner
    {
        private int _index;

        public List<CopilotSubagentRunRequest> Requests { get; } = new();

        public Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            Requests.Add(runRequest);
            var result = results[_index++];
            return Task.FromResult(new CopilotSubagentResult
            {
                RoleId = role.Id,
                RunId = runRequest.RunId,
                RequestTokenBudget = runRequest.RequestTokenBudget,
                QueueDurationMs = runRequest.QueueDurationMs,
                Answer = result.Answer,
                StopReason = result.StopReason,
                Usage = result.Usage,
                Budget = result.Budget,
                ToolNames = result.ToolNames,
                WasTruncated = result.WasTruncated,
                UsedBudgetFinalization = result.UsedBudgetFinalization,
                UsedPreselectedEvidence = result.UsedPreselectedEvidence,
                HasSuccessfulEvidence = result.HasSuccessfulEvidence,
                SessionResumed = result.SessionResumed,
                ResumeFailureReason = result.ResumeFailureReason,
                SessionCheckpoint = result.SessionCheckpoint,
            });
        }
    }
}

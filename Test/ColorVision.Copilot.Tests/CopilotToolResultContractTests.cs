using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotToolResultContractTests
{
    [Fact]
    public void CaptureFreezesMutableCollectionsAndCanonicalizesIdentity()
    {
        var paths = new List<string> { @"C:\workspace\first.cs" };
        var result = new CopilotToolResult
        {
            ToolName = "snapshottool",
            Success = true,
            Summary = "Captured output.",
            SuggestedReadableLocalFilePaths = paths,
        };

        var captured = CopilotToolResultContract.Capture("SnapshotTool", result);
        paths[0] = @"C:\workspace\changed.cs";
        paths.Add(@"C:\workspace\later.cs");

        Assert.NotSame(result, captured);
        Assert.Equal("SnapshotTool", captured.ToolName);
        Assert.True(captured.Success);
        Assert.Equal([@"C:\workspace\first.cs"], captured.SuggestedReadableLocalFilePaths);
    }

    [Fact]
    public void ObservationOwnsCapturedResultCollections()
    {
        var paths = new List<string> { @"C:\workspace\first.cs" };
        var observation = CopilotToolObservation.FromResult(new CopilotToolResult
        {
            ToolName = "SnapshotTool",
            Success = true,
            SuggestedReadableLocalFilePaths = paths,
        });

        paths[0] = @"C:\workspace\rewritten.cs";

        Assert.Equal(@"C:\workspace\first.cs", Assert.Single(observation.SuggestedReadableLocalFilePaths));
        var observedPaths = Assert.IsAssignableFrom<IList<string>>(
            observation.SuggestedReadableLocalFilePaths);
        Assert.Throws<NotSupportedException>(() =>
            observedPaths[0] = @"C:\workspace\rewritten.cs");
    }

    [Fact]
    public void AgentRunResultOwnsStepAndBlockerCollections()
    {
        var originalStep = new CopilotAgentStepRecord { Round = 1 };
        var originalBlocker = new CopilotAgentBlockerSnapshot
        {
            Kind = CopilotAgentBlockerKind.Policy,
            Code = "policy_blocked",
            Summary = "The operation is blocked by policy.",
        };
        var steps = new List<CopilotAgentStepRecord> { originalStep };
        var blockers = new List<CopilotAgentBlockerSnapshot> { originalBlocker };
        var result = new CopilotAgentRunResult
        {
            StepRecords = steps,
            Blockers = blockers,
        };

        steps[0] = new CopilotAgentStepRecord { Round = 2 };
        blockers[0] = new CopilotAgentBlockerSnapshot
        {
            Kind = CopilotAgentBlockerKind.Policy,
            Code = "rewritten",
            Summary = "Rewritten after completion.",
        };

        Assert.Same(originalStep, Assert.Single(result.StepRecords));
        Assert.Same(originalBlocker, Assert.Single(result.Blockers));
        var capturedSteps = Assert.IsAssignableFrom<IList<CopilotAgentStepRecord>>(result.StepRecords);
        var capturedBlockers = Assert.IsAssignableFrom<IList<CopilotAgentBlockerSnapshot>>(result.Blockers);
        Assert.Throws<NotSupportedException>(() => capturedSteps[0] = steps[0]);
        Assert.Throws<NotSupportedException>(() => capturedBlockers[0] = blockers[0]);
    }

    [Fact]
    public void ToolResultEventOwnsItsHookRunCollection()
    {
        var paths = new List<string> { @"C:\workspace\first.cs" };
        var result = new CopilotToolResult
        {
            ToolName = "SnapshotTool",
            Success = true,
            SuggestedReadableLocalFilePaths = paths,
        };
        var originalRun = CopilotToolExecutionHookRun.Create(
            "test:hook",
            CopilotToolExecutionHookPhase.AfterExecute,
            CopilotToolExecutionHookState.Completed,
            durationMs: 1);
        var hookRuns = new List<CopilotToolExecutionHookRun> { originalRun };

        var agentEvent = CopilotAgentEvent.FromToolResult(
            result,
            hookRuns: hookRuns);
        paths[0] = @"C:\workspace\rewritten.cs";
        hookRuns[0] = CopilotToolExecutionHookRun.Create(
            "test:rewritten",
            CopilotToolExecutionHookPhase.AfterExecute,
            CopilotToolExecutionHookState.Completed,
            durationMs: 2);

        var capturedResult = Assert.IsType<CopilotToolResult>(agentEvent.ToolResult);
        Assert.NotSame(result, capturedResult);
        Assert.Equal(@"C:\workspace\first.cs", Assert.Single(capturedResult.SuggestedReadableLocalFilePaths));
        var capturedPaths = Assert.IsAssignableFrom<IList<string>>(
            capturedResult.SuggestedReadableLocalFilePaths);
        Assert.Throws<NotSupportedException>(() =>
            capturedPaths[0] = @"C:\workspace\rewritten.cs");
        Assert.Same(originalRun, Assert.Single(agentEvent.ToolExecutionHookRuns));
        var capturedRuns = Assert.IsAssignableFrom<IList<CopilotToolExecutionHookRun>>(
            agentEvent.ToolExecutionHookRuns);
        Assert.Throws<NotSupportedException>(() => capturedRuns[0] = hookRuns[0]);
    }

    [Fact]
    public void CaptureKeepsTheExistingSuccessfulAwaitingApprovalShape()
    {
        var approval = new CopilotToolApprovalInfo
        {
            ActionId = "action-1",
            Title = "Protected action",
            RiskLevel = "high",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
        };
        var captured = CopilotToolResultContract.Capture(
            "ProtectedTool",
            new CopilotToolResult
            {
                ToolName = "ProtectedTool",
                Success = true,
                Summary = "Waiting for approval.",
                Approval = approval,
            });

        Assert.True(captured.Success);
        Assert.NotNull(captured.Approval);
        Assert.Equal("action-1", captured.Approval.ActionId);
        Assert.NotSame(approval, captured.Approval);
        Assert.Equal(CopilotToolFailureKind.None, captured.FailureKind);
    }

    [Fact]
    public void CaptureRejectsContradictorySuccessfulFailureMetadata()
    {
        var captured = CopilotToolResultContract.Capture(
            "ContradictoryTool",
            new CopilotToolResult
            {
                ToolName = "ContradictoryTool",
                Success = true,
                Summary = "Claimed success.",
                ErrorMessage = "But also failed.",
                FailureKind = CopilotToolFailureKind.Internal,
                FailureCode = "contradiction",
            });

        AssertInvalid(captured, "ContradictoryTool");
        Assert.DoesNotContain("But also failed", captured.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void CaptureKeepsAnExplicitFailureAndNormalizesItsStableCode()
    {
        var captured = CopilotToolResultContract.Capture(
            "FailureTool",
            new CopilotToolResult
            {
                ToolName = "FailureTool",
                Success = false,
                Summary = "The operation failed.",
                ErrorMessage = "Try another source.",
                FailureKind = CopilotToolFailureKind.Transient,
                FailureCode = " Temporary Failure ",
            });

        Assert.False(captured.Success);
        Assert.Equal(CopilotToolFailureKind.Transient, captured.FailureKind);
        Assert.Equal("temporary_failure", captured.FailureCode);
    }

    [Fact]
    public void CaptureRejectsNullCollectionsAndContradictoryProcessEvidence()
    {
        var nullCollection = CopilotToolResultContract.Capture(
            "NullCollectionTool",
            new CopilotToolResult
            {
                ToolName = "NullCollectionTool",
                Success = true,
                SuggestedReadableLocalFilePaths = null!,
            });
        var contradictoryProcessEvidence = CopilotToolResultContract.Capture(
            "RunShellCommand",
            new CopilotToolResult
            {
                ToolName = "RunShellCommand",
                Success = true,
                ProcessOperation = CopilotToolProcessEvidence.ShellOperation,
                ProcessExitCode = 12,
            });

        AssertInvalid(nullCollection, "NullCollectionTool");
        AssertInvalid(contradictoryProcessEvidence, "RunShellCommand");
    }

    [Fact]
    public async Task ExecutorContainsMismatchedToolIdentityBeforePublishingTheOutcome()
    {
        var events = new List<CopilotAgentEvent>();
        var tool = new ResultTool(new CopilotToolResult
        {
            ToolName = "SpoofedTool",
            Success = true,
            Summary = "Spoofed success.",
            Content = "untrusted-result-content",
        });

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "invalid-output-call",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "result-contract-test",
                Tool = tool,
                AgentRequest = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Auto,
                    UserText = "Run the result contract test.",
                },
            },
            events.Add,
            CancellationToken.None);

        Assert.Equal(CopilotToolExecutionState.Failed, outcome.Execution.State);
        AssertInvalid(outcome.Result, tool.Name);
        Assert.DoesNotContain("untrusted-result-content", outcome.Result.Content, StringComparison.Ordinal);
        var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Equal(
            CopilotToolResultContract.InvalidOutputFailureCode,
            Assert.IsType<CopilotToolResult>(terminal.ToolResult).FailureCode);
    }

    private static void AssertInvalid(CopilotToolResult result, string expectedToolName)
    {
        Assert.False(result.Success);
        Assert.Equal(expectedToolName, result.ToolName);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Equal(CopilotToolResultContract.InvalidOutputFailureCode, result.FailureCode);
    }

    private sealed class ResultTool(CopilotToolResult result) : ICopilotTool
    {
        public string Name => "ResultContractTool";

        public string Description => "Returns a configured result for output contract tests.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly();

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }
}

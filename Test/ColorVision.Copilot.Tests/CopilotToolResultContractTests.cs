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

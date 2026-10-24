namespace ColorVision.Copilot.Tests;

public sealed class CopilotToolResultContractTests
{
    [Theory]
    [InlineData(CopilotToolFailureKind.None, CopilotToolFailureKind.Unspecified)]
    [InlineData(CopilotToolFailureKind.Authorization, CopilotToolFailureKind.Authorization)]
    public void CapabilityFailureAdapterPreservesTheFailureWithoutInventingACause(CopilotToolFailureKind sourceKind, CopilotToolFailureKind expectedKind)
    {
        var result = new CopilotCapabilityResult
        {
            Success = false, Summary = "Read failed.", ErrorMessage = "The file could not be read.",
            FailureKind = sourceKind,
        }.ToToolResult("ReadLocalFile");
        var captured = CopilotToolResultContract.Capture("ReadLocalFile", result);
        Assert.False(captured.Success);
        Assert.Equal(expectedKind, captured.FailureKind);
        Assert.Equal(result.ErrorMessage, captured.ErrorMessage);
        Assert.NotEqual(CopilotToolResultContract.InvalidOutputFailureCode, captured.FailureCode);
    }

    [Fact]
    public void CapabilityAdapterDoesNotHideContradictorySuccessMetadata()
    {
        var result = new CopilotCapabilityResult { Success = true, ErrorMessage = "Failed anyway." }.ToToolResult("InvalidCapability");
        AssertInvalid(CopilotToolResultContract.Capture("InvalidCapability", result), "InvalidCapability");
    }

    [Fact]
    public void CaptureFreezesMutableCollectionsAndCanonicalizesIdentity()
    {
        var paths = new List<string> { @"C:\workspace\first.cs" };
        var sourceUrls = new List<string> { "https://example.test/First" };
        var result = new CopilotToolResult
        {
            ToolName = "snapshottool",
            Success = true,
            Summary = "Captured output.",
            SuggestedReadableLocalFilePaths = paths,
            LocalObservationScopePaths = paths,
            WorkspaceRecheckPaths = paths,
            WebEvidenceSourceUrls = sourceUrls,
        };

        var captured = CopilotToolResultContract.Capture("SnapshotTool", result);
        paths[0] = @"C:\workspace\changed.cs";
        paths.Add(@"C:\workspace\later.cs");
        sourceUrls[0] = "https://example.test/Changed";
        sourceUrls.Add("https://example.test/Later");

        Assert.NotSame(result, captured);
        Assert.Equal("SnapshotTool", captured.ToolName);
        Assert.True(captured.Success);
        Assert.Equal([@"C:\workspace\first.cs"], captured.SuggestedReadableLocalFilePaths);
        Assert.Equal([@"C:\workspace\first.cs"], captured.LocalObservationScopePaths);
        Assert.Equal([@"C:\workspace\first.cs"], captured.WorkspaceRecheckPaths);
        Assert.Equal(["https://example.test/First"], captured.WebEvidenceSourceUrls);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<string>>(captured.WebEvidenceSourceUrls)[0] = sourceUrls[0]);
    }

    [Fact]
    public void CaptureValidatesTheFrozenCollectionInsteadOfReenumeratingTheSource()
    {
        var paths = new AlternatingPathCollection(
            @"C:\workspace\captured.cs",
            null!);
        var sourceUrls = new AlternatingPathCollection("https://example.test/Captured", null!);
        var captured = CopilotToolResultContract.Capture(
            "SnapshotTool",
            new CopilotToolResult
            {
                ToolName = "SnapshotTool",
                Success = true,
                Summary = "Captured output.",
                SuggestedReadableLocalFilePaths = paths,
                WebEvidenceSourceUrls = sourceUrls,
            });

        Assert.True(captured.Success);
        Assert.Equal(@"C:\workspace\captured.cs", Assert.Single(captured.SuggestedReadableLocalFilePaths));
        Assert.Equal(1, paths.EnumerationCount);
        Assert.Equal("https://example.test/Captured", Assert.Single(captured.WebEvidenceSourceUrls!));
        Assert.Equal(1, sourceUrls.EnumerationCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    public void WebEvidenceSourceMetadataPreservesAbsentAndAuthoritativeEmptyAcrossResultBoundaries(int? sourceCount)
    {
        IReadOnlyList<string>? sourceUrls = sourceCount switch
        {
            null => null,
            0 => Array.Empty<string>(),
            _ => new[] { "https://example.test/Source" },
        };
        var adapted = new CopilotCapabilityResult
        {
            Success = true,
            WebEvidenceSourceUrls = sourceUrls,
        }.ToToolResult("WebSearch");
        var captured = CopilotToolResultContract.Capture("WebSearch", adapted);
        var observation = CopilotToolObservation.FromResult(captured);
        var runResult = new CopilotAgentRunResult
        {
            StepRecords = new[]
            {
                new CopilotAgentStepRecord { Observation = observation, ModelObservation = observation },
            },
        };
        var step = Assert.Single(runResult.StepRecords);

        Assert.True(captured.Success);
        foreach (var actual in new[]
        {
            adapted.WebEvidenceSourceUrls,
            captured.WebEvidenceSourceUrls,
            observation.WebEvidenceSourceUrls,
            step.Observation.WebEvidenceSourceUrls,
            step.EffectiveModelObservation.WebEvidenceSourceUrls,
        })
        {
            if (sourceUrls == null)
                Assert.Null(actual);
            else
                Assert.Equal(sourceUrls, Assert.IsAssignableFrom<IReadOnlyList<string>>(actual));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CaptureRejectsMissingWebEvidenceSourceElements(string? sourceUrl)
    {
        var captured = CopilotToolResultContract.Capture("WebSearch", new CopilotToolResult
        {
            ToolName = "WebSearch",
            Success = true,
            WebEvidenceSourceUrls = new[] { sourceUrl! },
        });

        AssertInvalid(captured, "WebSearch");
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
    public void CaptureRejectsInvalidDelegatedAccountingBeforeItReachesBudgetAggregation()
    {
        var negativeUsage = CaptureDelegatedResult(new CopilotTokenUsage(-1, 2, 2));
        var impossibleCachedUsage = CaptureDelegatedResult(new CopilotTokenUsage(2, 1, 3, 3));
        var negativeDuration = CaptureDelegatedResult(CopilotTokenUsage.Empty, queueDurationMs: -1);

        AssertInvalid(negativeUsage, "DelegateTool");
        AssertInvalid(impossibleCachedUsage, "DelegateTool");
        AssertInvalid(negativeDuration, "DelegateTool");
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

    private static CopilotToolResult CaptureDelegatedResult(
        CopilotTokenUsage usage,
        long queueDurationMs = 0)
    {
        return CopilotToolResultContract.Capture(
            "DelegateTool",
            new CopilotToolResult
            {
                ToolName = "DelegateTool",
                Success = true,
                DelegatedRunUsage = new CopilotDelegatedRunUsage
                {
                    StopReason = CopilotAgentStopReason.Completed,
                    QueueDurationMs = queueDurationMs,
                    Usage = usage,
                },
            });
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

    private sealed class AlternatingPathCollection(
        string firstValue,
        string secondValue) : IReadOnlyList<string>
    {
        private int _enumerationCount;

        public int Count => 1;

        public int EnumerationCount => Volatile.Read(ref _enumerationCount);

        public string this[int index] => index == 0 ? firstValue : throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<string> GetEnumerator()
        {
            var enumeration = Interlocked.Increment(ref _enumerationCount);
            yield return enumeration == 1 ? firstValue : secondValue;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

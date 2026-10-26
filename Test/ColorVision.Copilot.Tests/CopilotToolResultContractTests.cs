using System.IO;
using System.Text.Json;

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

    [Theory]
    [InlineData(CopilotToolIdempotency.Idempotent)]
    [InlineData(CopilotToolIdempotency.NonIdempotent)]
    public async Task WriteThenThrowPreservesUnknownOutcomeAndWorkspaceEvidence(CopilotToolIdempotency idempotency)
    {
        var path = Path.GetTempFileName();
        try
        {
            var tool = new ExceptionalResultTool(path, writeCapable: true, idempotency: idempotency);
            var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
            var outcome = await new CopilotToolExecutor(hooks: []).ExecuteAsync(
                CreateExceptionalInvocation(tool),
                agentEvent => CopilotAssistantMessagePresenter.ApplyAgentEvent(message, agentEvent),
                CancellationToken.None);

            Assert.Equal("written before response failure", File.ReadAllText(path));
            Assert.Equal(1, tool.ExecutionCount);
            Assert.Equal(CopilotToolExecutionState.Failed, outcome.Execution.State);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, outcome.Result.FailureKind);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, outcome.Result.FailureCode);
            Assert.False(outcome.Execution.RetryEligible);
            Assert.Equal([path], outcome.Result.WorkspaceRecheckPaths);
            Assert.Contains("The tool response was lost.", outcome.Result.ErrorMessage, StringComparison.Ordinal);
            var trace = Assert.Single(message.AgentTraceEntries);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, trace.FailureKind);
            Assert.Equal([path], trace.WorkspaceRecheckPaths);
            using var modelResult = JsonDocument.Parse(CopilotFrameworkToolResultFormatter.Format(outcome));
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, modelResult.RootElement.GetProperty("failure_code").GetString());
            Assert.False(modelResult.RootElement.GetProperty("retry_allowed").GetBoolean());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("identity")]
    public async Task WriteThenReturnInvalidResultPreservesUnknownOutcomeAndWorkspaceEvidence(string invalidResultKind)
    {
        var path = Path.GetTempFileName();
        try
        {
            var tool = new ExceptionalResultTool(path, writeCapable: true, invalidResultKind: invalidResultKind);
            var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
            var outcome = await new CopilotToolExecutor(hooks: []).ExecuteAsync(
                CreateExceptionalInvocation(tool),
                agentEvent => CopilotAssistantMessagePresenter.ApplyAgentEvent(message, agentEvent),
                CancellationToken.None);

            Assert.Equal("written before response failure", File.ReadAllText(path));
            Assert.Equal(1, tool.ExecutionCount);
            Assert.Equal(CopilotToolExecutionState.Failed, outcome.Execution.State);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, outcome.Result.FailureKind);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, outcome.Result.FailureCode);
            Assert.False(outcome.Execution.RetryEligible);
            Assert.Equal([path], outcome.Result.WorkspaceRecheckPaths);
            Assert.DoesNotContain("untrusted-result-content", outcome.Result.Content, StringComparison.Ordinal);
            var trace = Assert.Single(message.AgentTraceEntries);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, trace.FailureKind);
            Assert.Equal([path], trace.WorkspaceRecheckPaths);
            using var modelResult = JsonDocument.Parse(CopilotFrameworkToolResultFormatter.Format(outcome));
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, modelResult.RootElement.GetProperty("failure_code").GetString());
            Assert.False(modelResult.RootElement.GetProperty("retry_allowed").GetBoolean());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KnownFailureBeforeDispatchOrFromStructuredResultRemainsAuthoritative(bool failBeforeDispatch)
    {
        var tool = new ExceptionalResultTool(
            @"C:\workspace\unchanged.txt",
            writeCapable: true,
            failDuringMetadata: failBeforeDispatch,
            returnStructuredFailure: !failBeforeDispatch);
        var outcome = await new CopilotToolExecutor(hooks: []).ExecuteAsync(
            CreateExceptionalInvocation(tool), _ => { }, CancellationToken.None);

        Assert.Equal(failBeforeDispatch ? 0 : 1, tool.ExecutionCount);
        Assert.Equal(CopilotToolFailureKind.Transient, outcome.Result.FailureKind);
        Assert.NotEqual(CopilotToolFailureCode.OutcomeUnknown, outcome.Result.FailureCode);
        Assert.True(outcome.Execution.RetryEligible);
        Assert.Empty(outcome.Result.WorkspaceRecheckPaths);
        if (!failBeforeDispatch)
            Assert.Equal("explicit_failure", outcome.Result.FailureCode);
    }

    [Theory]
    [InlineData(CopilotToolIdempotency.Idempotent, CopilotToolFailureKind.Transient, true)]
    [InlineData(CopilotToolIdempotency.NonIdempotent, CopilotToolFailureKind.OutcomeUnknown, false)]
    public async Task ReadOnlyExceptionRespectsDeclaredIdempotency(
        CopilotToolIdempotency idempotency,
        CopilotToolFailureKind expectedFailure,
        bool retryEligible)
    {
        var tool = new ExceptionalResultTool(string.Empty, writeCapable: false, idempotency: idempotency);
        var outcome = await new CopilotToolExecutor(hooks: []).ExecuteAsync(
            CreateExceptionalInvocation(tool), _ => { }, CancellationToken.None);

        Assert.Equal(1, tool.ExecutionCount);
        Assert.Equal(expectedFailure, outcome.Result.FailureKind);
        Assert.Equal(retryEligible, outcome.Execution.RetryEligible);
        Assert.Empty(outcome.Result.WorkspaceRecheckPaths);
    }

    private static CopilotToolInvocation CreateExceptionalInvocation(ICopilotTool tool) => new()
    {
        CallId = "exceptional-output-call",
        Round = 1,
        Attempt = 1,
        MaxAttempts = 2,
        RuntimeName = "result-contract-test",
        Tool = tool,
        FrameworkApprovalGranted = tool.Capability.Access == CopilotToolAccess.Write,
        AgentRequest = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            UserText = "Run the result contract test.",
        },
    };

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

    private sealed class ExceptionalResultTool(
        string path,
        bool writeCapable,
        CopilotToolIdempotency idempotency = CopilotToolIdempotency.Idempotent,
        bool failDuringMetadata = false,
        bool returnStructuredFailure = false,
        string invalidResultKind = "") : ICopilotFrameworkApprovedTool, ICopilotWorkspaceMutationEvidenceSource
    {
        public int ExecutionCount { get; private set; }

        public string Name => "ExceptionalResultTool";

        public string Description => "Exercises execution failures before and after the tool body starts.";

        public CopilotToolCapabilityDescriptor Capability { get; } = writeCapable
            ? CopilotToolCapabilityDescriptor.ProtectedWrite(idempotency)
            : CopilotToolCapabilityDescriptor.ReadOnly() with { Idempotency = idempotency };

        public bool CanHandle(CopilotAgentRequest request) => true;

        public IReadOnlyList<string> GetWorkspaceRecheckPaths(CopilotAgentRequest request, CopilotAgentToolInput input) =>
            failDuringMetadata ? throw new IOException("Workspace metadata could not be read.") : [path];

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken) =>
            ExecuteApprovedAsync(request, input, cancellationToken);

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            if (returnStructuredFailure)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The operation was rejected before any change.",
                    ErrorMessage = "The destination was temporarily unavailable.",
                    FailureKind = CopilotToolFailureKind.Transient,
                    FailureCode = "explicit_failure",
                });
            }
            if (writeCapable)
                File.WriteAllText(path, "written before response failure");
            if (invalidResultKind == "null")
                return Task.FromResult<CopilotToolResult>(null!);
            if (invalidResultKind == "identity")
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = "UnexpectedTool",
                    Success = true,
                    Content = "untrusted-result-content",
                });
            }
            throw new IOException("The tool response was lost.");
        }
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

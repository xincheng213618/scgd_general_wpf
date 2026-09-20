using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAgentBlockerTests
{
    [Theory]
    [InlineData("recovered", false)]
    [InlineData("different-path", true)]
    [InlineData("different-range", true)]
    [InlineData("different-tool", true)]
    [InlineData("same-call-id", true)]
    [InlineData("success-before-failure", true)]
    [InlineData("later-failure", true)]
    [InlineData("unfinished-call", true)]
    [InlineData("unsuccessful-result", true)]
    [InlineData("write", true)]
    [InlineData("unknown-outcome", true)]
    public void OnlyALaterSuccessfulExactReadResolvesAnEarlierFailure(string scenario, bool expectBlocker)
    {
        var ledger = new CopilotAgentTaskLedgerSnapshot { Mode = "execute", Items = [new() { Id = 1, Title = "Continue work" }] };
        var failed = CreateReadStep("failed-read", success: false, write: scenario == "write", unknownOutcome: scenario == "unknown-outcome");
        var successful = CreateReadStep(scenario == "same-call-id" ? "failed-read" : "fresh-read", success: scenario != "unsuccessful-result",
            path: scenario == "different-path" ? "other.json" : "camera.json", startLine: scenario == "different-range" ? 2 : null,
            name: scenario == "different-tool" ? "ReadAttachedFile" : "ReadLocalFile", write: scenario == "write",
            state: scenario == "unfinished-call" ? CopilotToolExecutionState.Running : null);
        CopilotAgentStepRecord[] steps = scenario switch
        {
            "success-before-failure" => [successful, failed],
            "later-failure" => [failed, successful, CreateReadStep("later-failure", success: false)],
            _ => [failed, successful],
        };
        var blockers = CopilotAgentBlockerDetector.Detect(ledger, steps, CopilotAgentStopReason.TaskPassLimit);
        Assert.Equal(expectBlocker, blockers.Count != 0);
        if (!expectBlocker)
        {
            Assert.False(steps[0].Observation.Success);
            Assert.Equal(CopilotToolExecutionState.Failed, steps[0].Execution.State);
        }
        if (scenario == "later-failure") Assert.Equal(CopilotAgentTaskEventIds.ForCall("later-failure"), Assert.Single(blockers).SourceCallKey);
    }

    [Theory]
    [InlineData(CopilotAgentMode.Auto)]
    [InlineData(CopilotAgentMode.Plan)]
    public void ResolvedReadDenialDoesNotKeepTheTaskInAnApprovalState(CopilotAgentMode mode)
    {
        var ledger = new CopilotAgentTaskLedgerSnapshot { Mode = "execute", Items = [new() { Id = 1, Title = "Continue work" }] };
        var denied = CreateReadStep("denied-read", success: false, state: CopilotToolExecutionState.Denied);
        var read = CreateReadStep("approved-fresh-read", success: true);
        var reason = CopilotMicrosoftAgentFrameworkRuntime.DetermineStopReason(ledger, new(), [denied, read], hasModelFinalAnswer: true, requestMode: mode);
        Assert.Equal(mode == CopilotAgentMode.Plan ? CopilotAgentStopReason.Completed : CopilotAgentStopReason.TaskPassLimit, reason);
        Assert.Empty(CopilotAgentBlockerDetector.Detect(ledger, [denied, read], reason));
        Assert.Equal(CopilotAgentStopReason.BudgetExhausted,
            CopilotMicrosoftAgentFrameworkRuntime.DetermineStopReason(ledger, new() { ToolBudgetExhausted = true }, [denied, read], true, mode));
    }

    [Fact]
    public void ResolvingOneReadDoesNotHideAnotherUnresolvedFailure()
    {
        var ledger = new CopilotAgentTaskLedgerSnapshot { Mode = "execute", Items = [new() { Id = 1, Title = "Continue work" }] };
        var unresolved = CreateReadStep("unresolved-read", success: false, path: "other.json");
        var failed = CreateReadStep("recovered-read", success: false);
        var read = CreateReadStep("fresh-read", success: true);
        var blocker = Assert.Single(CopilotAgentBlockerDetector.Detect(ledger, [unresolved, failed, read], CopilotAgentStopReason.TaskPassLimit));
        Assert.Equal(CopilotAgentTaskEventIds.ForCall("unresolved-read"), blocker.SourceCallKey);
    }

    private static CopilotAgentStepRecord CreateReadStep(string callId, bool success, string path = "camera.json", int? startLine = null,
        string name = "ReadLocalFile", bool write = false, bool unknownOutcome = false, CopilotToolExecutionState? state = null)
    {
        var failureKind = success ? CopilotToolFailureKind.None : unknownOutcome ? CopilotToolFailureKind.OutcomeUnknown
            : state == CopilotToolExecutionState.Denied ? CopilotToolFailureKind.Authorization : CopilotToolFailureKind.NotFound;
        return new()
        {
            ToolCall = new() { ToolName = name, ToolInput = new() { Path = path, StartLine = startLine, Arguments = new Dictionary<string, object?> { ["path"] = path } } },
            Observation = new() { Success = success, FailureKind = failureKind, FailureCode = state == CopilotToolExecutionState.Denied ? "approval_rejected" : string.Empty },
            Execution = new()
            {
                CallId = callId, ToolName = name, Access = write ? CopilotToolAccess.Write : CopilotToolAccess.ReadOnly,
                State = state ?? (success ? CopilotToolExecutionState.Completed : CopilotToolExecutionState.Failed), FailureKind = failureKind,
            },
        };
    }

    [Fact]
    public void PersistedMessageOwnsItsBlockerCollection()
    {
        var blocker = new CopilotAgentBlockerSnapshot
        {
            Kind = CopilotAgentBlockerKind.Policy,
            Code = "policy_blocked",
            Summary = "The operation is blocked by policy.",
        };
        var source = new[] { blocker };
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            AgentBlockers = source,
        };

        source[0] = new CopilotAgentBlockerSnapshot
        {
            Kind = CopilotAgentBlockerKind.Policy,
            Code = "rewritten",
            Summary = "Rewritten after persistence.",
        };

        Assert.Same(blocker, Assert.Single(message.AgentBlockers));
        var persisted = Assert.IsAssignableFrom<IList<CopilotAgentBlockerSnapshot>>(
            message.AgentBlockers);
        Assert.Throws<NotSupportedException>(() => persisted[0] = source[0]);
    }

    [Fact]
    public void PersistedMessageFiltersBlockersWithNullTextFields()
    {
        var message = JsonConvert.DeserializeObject<CopilotChatMessage>(
            """
            {
              "AgentBlockers": [
                {
                  "Kind": 2,
                  "Code": "tool_failure",
                  "Summary": null,
                  "ToolName": null
                }
              ]
            }
            """)!;

        Assert.Empty(message.AgentBlockers);
    }

    [Theory]
    [InlineData(CopilotToolExecutionState.TimedOut)]
    [InlineData(CopilotToolExecutionState.Interrupted)]
    public void UnknownWriteOutcomeRequiresStateVerificationWithoutDuplicatingToolPrefix(
        CopilotToolExecutionState state)
    {
        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute",
            Items =
            [
                new CopilotAgentTaskItem
                {
                    Id = 1,
                    Title = "完成受保护写入",
                },
            ],
        };
        var step = new CopilotAgentStepRecord
        {
            Observation = new CopilotToolObservation
            {
                Success = false,
                FailureKind = CopilotToolFailureKind.OutcomeUnknown,
                FailureCode = CopilotToolFailureCode.OutcomeUnknown,
            },
            Execution = new CopilotToolExecutionInfo
            {
                CallId = "unknown-write-call",
                ToolName = "ProtectedWrite",
                State = state,
                FailureKind = CopilotToolFailureKind.OutcomeUnknown,
                RetryEligible = false,
            },
        };

        var blocker = Assert.Single(CopilotAgentBlockerDetector.Detect(
            ledger,
            [step],
            CopilotAgentStopReason.Completed));

        Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, blocker.Code);
        Assert.True(blocker.RequiresUserInput);
        Assert.False(blocker.RetryEligible);
        Assert.Contains("Verify the current state", blocker.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("approval_rejected", CopilotAgentStopReason.ApprovalDenied, CopilotAgentBlockerKind.Approval, "approval_denied")]
    [InlineData("tool_hook_denied", CopilotAgentStopReason.Blocked, CopilotAgentBlockerKind.Policy, "tool_hook_denied")]
    public void DeniedToolClassificationPreservesItsDecisionDomain(
        string failureCode,
        CopilotAgentStopReason expectedStopReason,
        CopilotAgentBlockerKind expectedBlockerKind,
        string expectedBlockerCode)
    {
        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute",
            Items =
            [
                new CopilotAgentTaskItem
                {
                    Id = 1,
                    Title = "Complete protected work",
                },
            ],
        };
        var step = new CopilotAgentStepRecord
        {
            Observation = new CopilotToolObservation
            {
                Success = false,
                FailureKind = CopilotToolFailureKind.Authorization,
                FailureCode = failureCode,
            },
            Execution = new CopilotToolExecutionInfo
            {
                CallId = "denied-call",
                ToolName = "ProtectedTool",
                State = CopilotToolExecutionState.Denied,
                FailureKind = CopilotToolFailureKind.Authorization,
            },
        };

        var stopReason = CopilotMicrosoftAgentFrameworkRuntime.DetermineStopReason(
            ledger,
            new CopilotAgentBudgetSnapshot(),
            [step],
            hasModelFinalAnswer: false);
        var blocker = Assert.Single(CopilotAgentBlockerDetector.Detect(
            ledger,
            [step],
            stopReason));

        Assert.Equal(expectedStopReason, stopReason);
        Assert.Equal(expectedBlockerKind, blocker.Kind);
        Assert.Equal(expectedBlockerCode, blocker.Code);
    }
}

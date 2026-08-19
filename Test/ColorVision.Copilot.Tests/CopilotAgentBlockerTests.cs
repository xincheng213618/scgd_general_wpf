using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAgentBlockerTests
{
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

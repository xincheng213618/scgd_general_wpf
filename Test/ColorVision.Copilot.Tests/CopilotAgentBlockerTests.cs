using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAgentBlockerTests
{
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
}

using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentToolBudgetCompletionGateTests
{
    [Fact]
    public void ExhaustionWaitsForEveryReservedRound()
    {
        var signalCount = 0;
        var gate = new CopilotAgentToolBudgetCompletionGate(() => signalCount++);
        gate.TrackReservedRound(15);
        gate.TrackReservedRound(16);

        gate.MarkExhausted();
        gate.CompleteRound(15);

        Assert.True(gate.IsExhausted);
        Assert.Equal(0, signalCount);

        gate.CompleteRound(16);
        Assert.Equal(1, signalCount);
    }

    [Fact]
    public void ExhaustionWithoutReservedWorkSignalsImmediatelyOnce()
    {
        var signalCount = 0;
        var gate = new CopilotAgentToolBudgetCompletionGate(() => signalCount++);

        gate.MarkExhausted();
        gate.MarkExhausted();
        gate.CompleteRound(999);

        Assert.True(gate.IsExhausted);
        Assert.Equal(1, signalCount);
    }

    [Fact]
    public void CompletedWorkDoesNotSignalBeforeExhaustion()
    {
        var signalCount = 0;
        var gate = new CopilotAgentToolBudgetCompletionGate(() => signalCount++);
        gate.TrackReservedRound(1);

        gate.CompleteRound(1);

        Assert.False(gate.IsExhausted);
        Assert.Equal(0, signalCount);
    }
}

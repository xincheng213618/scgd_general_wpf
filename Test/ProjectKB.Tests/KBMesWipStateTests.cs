using Xunit;

namespace ProjectKB.Tests;

public sealed class KBMesWipStateTests
{
    [Fact]
    public void CompletingPriorRunKeepsTheNextScannedSerialsApproval()
    {
        var state = new KBMesWipState();
        var first = state.BeginCheck("KB-001", "F100");
        state.BeginRun("KB-001", "F100");

        state.InputChanged("KB-002");
        first.IsApproved = true;
        Assert.False(state.IsCurrentInputCheck(first, "F100"));
        var second = state.BeginCheck("KB-002", "F100");
        second.IsApproved = true;

        Assert.True(state.ConsumeRunApproval("KB-001", "F100"));
        Assert.True(state.IsCurrentInputCheck(second, "F100"));
        state.EndRun();
        state.BeginRun("KB-002", "F100");
        Assert.True(state.ConsumeRunApproval("KB-002", "F100"));
        Assert.False(state.ConsumeRunApproval("KB-002", "F100"));
    }

    [Theory]
    [InlineData("KB-002", "F100")]
    [InlineData("KB-001", "F200")]
    public void ApprovalCannotBeConsumedForAnotherSerialOrStage(string serialNumber, string stage)
    {
        var state = new KBMesWipState();
        state.BeginRun("KB-001", "F100");
        state.BeginCheck("KB-001", "F100").IsApproved = true;

        Assert.False(state.ConsumeRunApproval(serialNumber, stage));
    }
}

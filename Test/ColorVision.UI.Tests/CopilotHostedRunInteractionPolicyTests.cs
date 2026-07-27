using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotHostedRunInteractionPolicyTests
{
    [Theory]
    [InlineData(CopilotHostedRunState.Queued, false, (int)CopilotHostedRunPrimaryAction.Cancel)]
    [InlineData(CopilotHostedRunState.Running, true, (int)CopilotHostedRunPrimaryAction.Stop)]
    [InlineData(CopilotHostedRunState.PauseRequested, false, (int)CopilotHostedRunPrimaryAction.Cancel)]
    [InlineData(CopilotHostedRunState.CancelRequested, false, (int)CopilotHostedRunPrimaryAction.None)]
    [InlineData(CopilotHostedRunState.Completed, false, (int)CopilotHostedRunPrimaryAction.None)]
    public void EvaluateMatchesHostedRunControlState(
        CopilotHostedRunState state,
        bool acceptsNewInput,
        int primaryAction)
    {
        var interaction = CopilotHostedRunInteractionPolicy.Evaluate(state);

        Assert.Equal(acceptsNewInput, interaction.AcceptsNewInput);
        Assert.Equal((CopilotHostedRunPrimaryAction)primaryAction, interaction.PrimaryAction);
    }

    [Fact]
    public void PauseThenCancelMovesFromEscalationToWaitingState()
    {
        var run = new CopilotHostedAgentRun("conversation", CopilotAgentMode.Auto);
        Assert.True(run.TryStart());
        Assert.True(run.TryMarkCheckpointReady());

        Assert.True(run.TryRequestPause());
        var pauseInteraction = CopilotHostedRunInteractionPolicy.Evaluate(run.State);
        Assert.False(pauseInteraction.AcceptsNewInput);
        Assert.Equal(CopilotHostedRunPrimaryAction.Cancel, pauseInteraction.PrimaryAction);

        Assert.True(run.TryRequestCancel());
        var cancelInteraction = CopilotHostedRunInteractionPolicy.Evaluate(run.State);
        Assert.False(cancelInteraction.AcceptsNewInput);
        Assert.Equal(CopilotHostedRunPrimaryAction.None, cancelInteraction.PrimaryAction);

        run.Complete(error: null);
    }
}

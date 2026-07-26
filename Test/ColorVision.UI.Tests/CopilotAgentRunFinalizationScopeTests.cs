using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentRunFinalizationScopeTests
{
    [Fact]
    public void NormalFinalizationKeepsRunCancellationToken()
    {
        using var runCancellation = new CancellationTokenSource();
        using var scope = CopilotAgentRunFinalizationScope.Create(
            CopilotAgentControlIntent.None,
            timeBudgetExhausted: false,
            runCancellation.Token);

        runCancellation.Cancel();

        Assert.True(scope.Token.IsCancellationRequested);
        Assert.False(scope.IsTimeoutCancellationRequested);
    }

    [Theory]
    [InlineData(CopilotAgentControlIntent.Pause, false)]
    [InlineData(CopilotAgentControlIntent.Cancel, false)]
    [InlineData(CopilotAgentControlIntent.None, true)]
    public void InterruptedFinalizationUsesIndependentBoundedToken(
        CopilotAgentControlIntent controlIntent,
        bool timeBudgetExhausted)
    {
        using var runCancellation = new CancellationTokenSource();
        runCancellation.Cancel();
        using var scope = CopilotAgentRunFinalizationScope.Create(
            controlIntent,
            timeBudgetExhausted,
            runCancellation.Token,
            TimeSpan.FromSeconds(1));

        Assert.False(scope.Token.IsCancellationRequested);
        Assert.False(scope.IsTimeoutCancellationRequested);
    }

    [Fact]
    public async Task InterruptedFinalizationTokenExpires()
    {
        using var scope = CopilotAgentRunFinalizationScope.Create(
            CopilotAgentControlIntent.Pause,
            timeBudgetExhausted: false,
            CancellationToken.None,
            TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Task.Delay(Timeout.InfiniteTimeSpan, scope.Token));

        Assert.True(scope.IsTimeoutCancellationRequested);
    }
}

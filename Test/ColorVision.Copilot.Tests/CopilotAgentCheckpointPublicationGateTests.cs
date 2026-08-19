using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAgentCheckpointPublicationGateTests
{
    [Fact]
    public async Task RequiredPublicationWaitsWhileBestEffortPublicationCanSkipContention()
    {
        var gate = new CopilotAgentCheckpointPublicationGate();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requiredStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = gate.RunRequiredAsync(
            async _ =>
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
                return true;
            },
            CancellationToken.None).AsTask();
        try
        {
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var bestEffortCallbackCalled = false;
            var bestEffort = await gate.TryRunAsync(
                _ =>
                {
                    bestEffortCallbackCalled = true;
                    return ValueTask.FromResult(true);
                },
                CancellationToken.None);
            var required = gate.RunRequiredAsync(
                _ =>
                {
                    requiredStarted.TrySetResult();
                    return ValueTask.FromResult(true);
                },
                CancellationToken.None).AsTask();

            Assert.False(bestEffort);
            Assert.False(bestEffortCallbackCalled);
            Assert.False(required.IsCompleted);

            releaseFirst.TrySetResult();

            Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5)));
            await requiredStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(await required.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            releaseFirst.TrySetResult();
        }
    }
}

using ColorVision.Engine.FlowProcessing;

namespace ColorVision.UI.Tests;

public class ViewFlowAsyncCommandTests
{
    [Fact]
    public async Task RunCommandObservesFailureFromStandalonePreparation()
    {
        var expected = new InvalidOperationException("start node selection failed");
        Exception? reported = null;
        bool executionStarted = false;
        Action prepareStandaloneRun = () => throw expected;

        await ViewFlow.ExecuteRunCommandAsync(
            async () =>
            {
                prepareStandaloneRun();
                executionStarted = true;
                await Task.CompletedTask;
            },
            ex => reported = ex);

        Assert.False(executionStarted);
        Assert.Same(expected, reported);
    }

    [Fact]
    public async Task RunCommandObservesFailureAfterLifecycleIsRestored()
    {
        var lifecycle = new FlowRunLifecycleGate();
        using var cancellation = new CancellationTokenSource();
        var expected = new InvalidOperationException("pre-journal failed");
        Exception? reported = null;

        Assert.True(lifecycle.TryBegin("SN-1", cancellation, engineIsRunning: false));

        await ViewFlow.ExecuteRunCommandAsync(
            async () =>
            {
                try
                {
                    await Task.Yield();
                    throw expected;
                }
                finally
                {
                    lifecycle.Complete("SN-1");
                }
            },
            ex => reported = ex);

        Assert.False(lifecycle.IsActive);
        Assert.Same(expected, reported);
    }
}

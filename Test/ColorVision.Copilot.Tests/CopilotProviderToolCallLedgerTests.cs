using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotProviderToolCallLedgerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task OriginatingCancellationWaitsForExecutionSettlement()
    {
        var ledger = new CopilotProviderToolCallLedger();
        using var cancellation = new CancellationTokenSource();
        var executionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSettlement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellation.Cancel();

        var callTask = ledger.ExecuteOnceAsync(
            "provider-call",
            "signature",
            async () =>
            {
                executionStarted.SetResult();
                try
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    return "unreachable";
                }
                catch (OperationCanceledException)
                {
                    await releaseSettlement.Task;
                    throw;
                }
            },
            cancellation.Token);

        await executionStarted.Task.WaitAsync(TestTimeout);
        Assert.False(callTask.IsCompleted);

        releaseSettlement.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => callTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task DuplicateWaiterCanCancelWithoutInterruptingOwnedExecution()
    {
        var ledger = new CopilotProviderToolCallLedger();
        var executionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseExecution = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ownerTask = ledger.ExecuteOnceAsync(
            "provider-call",
            "signature",
            async () =>
            {
                executionStarted.SetResult();
                return await releaseExecution.Task;
            },
            CancellationToken.None);
        await executionStarted.Task.WaitAsync(TestTimeout);

        using var duplicateCancellation = new CancellationTokenSource();
        duplicateCancellation.Cancel();
        var duplicateTask = ledger.ExecuteOnceAsync(
            "provider-call",
            "signature",
            () => Task.FromResult("duplicate factory must not run"),
            duplicateCancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => duplicateTask.WaitAsync(TestTimeout));
        Assert.False(ownerTask.IsCompleted);

        releaseExecution.SetResult("settled result");
        var ownerResult = await ownerTask.WaitAsync(TestTimeout);
        Assert.Equal("settled result", ownerResult.Content);
    }
}

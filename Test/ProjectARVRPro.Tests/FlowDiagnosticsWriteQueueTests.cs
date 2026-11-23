namespace ProjectARVRPro.Tests;

public sealed class FlowDiagnosticsWriteQueueTests
{
    [Fact]
    public async Task PendingWritesAreIsolatedBySerialAndBackpressureWaitsForTheOldestRun()
    {
        var queue = new FlowDiagnosticsWriteQueue();
        TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource second = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource third = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource fourth = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True(queue.Track("first", first.Task).IsCompletedSuccessfully);
        Assert.True(queue.Track("second", second.Task).IsCompletedSuccessfully);
        Assert.True(queue.Track("third", third.Task).IsCompletedSuccessfully);
        Assert.Same(first.Task, queue.Track("fourth", fourth.Task));
        Assert.False(await FlowDiagnosticsWriteQueue.WaitAsync(first.Task, TimeSpan.Zero));
        Assert.False(first.Task.IsCompleted);

        Assert.True(queue.WhenWritten("unused").IsCompletedSuccessfully);
        Task fourthWrite = queue.WhenWritten("fourth");
        fourth.SetResult();
        await fourthWrite;
        Assert.False(queue.WhenWritten("first").IsCompleted);
        Assert.False(queue.DrainAsync().IsCompleted);

        first.SetResult();
        Assert.Same(second.Task, queue.Track("next", Task.CompletedTask));
        second.SetResult();
        third.SetResult();
        await queue.DrainAsync();
        Assert.True(await FlowDiagnosticsWriteQueue.WaitAsync(queue.DrainAsync(), TimeSpan.Zero));
    }
}
